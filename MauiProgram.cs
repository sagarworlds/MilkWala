using Microsoft.Extensions.Logging;
using MilkWala.Services;
using MilkWala.ViewModels;
using MilkWala.Views;
using MilkWala.Views.Customer;
using MilkWala.Views.Vendor;
using MilkWala.Views.Delivery;

namespace MilkWala
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // 1. Services
            builder.Services.AddSingleton<LocalDbService>();
            builder.Services.AddSingleton<SyncEngineService>();
            builder.Services.AddSingleton<LocalizationService>();
            builder.Services.AddSingleton<InvoiceService>();
            builder.Services.AddSingleton<DemandForecastService>();
            builder.Services.AddSingleton<BackupService>();
            builder.Services.AddSingleton<MockDataSeeder>();

            // 2. ViewModels
            builder.Services.AddTransient<AuthViewModel>();
            builder.Services.AddTransient<CustomerDashboardViewModel>();
            builder.Services.AddTransient<CustomerSubscriptionViewModel>();
            builder.Services.AddTransient<CustomerCalendarViewModel>();
            builder.Services.AddTransient<CustomerWalletViewModel>();
            builder.Services.AddTransient<CustomerComplaintsViewModel>();
            builder.Services.AddTransient<CustomerStoreViewModel>();
            builder.Services.AddTransient<VendorDashboardViewModel>();
            builder.Services.AddTransient<VendorCustomersViewModel>();
            builder.Services.AddTransient<VendorDeliveriesViewModel>();
            builder.Services.AddTransient<VendorInventoryExpenseViewModel>();
            builder.Services.AddTransient<VendorProcurementViewModel>();
            builder.Services.AddTransient<VendorReportsViewModel>();
            builder.Services.AddTransient<DeliveryBoyViewModel>();

            // 3. Views
            builder.Services.AddTransient<AuthPage>();
            builder.Services.AddTransient<CustomerDashboardPage>();
            builder.Services.AddTransient<SubscriptionPage>();
            builder.Services.AddTransient<DeliveryCalendarPage>();
            builder.Services.AddTransient<WalletPage>();
            builder.Services.AddTransient<ComplaintPage>();
            builder.Services.AddTransient<DairyStorePage>();
            builder.Services.AddTransient<VendorDashboardPage>();
            builder.Services.AddTransient<CustomerManagementPage>();
            builder.Services.AddTransient<DeliveryManagementPage>();
            builder.Services.AddTransient<InventoryExpensePage>();
            builder.Services.AddTransient<ProcurementPage>();
            builder.Services.AddTransient<VendorReportsPage>();
            builder.Services.AddTransient<DeliveryBoyPage>();

            var app = builder.Build();

            // Seed mock data on launch
            Task.Run(async () =>
            {
                var seeder = app.Services.GetRequiredService<MockDataSeeder>();
                await seeder.SeedAsync();
            });

            return app;
        }
    }
}
