using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilkWala.Models;
using MilkWala.Services;
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

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public AuthViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Login to MilkWala";
        }

        [RelayCommand]
        public async Task SendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(MobileNumber) || MobileNumber.Length < 10)
            {
                StatusMessage = "Please enter a valid 10-digit mobile number.";
                return;
            }

            IsBusy = true;
            await Task.Delay(400); // Simulate network
            IsOtpSent = true;
            StatusMessage = "OTP sent to " + MobileNumber + " (Use 1234)";
            IsBusy = false;
        }

        [RelayCommand]
        public async Task VerifyOtpAndLoginAsync()
        {
            if (OtpCode != "1234" && !string.IsNullOrEmpty(OtpCode))
            {
                // Accept 1234 or any entered code for demo
            }

            IsBusy = true;
            await DbService.InitAsync();

            // Set current active role preference
            Preferences.Default.Set("ActiveRole", SelectedRole.ToString());
            Preferences.Default.Set("UserMobile", MobileNumber);

            StatusMessage = "Login Successful!";
            IsBusy = false;

            // Navigate to main application dynamic Shell tabs
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new AppShell();
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
