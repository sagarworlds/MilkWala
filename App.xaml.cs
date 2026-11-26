using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using MilkWala.Services;
using MilkWala.Views;

namespace MilkWala
{
    public partial class App : Application
    {
        private readonly AuthPage _authPage;
        private readonly IServiceProvider _services;

        public App(AuthPage authPage, LocalizationService localization, IServiceProvider services)
        {
            InitializeComponent();
            _authPage = authPage;
            _services = services;

            // Text is read when a screen loads, so a language change rebuilds the visible screens.
            localization.LanguageChanged += OnLanguageChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_authPage);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var window = Windows.FirstOrDefault();
                if (window == null)
                    return;

                // Signed in: rebuild the role's shell (it opens on that role's first tab). Signed out: a fresh login page.
                window.Page = window.Page is Shell
                    ? new AppShell()
                    : _services.GetRequiredService<AuthPage>();
            });
        }
    }
}
