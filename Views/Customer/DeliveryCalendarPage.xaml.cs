using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class DeliveryCalendarPage : ContentPage
    {
        private readonly CustomerCalendarViewModel _vm;

        public DeliveryCalendarPage(CustomerCalendarViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadCalendarAsync();
        }
    }
}
