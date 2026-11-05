using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MilkWala.Services.Identity;

namespace MilkWala
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            string role = Preferences.Default.Get(SessionKeys.ActiveRole, "Customer");

            if (role == "Vendor")
            {
                CurrentItem = VendorTabBar;
            }
            else if (role == "DeliveryBoy")
            {
                CurrentItem = DeliveryTabBar;
            }
            else
            {
                CurrentItem = CustomerTabBar;
            }
        }
    }
}
