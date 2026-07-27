using Microsoft.Maui.Controls;
using MilkWala.ViewModels;

namespace MilkWala.Views
{
    public partial class AuthPage : ContentPage
    {
        public AuthPage(AuthViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
