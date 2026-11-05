using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Data;

namespace MilkWala.ViewModels
{
    /// <summary>Lets the vendor see every customer complaint, pick it up, and close it with a written answer.</summary>
    public partial class VendorComplaintsViewModel : BaseViewModel
    {
        [ObservableProperty]
        private ObservableCollection<Complaint> _complaints = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyPropertyChangedFor(nameof(CanStartWork))]
        [NotifyPropertyChangedFor(nameof(CanClose))]
        [NotifyPropertyChangedFor(nameof(CanReopen))]
        private Complaint? _selectedComplaint;

        [ObservableProperty]
        private string _resolutionNotes = string.Empty;

        [ObservableProperty]
        private int _unresolvedCount;

        public bool HasSelection => SelectedComplaint != null;
        public bool CanStartWork => SelectedComplaint?.Status == ComplaintStatus.Open;
        public bool CanClose => SelectedComplaint?.Status is ComplaintStatus.Open or ComplaintStatus.InProgress;
        public bool CanReopen => SelectedComplaint?.Status == ComplaintStatus.Closed;

        public VendorComplaintsViewModel(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Customer Complaints";
        }

        // Selecting a complaint loads its existing notes into the editor.
        partial void OnSelectedComplaintChanged(Complaint? value) => ResolutionNotes = value?.ResolutionNotes ?? string.Empty;

        [RelayCommand]
        public async Task LoadComplaintsAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var all = await DbService.GetAllAsync<Complaint>();
                var selectedId = SelectedComplaint?.Id;

                Complaints.Clear();
                // Needs-attention first, newest first within each group.
                foreach (var complaint in all
                    .OrderBy(c => ComplaintWorkflow.Priority(c.Status))
                    .ThenByDescending(c => c.CreatedAt))
                {
                    Complaints.Add(complaint);
                }

                UnresolvedCount = all.Count(c => c.Status != ComplaintStatus.Closed);
                SelectedComplaint = Complaints.FirstOrDefault(c => c.Id == selectedId);
            });
        }

        [RelayCommand]
        public Task StartWorkAsync() => ChangeStatusAsync(ComplaintStatus.InProgress, "marked in progress");

        [RelayCommand]
        public Task CloseComplaintAsync() => ChangeStatusAsync(ComplaintStatus.Closed, "closed");

        [RelayCommand]
        public Task ReopenComplaintAsync() => ChangeStatusAsync(ComplaintStatus.InProgress, "reopened");

        private async Task ChangeStatusAsync(ComplaintStatus target, string verb)
        {
            await RunSafeAsync(async () =>
            {
                if (SelectedComplaint == null)
                {
                    ErrorMessage = "Select a complaint first.";
                    return;
                }

                var id = SelectedComplaint.Id;
                var notes = ResolutionNotes;

                try
                {
                    await DbService.RunInTransactionAsync(connection =>
                        ComplaintLedger.Update(connection, id, target, notes));
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                StatusMessage = $"Complaint #{id} {verb}.";
                await LoadComplaintsAsync();
            });
        }
    }
}
