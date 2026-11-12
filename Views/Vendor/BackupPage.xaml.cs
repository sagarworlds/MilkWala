using Microsoft.Maui.Controls;
using MilkWala.Services.Data;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class BackupPage : ContentPage
    {
        private readonly VendorBackupViewModel _vm;

        public BackupPage(VendorBackupViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadBackupsAsync();
        }

        // Restoring replaces all current data, so ask first.
        private async void OnRestoreClicked(object? sender, EventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is not BackupFile backup)
                return;

            bool confirmed = await DisplayAlertAsync(
                "Restore this backup?",
                $"All current data will be replaced by the backup from {backup.CreatedAt:dd MMM yyyy, hh:mm tt}. " +
                "Your current data is saved first, so you can undo this.",
                "Restore",
                "Cancel");

            if (confirmed)
                await _vm.RestoreBackupCommand.ExecuteAsync(backup);
        }
    }
}
