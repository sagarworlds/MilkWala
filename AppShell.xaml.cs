using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MilkWala
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            string role = Preferences.Default.Get("ActiveRole", "Customer");

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
