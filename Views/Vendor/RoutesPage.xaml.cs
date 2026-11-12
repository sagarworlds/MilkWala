using Microsoft.Maui.Controls;
using MilkWala.Services.Identity;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class RoutesPage : ContentPage
    {
        private readonly VendorRoutesViewModel _vm;

        public RoutesPage(VendorRoutesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadAsync();
        }

        // Deleting cannot be undone, so ask first.
        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is not RouteSummary route)
                return;

            bool confirmed = await DisplayAlertAsync(
                "Delete this route?",
                $"\"{route.RouteName}\" will be removed. Routes that still have customers cannot be deleted.",
                "Delete",
                "Cancel");

            if (confirmed)
                await _vm.DeleteRouteCommand.ExecuteAsync(route);
        }
    }
}
