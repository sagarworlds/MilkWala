using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Reporting;

namespace MilkWala.ViewModels
{
    public partial class VendorDashboardViewModel : BaseViewModel
    {
        [ObservableProperty]
        private int _totalCustomers;

        [ObservableProperty]
        private int _todaysTotalDeliveries;

        [ObservableProperty]
        private int _pendingDeliveriesCount;

        [ObservableProperty]
        private decimal _todayCollection;

        [ObservableProperty]
        private decimal _monthlyRevenue;

        [ObservableProperty]
        private decimal _totalOutstandingAmount;

        [ObservableProperty]
        private ObservableCollection<Delivery> _pendingDeliveriesList = new();

        public VendorDashboardViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Vendor Control Center";
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var customers = await DbService.GetAllAsync<Customer>();
                TotalCustomers = customers.Count;
                TotalOutstandingAmount = customers.Sum(c => c.OutstandingBalance);

                var todayDeliveries = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                TodaysTotalDeliveries = todayDeliveries.Count;

                var pending = todayDeliveries.Where(d => d.Status == DeliveryStatus.Pending).ToList();
                PendingDeliveriesCount = pending.Count;

                PendingDeliveriesList.Clear();
                foreach (var p in pending)
                {
                    PendingDeliveriesList.Add(p);
                }

                var payments = await DbService.GetAllAsync<Payment>();
                TodayCollection = payments.Where(p => p.PaymentDate.Date == DateTime.Today).Sum(p => p.Amount);

                // Revenue for the whole current month, not just today's deliveries.
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthDeliveries = await DbService.GetDeliveriesBetweenAsync(monthStart, monthStart.AddMonths(1));
                MonthlyRevenue = DeliveryStats.Revenue(monthDeliveries);
            });
        }
    }

    public partial class VendorCustomersViewModel : BaseViewModel
    {
        [ObservableProperty]
        private ObservableCollection<Customer> _customerList = new();

        [ObservableProperty]
        private string _newCustomerName = string.Empty;

        [ObservableProperty]
        private string _newCustomerMobile = string.Empty;

        [ObservableProperty]
        private string _newCustomerAddress = string.Empty;

        [ObservableProperty]
        private MilkType _newMilkType = MilkType.Cow;

        [ObservableProperty]
        private double _newQuantity = 1.0;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public VendorCustomersViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Customer Management";
        }

        [RelayCommand]
        public async Task LoadCustomersAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var list = await DbService.GetAllAsync<Customer>();
                CustomerList.Clear();
                foreach (var c in list)
                {
                    CustomerList.Add(c);
                }
            });
        }

        [RelayCommand]
        public async Task AddCustomerAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerMobile))
                {
                    StatusMessage = "Name and Mobile number are required.";
                    return;
                }

                await DbService.InitAsync();

                var customer = new Customer
                {
                    Name = NewCustomerName,
                    Mobile = NewCustomerMobile,
                    Address = NewCustomerAddress,
                    PreferredMilkType = NewMilkType,
                    PreferredQuantity = NewQuantity,
                    RouteId = 1,
                    WalletBalance = 500.00m,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                };

                await DbService.InsertWithSyncAsync(customer);

                NewCustomerName = string.Empty;
                NewCustomerMobile = string.Empty;
                NewCustomerAddress = string.Empty;
                StatusMessage = "Customer added successfully!";

                await LoadCustomersAsync();
            });
        }

        [RelayCommand]
        public async Task ToggleStatusAsync(Customer customer)
        {
            await RunSafeAsync(async () =>
            {
                if (customer == null) return;
                customer.IsActive = !customer.IsActive;
                customer.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(customer);
                await LoadCustomersAsync();
            });
        }
    }

    public partial class VendorDeliveriesViewModel : BaseViewModel
    {
        [ObservableProperty]
        private ObservableCollection<Delivery> _deliveries = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public VendorDeliveriesViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Delivery Management";
        }

        [RelayCommand]
        public async Task LoadDeliveriesAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var list = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                Deliveries.Clear();
                foreach (var d in list)
                {
                    Deliveries.Add(d);
                }
            });
        }

        [RelayCommand]
        public async Task UpdateDeliveryStatusAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;

                await DbService.InitAsync();

                // Only a completed delivery has a delivery time; skipping must not stamp one.
                if (delivery.Status == DeliveryStatus.Delivered)
                    delivery.DeliveredAt = DateTime.Now;
                delivery.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(delivery);

                StatusMessage = $"Updated status for {delivery.CustomerName} to {delivery.Status}.";
                await LoadDeliveriesAsync();
            });
        }

        [RelayCommand]
        public async Task MarkDeliveredAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;
                delivery.Status = DeliveryStatus.Delivered;
                await UpdateDeliveryStatusAsync(delivery);
            });
        }

        [RelayCommand]
        public async Task MarkSkippedAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;
                delivery.Status = DeliveryStatus.Skipped;
                await UpdateDeliveryStatusAsync(delivery);
            });
        }
    }

    public partial class VendorInventoryExpenseViewModel : BaseViewModel
    {
        [ObservableProperty]
        private decimal _morningStock = 350.0m;

        [ObservableProperty]
        private decimal _eveningStock = 200.0m;

        [ObservableProperty]
        private decimal _distributedStock;

        [ObservableProperty]
        private decimal _remainingStock;

        [ObservableProperty]
        private ExpenseCategory _selectedCategory = ExpenseCategory.Fuel;

        [ObservableProperty]
        private decimal _expenseAmount;

        [ObservableProperty]
        private string _expenseDescription = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Expense> _expenses = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<ExpenseCategory> Categories { get; } = new()
        {
            ExpenseCategory.Fuel, ExpenseCategory.Salary, ExpenseCategory.Maintenance, ExpenseCategory.Packaging, ExpenseCategory.Miscellaneous
        };

        public VendorInventoryExpenseViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Inventory & Expenses";
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var todayDeliveries = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                DistributedStock = (decimal)DeliveryStats.Litres(todayDeliveries);

                var vendors = await DbService.GetAllAsync<Vendor>();
                var v = vendors.FirstOrDefault();
                if (v != null)
                {
                    MorningStock = v.TotalMorningStockLitres;
                    EveningStock = v.TotalEveningStockLitres;
                }

                RemainingStock = (MorningStock + EveningStock) - DistributedStock;

                var list = await DbService.GetAllAsync<Expense>();
                Expenses.Clear();
                foreach (var e in list.OrderByDescending(x => x.Date))
                {
                    Expenses.Add(e);
                }
            });
        }

        [RelayCommand]
        public async Task AddExpenseAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (ExpenseAmount <= 0) return;

                await DbService.InitAsync();

                var exp = new Expense
                {
                    Category = SelectedCategory,
                    Amount = ExpenseAmount,
                    Description = string.IsNullOrWhiteSpace(ExpenseDescription) ? SelectedCategory.ToString() : ExpenseDescription,
                    Date = DateTime.Now
                };

                await DbService.InsertWithSyncAsync(exp);

                ExpenseAmount = 0;
                ExpenseDescription = string.Empty;
                StatusMessage = "Expense logged successfully!";

                await LoadDataAsync();
            });
        }
    }

    public partial class VendorReportsViewModel : BaseViewModel
    {
        [ObservableProperty]
        private decimal _totalSales;

        [ObservableProperty]
        private decimal _totalCollection;

        [ObservableProperty]
        private decimal _totalExpenses;

        [ObservableProperty]
        private decimal _totalProcurementCost;

        [ObservableProperty]
        private decimal _netProfit;

        [ObservableProperty]
        private string _reportMonthLabel = string.Empty;

        [ObservableProperty]
        private double _totalLitresSold;

        public VendorReportsViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Reports & Analytics";
        }

        [RelayCommand]
        public async Task LoadReportsAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var month = DateTime.Today;
                ReportMonthLabel = $"Business Summary - {month:MMMM yyyy}";

                var monthStart = new DateTime(month.Year, month.Month, 1);
                var deliveries = await DbService.GetDeliveriesBetweenAsync(monthStart, monthStart.AddMonths(1));
                var payments = await DbService.GetAllAsync<Payment>();
                var expenses = await DbService.GetAllAsync<Expense>();
                var procurements = await DbService.GetAllAsync<MilkProcurement>();

                var report = MonthlyReportCalculator.Calculate(month, deliveries, payments, expenses, procurements);
                TotalSales = report.TotalSales;
                TotalLitresSold = report.TotalLitresSold;
                TotalCollection = report.TotalCollection;
                TotalExpenses = report.TotalExpenses;
                TotalProcurementCost = report.TotalProcurementCost;
                NetProfit = report.NetProfit;
            });
        }
    }
}
