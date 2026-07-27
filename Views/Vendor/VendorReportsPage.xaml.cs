using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class VendorReportsPage : ContentPage
    {
        private readonly VendorReportsViewModel _vm;

        public VendorReportsPage(VendorReportsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadReportsAsync();
        }
    }
}
