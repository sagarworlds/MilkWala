using Microsoft.Maui.Controls;
using MilkWala.Localization;
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
                Strings.RestoreThisBackup,
                string.Format(Strings.FmtRestoreConfirm, backup.CreatedAt) + " " + Strings.YourCurrentDataIsSavedFirst,
                Strings.Restore,
                Strings.Cancel);

            if (confirmed)
                await _vm.RestoreBackupCommand.ExecuteAsync(backup);
        }
    }
}
