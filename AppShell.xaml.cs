using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MilkWala.Services.Identity;
using MilkWala.Views.Vendor;

namespace MilkWala
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Pushed from the Customers page (not a tab).
            Routing.RegisterRoute("routes", typeof(RoutesPage));

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
