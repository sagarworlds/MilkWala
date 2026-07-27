using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class CustomerDashboardPage : ContentPage
    {
        private readonly CustomerDashboardViewModel _vm;

        public CustomerDashboardPage(CustomerDashboardViewModel viewModel)
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
