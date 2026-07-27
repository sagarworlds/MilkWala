using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class DeliveryManagementPage : ContentPage
    {
        private readonly VendorDeliveriesViewModel _vm;

        public DeliveryManagementPage(VendorDeliveriesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadDeliveriesAsync();
        }
    }
}
