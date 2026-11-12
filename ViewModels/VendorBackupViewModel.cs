using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Services;
using MilkWala.Services.Data;

namespace MilkWala.ViewModels
{
    /// <summary>Backup and restore of the vendor's data.</summary>
    public partial class VendorBackupViewModel : BaseViewModel
    {
        private readonly IBackupService _backups;

        [ObservableProperty]
        private ObservableCollection<BackupFile> _backupFiles = new();

        [ObservableProperty]
        private bool _hasBackups;

        public VendorBackupViewModel(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory,
            IBackupService backups)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _backups = backups;
            Title = "Backup & Restore";
        }

        [RelayCommand]
        public async Task LoadBackupsAsync()
        {
            await RunSafeAsync(() =>
            {
                Refresh();
                return Task.CompletedTask;
            });
        }

        [RelayCommand]
        public async Task CreateBackupAsync()
        {
            await RunSafeAsync(async () =>
            {
                var backup = await _backups.CreateBackupAsync();
                Refresh();
                StatusMessage = $"Backup saved ({backup.SizeText}). Use Share to keep a copy off this phone.";
            });
        }

        [RelayCommand]
        public async Task ShareBackupAsync(BackupFile backup)
        {
            await RunSafeAsync(async () =>
            {
                if (backup == null) return;
                await _backups.ShareAsync(backup);
            });
        }

        [RelayCommand]
        public async Task RestoreBackupAsync(BackupFile backup)
        {
            await RunSafeAsync(async () =>
            {
                if (backup == null) return;

                try
                {
                    await _backups.RestoreAsync(backup);
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                Refresh();
                StatusMessage = "Backup restored. Your previous data was saved as a pre-restore backup in this list.";
            });
        }

        private void Refresh()
        {
            BackupFiles.Clear();
            foreach (var file in _backups.ListBackups())
            {
                BackupFiles.Add(file);
            }
            HasBackups = BackupFiles.Count > 0;
        }
    }
}
