using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class CustomerManagementPage : ContentPage
    {
        private readonly VendorCustomersViewModel _vm;

        public CustomerManagementPage(VendorCustomersViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        private async void OnManageRoutesClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("routes");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadCustomersAsync();
        }
    }
}
