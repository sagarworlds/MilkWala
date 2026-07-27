using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Vendor
{
    public partial class InventoryExpensePage : ContentPage
    {
        private readonly VendorInventoryExpenseViewModel _vm;

        public InventoryExpensePage(VendorInventoryExpenseViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadDataAsync();
        }
    }
}
