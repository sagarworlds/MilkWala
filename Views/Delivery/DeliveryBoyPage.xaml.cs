using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Delivery
{
    public partial class DeliveryBoyPage : ContentPage
    {
        private readonly DeliveryBoyViewModel _vm;

        public DeliveryBoyPage(DeliveryBoyViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadRouteDeliveriesAsync();
        }
    }
}
