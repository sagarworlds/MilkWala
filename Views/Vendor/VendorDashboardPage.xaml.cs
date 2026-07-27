using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class VendorDashboardPage : ContentPage
    {
        private readonly VendorDashboardViewModel _vm;

        public VendorDashboardPage(VendorDashboardViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadDashboardDataAsync();
        }
    }
}
