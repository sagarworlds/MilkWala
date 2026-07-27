using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class SubscriptionPage : ContentPage
    {
        private readonly CustomerSubscriptionViewModel _vm;

        public SubscriptionPage(CustomerSubscriptionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadSubscriptionAsync();
        }
    }
}
