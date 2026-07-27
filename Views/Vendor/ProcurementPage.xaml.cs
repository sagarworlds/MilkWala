using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class ProcurementPage : ContentPage
    {
        private readonly VendorProcurementViewModel _vm;

        public ProcurementPage(VendorProcurementViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadProcurementsAsync();
        }
    }
}
