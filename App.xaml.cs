using Microsoft.Maui.Controls;
using MilkWala.Views;

namespace MilkWala
{
    public partial class App : Application
    {
        private readonly AuthPage _authPage;

        public App(AuthPage authPage)
        {
            InitializeComponent();
            _authPage = authPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_authPage);
        }
    }
}