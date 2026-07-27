using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class DairyStorePage : ContentPage
    {
        private readonly CustomerStoreViewModel _vm;

        public DairyStorePage(CustomerStoreViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadStoreCatalogAsync();
        }
    }
}
