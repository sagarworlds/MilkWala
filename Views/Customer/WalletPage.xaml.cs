using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views.Customer
{
    public partial class WalletPage : ContentPage
    {
        private readonly CustomerWalletViewModel _vm;

        public WalletPage(CustomerWalletViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _vm = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadWalletAsync();
        }
    }
}
