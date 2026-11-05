using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class ComplaintManagementPage : ContentPage
    {
        private readonly VendorComplaintsViewModel _vm;

        public ComplaintManagementPage(VendorComplaintsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadComplaintsAsync();
        }
    }
}
