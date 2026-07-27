using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class ComplaintPage : ContentPage
    {
        private readonly CustomerComplaintsViewModel _vm;

        public ComplaintPage(CustomerComplaintsViewModel viewModel)
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
