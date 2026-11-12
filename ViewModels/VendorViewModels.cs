using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Data;
using MilkWala.Services.Scheduling;
using MilkWala.Services.Reporting;

namespace MilkWala.ViewModels
{
    public partial class VendorDashboardViewModel : BaseViewModel
    {
        private readonly IDeliveryScheduleService _deliveryScheduler;

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
                ILoggerFactory loggerFactory,
                IDeliveryScheduleService deliveryScheduler)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _deliveryScheduler = deliveryScheduler;
            Title = "Vendor Control Center";
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                await _deliveryScheduler.EnsureDeliveriesAsync(DateTime.Today);

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
        private readonly IStatementService _statements;

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
        private DeliveryRoute? _selectedRoute;

        [ObservableProperty]
        private ObservableCollection<DeliveryRoute> _routes = new();

        public ObservableCollection<MilkType> AvailableMilkTypes { get; } = new()
        {
            MilkType.Cow, MilkType.Buffalo, MilkType.Toned, MilkType.FullCream, MilkType.Organic
        };


        public VendorCustomersViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                IStatementService statements)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _statements = statements;
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

                var routeList = await DbService.GetAllAsync<DeliveryRoute>();
                var selectedRouteId = SelectedRoute?.Id;
                Routes.Clear();
                foreach (var route in routeList.OrderBy(r => r.SequenceOrder))
                {
                    Routes.Add(route);
                }
                SelectedRoute = Routes.FirstOrDefault(r => r.Id == selectedRouteId) ?? Routes.FirstOrDefault();
            });
        }

        [RelayCommand]
        public async Task AddCustomerAsync()
        {
            await RunSafeAsync(async () =>
            {
                // New customers start tomorrow so today's delivery run is not changed halfway through.
                var request = new NewCustomerRequest(
                    NewCustomerName,
                    NewCustomerMobile,
                    NewCustomerAddress,
                    NewMilkType,
                    NewQuantity,
                    SelectedRoute?.Id ?? 0,
                    DateTime.Today.AddDays(1));

                Customer? customer = null;
                try
                {
                    // Customer and subscription are saved together (and the mobile checked for duplicates) or not at all.
                    await DbService.RunInTransactionAsync(connection =>
                        customer = CustomerOnboarding.Add(connection, request));
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                NewCustomerName = string.Empty;
                NewCustomerMobile = string.Empty;
                NewCustomerAddress = string.Empty;
                StatusMessage = $"{customer!.Name} added. Deliveries start {request.StartDate:dd MMM}; they can log in with {customer.Mobile}.";

                await LoadCustomersAsync();
            });
        }

        [RelayCommand]
        public async Task ShareStatementAsync(Customer customer)
        {
            await RunSafeAsync(async () =>
            {
                if (customer == null) return;

                var month = DateTime.Today;
                var deliveries = await DbService.GetCustomerDeliveriesAsync(customer.Id, month);

                bool opened;
                try
                {
                    opened = await _statements.ShareOnWhatsAppAsync(customer, deliveries, month);
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                if (opened)
                    StatusMessage = $"{month:MMMM} statement for {customer.Name} opened in WhatsApp.";
                else
                    ErrorMessage = "WhatsApp could not be opened on this device. Is it installed?";
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
        private readonly IDeliveryScheduleService _deliveryScheduler;

        [ObservableProperty]
        private ObservableCollection<Delivery> _deliveries = new();


        public VendorDeliveriesViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                IDeliveryScheduleService deliveryScheduler)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _deliveryScheduler = deliveryScheduler;
            Title = "Delivery Management";
        }

        [RelayCommand]
        public async Task LoadDeliveriesAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                await _deliveryScheduler.EnsureDeliveriesAsync(DateTime.Today);
                var list = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                Deliveries.Clear();
                foreach (var d in list)
                {
                    Deliveries.Add(d);
                }
            });
        }

        [RelayCommand]
        public async Task MarkDeliveredAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;
                StatusMessage = await ApplyStatusAsync(delivery, DeliveryStatus.Delivered);
                await LoadDeliveriesAsync();
            });
        }

        [RelayCommand]
        public async Task MarkSkippedAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;
                StatusMessage = await ApplyStatusAsync(delivery, DeliveryStatus.Skipped);
                await LoadDeliveriesAsync();
            });
        }

        /// <summary>Changes the status and bills (or un-bills) the customer in one transaction; returns the message to show.</summary>
        private async Task<string> ApplyStatusAsync(Delivery delivery, DeliveryStatus status)
        {
            DeliveryChange? change = null;
            await DbService.RunInTransactionAsync(connection =>
                change = DeliveryLedger.ChangeStatus(connection, delivery.Id, status, DateTime.Now));
            return change!.Summary();
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
        private readonly IDemandForecastService _demandForecast;

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
        private ObservableCollection<DemandForecastResult> _tomorrowDemand = new();

        [ObservableProperty]
        private double _tomorrowTotalLitres;

        [ObservableProperty]
        private string _tomorrowLabel = string.Empty;

        [ObservableProperty]
        private double _totalLitresSold;

        public VendorReportsViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                IDemandForecastService demandForecast)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _demandForecast = demandForecast;
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

            // What to have ready tomorrow: the same rules as delivery generation, so it matches the delivery list.
            var forecast = await _demandForecast.ForecastTomorrowDemandAsync();
            TomorrowLabel = $"Milk needed tomorrow ({DateTime.Today.AddDays(1):ddd dd MMM})";
            TomorrowDemand.Clear();
            foreach (var row in forecast.Where(r => r.SubscribedLitres > 0 || r.SpecialOrderExtraLitres > 0))
            {
                TomorrowDemand.Add(row);
            }
            TomorrowTotalLitres = forecast.Sum(r => r.PredictedRequiredLitres);
            });
        }
    }
}
