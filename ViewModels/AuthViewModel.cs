using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Identity;
using Microsoft.Maui.Controls;

namespace MilkWala.ViewModels
{
    public partial class AuthViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _mobileNumber = "9876543210";

        [ObservableProperty]
        private string _otpCode = "1234";

        [ObservableProperty]
        private bool _isOtpSent = false;

        [ObservableProperty]
        private UserRole _selectedRole = UserRole.Customer;


        private readonly MockDataSeeder _seeder;

        public AuthViewModel(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory,
            MockDataSeeder seeder)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _seeder = seeder;
            Title = "Login to MilkWala";
        }

        [RelayCommand]
        public async Task SendOtpAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(MobileNumber) || MobileNumber.Length < 10)
                {
                    StatusMessage = "Please enter a valid 10-digit mobile number.";
                    return;
                }

                await Task.Delay(400); // Simulate network
                IsOtpSent = true;
                StatusMessage = "OTP sent to " + MobileNumber + " (Use 1234)";
            });
        }

        [RelayCommand]
        public async Task VerifyOtpAndLoginAsync()
        {
            if (OtpCode != "1234" && !string.IsNullOrEmpty(OtpCode))
            {
                // Accept 1234 or any entered code for demo
            }

            IsBusy = true;
            try
            {
                // Seeding runs in the background from app start; wait for it so the first page never reads an empty database.
                await _seeder.EnsureSeededAsync();

                // Set current active role preference
                Preferences.Default.Set(SessionKeys.ActiveRole, SelectedRole.ToString());
                Preferences.Default.Set(SessionKeys.UserMobile, MobileNumber);

                StatusMessage = "Login Successful!";

                // Navigate to main application dynamic Shell tabs
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Login failed while preparing local data for role {Role}", SelectedRole);
                StatusMessage = "Could not prepare local data. Please try again or restart the app.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SelectRole(string roleName)
        {
            if (Enum.TryParse<UserRole>(roleName, out var role))
            {
                SelectedRole = role;
                if (role == UserRole.Customer) MobileNumber = "9876543210";
                else if (role == UserRole.Vendor) MobileNumber = "9988776655";
                else if (role == UserRole.DeliveryBoy) MobileNumber = "9123456789";
            }
        }
    }
}
