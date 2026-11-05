using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Scheduling;
using MilkWala.Services.Data;
using MilkWala.Services.Identity;
using MilkWala.Services.Reporting;

namespace MilkWala.ViewModels
{
    public partial class CustomerDashboardViewModel : CustomerViewModelBase
    {
        private readonly IDeliveryScheduleService _deliveryScheduler;

        [ObservableProperty]
        private Customer? _currentCustomer;

        [ObservableProperty]
        private Delivery? _todaysDelivery;

        [ObservableProperty]
        private string _todaysDeliveryStatusText = "Pending Delivery";

        [ObservableProperty]
        private decimal _walletBalance;

        [ObservableProperty]
        private decimal _pendingDues;

        [ObservableProperty]
        private double _monthlyLitresDelivered;

        [ObservableProperty]
        private decimal _monthlyTotalAmount;

        [ObservableProperty]
        private ObservableCollection<Delivery> _recentDeliveries = new();

        public CustomerDashboardViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser,
                IDeliveryScheduleService deliveryScheduler)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            _deliveryScheduler = deliveryScheduler;
            Title = "Customer Dashboard";
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                await _deliveryScheduler.EnsureDeliveriesAsync(DateTime.Today);
                CurrentCustomer = await RequireCustomerAsync();

                if (CurrentCustomer != null)
                {
                    WalletBalance = CurrentCustomer.WalletBalance;
                    PendingDues = CurrentCustomer.OutstandingBalance;

                    var monthDeliveries = await DbService.GetCustomerDeliveriesAsync(CurrentCustomer.Id, DateTime.Today);
                    TodaysDelivery = monthDeliveries.FirstOrDefault(d => d.DeliveryDate.Date == DateTime.Today);

                    // Always reassign so a previous customer's / day's status never lingers when there is no delivery today.
                    TodaysDeliveryStatusText = TodaysDelivery != null
                        ? $"{TodaysDelivery.QuantityLitres}L {TodaysDelivery.MilkType} - {TodaysDelivery.Status}"
                        : "No delivery scheduled today";

                    MonthlyLitresDelivered = DeliveryStats.Litres(monthDeliveries);
                    MonthlyTotalAmount = DeliveryStats.Revenue(monthDeliveries);

                    // The month list is sorted oldest-first, so take the newest explicitly.
                    RecentDeliveries.Clear();
                    foreach (var del in DeliveryStats.MostRecent(monthDeliveries, 7))
                    {
                        RecentDeliveries.Add(del);
                    }
                }
            });
        }
    }

    public partial class CustomerSubscriptionViewModel : CustomerViewModelBase
    {
        [ObservableProperty]
        private Subscription? _currentSubscription;

        [ObservableProperty]
        private MilkType _selectedMilkType = MilkType.Cow;

        [ObservableProperty]
        private double _quantityLitres = 1.5;

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private DateTime _vacationStartDate = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private DateTime _vacationEndDate = DateTime.Today.AddDays(7);


        public ObservableCollection<MilkType> AvailableMilkTypes { get; } = new()
        {
            MilkType.Cow, MilkType.Buffalo, MilkType.Toned, MilkType.FullCream, MilkType.Organic
        };

        public CustomerSubscriptionViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            Title = "Manage Subscription";
        }

        [RelayCommand]
        public async Task LoadSubscriptionAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                var subs = await DbService.GetAllAsync<Subscription>();
                CurrentSubscription = subs.FirstOrDefault(s => s.CustomerId == customer.Id);
                if (CurrentSubscription == null)
                    StatusMessage = "You do not have an active subscription yet. Please contact your vendor.";

                if (CurrentSubscription != null)
                {
                    SelectedMilkType = CurrentSubscription.MilkType;
                    QuantityLitres = CurrentSubscription.QuantityLitres;
                    IsPaused = CurrentSubscription.IsPaused;
                }
            });
        }

        [RelayCommand]
        public async Task SaveSubscriptionAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (CurrentSubscription == null)
                {
                    ErrorMessage = "No active subscription was found to update.";
                    return;
                }

                CurrentSubscription.MilkType = SelectedMilkType;
                CurrentSubscription.QuantityLitres = QuantityLitres;
                CurrentSubscription.IsPaused = IsPaused;
                CurrentSubscription.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(CurrentSubscription);

                StatusMessage = "Subscription details saved successfully!";
            });
        }

        [RelayCommand]
        public async Task ApplyVacationHoldAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (CurrentSubscription == null)
                {
                    ErrorMessage = "No active subscription was found to update.";
                    return;
                }

                CurrentSubscription.IsPaused = true;
                CurrentSubscription.PauseStartDate = VacationStartDate;
                CurrentSubscription.PauseEndDate = VacationEndDate;
                CurrentSubscription.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(CurrentSubscription);

                IsPaused = true;
                StatusMessage = $"Vacation hold applied from {VacationStartDate:dd MMM} to {VacationEndDate:dd MMM}.";
            });
        }

        [RelayCommand]
        public async Task ResumeDeliveryAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (CurrentSubscription == null)
                {
                    ErrorMessage = "No active subscription was found to update.";
                    return;
                }

                CurrentSubscription.IsPaused = false;
                CurrentSubscription.PauseStartDate = null;
                CurrentSubscription.PauseEndDate = null;
                CurrentSubscription.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(CurrentSubscription);

                IsPaused = false;
                StatusMessage = "Subscription resumed! Daily delivery is active.";
            });
        }
    }

    public partial class CustomerCalendarViewModel : CustomerViewModelBase
    {
        [ObservableProperty]
        private DateTime _selectedMonth = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<Delivery> _calendarDeliveries = new();

        [ObservableProperty]
        private int _totalDeliveredDays;

        [ObservableProperty]
        private int _totalSkippedDays;

        [ObservableProperty]
        private double _totalLitres;

        public CustomerCalendarViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            Title = "Delivery Calendar";
        }

        [RelayCommand]
        public async Task LoadCalendarAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                var list = await DbService.GetCustomerDeliveriesAsync(customer.Id, SelectedMonth);
                CalendarDeliveries.Clear();
                foreach (var d in list)
                {
                    CalendarDeliveries.Add(d);
                }

                TotalDeliveredDays = list.Count(d => DeliveryStats.IsFulfilled(d.Status));
                TotalSkippedDays = list.Count(d => d.Status == DeliveryStatus.Skipped || d.Status == DeliveryStatus.Holiday);
                TotalLitres = DeliveryStats.Litres(list);
            });
        }

        [RelayCommand]
        public async Task PreviousMonthAsync()
        {
            await RunSafeAsync(async () =>
            {
                SelectedMonth = SelectedMonth.AddMonths(-1);
                await LoadCalendarAsync();
            });
        }

        [RelayCommand]
        public async Task NextMonthAsync()
        {
            await RunSafeAsync(async () =>
            {
                SelectedMonth = SelectedMonth.AddMonths(1);
                await LoadCalendarAsync();
            });
        }
    }

    public partial class CustomerWalletViewModel : CustomerViewModelBase
    {
        [ObservableProperty]
        private decimal _balance;

        [ObservableProperty]
        private decimal _rechargeAmount = 500;

        [ObservableProperty]
        private ObservableCollection<WalletTransaction> _transactions = new();


        public CustomerWalletViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            Title = "Wallet & Payments";
        }

        [RelayCommand]
        public async Task LoadWalletAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                Balance = customer.WalletBalance;

                var txs = await DbService.GetAllAsync<WalletTransaction>();
                Transactions.Clear();
                foreach (var tx in txs.Where(t => t.CustomerId == customer.Id).OrderByDescending(t => t.Date))
                {
                    Transactions.Add(tx);
                }
            });
        }

        [RelayCommand]
        public async Task AddMoneyAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (RechargeAmount <= 0) return;

                await DbService.InitAsync();

                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                var amount = RechargeAmount;
                var reference = $"TXN{Random.Shared.Next(1000, 9999)}";

                // Credits the wallet, records the collection for the vendor and pays off any dues, all together.
                WalletLedger.RechargeResult? result = null;
                await DbService.RunInTransactionAsync(connection =>
                {
                    result = WalletLedger.Recharge(
                        connection, customer.Id, amount, PaymentMode.UPI, reference,
                        $"Online Recharge via UPI (Ref: {reference})");
                });

                Balance = result!.BalanceAfter;
                StatusMessage = result.DuesCleared > 0
                    ? $"₹{amount} added. ₹{result.DuesCleared:N2} of your dues were cleared."
                    : $"₹{amount} added to wallet successfully!";
                await LoadWalletAsync();
            });
        }
    }

    public partial class CustomerComplaintsViewModel : CustomerViewModelBase
    {
        [ObservableProperty]
        private ComplaintCategory _selectedCategory = ComplaintCategory.Quality;

        [ObservableProperty]
        private string _subject = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;


        [ObservableProperty]
        private ObservableCollection<Complaint> _complaints = new();

        public ObservableCollection<ComplaintCategory> Categories { get; } = new()
        {
            ComplaintCategory.Quality,
            ComplaintCategory.DeliveryNotReceived,
            ComplaintCategory.WrongQuantity,
            ComplaintCategory.Billing,
            ComplaintCategory.GeneralFeedback
        };

        public CustomerComplaintsViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            Title = "Complaints & Support";
        }

        [RelayCommand]
        public async Task LoadComplaintsAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                var list = await DbService.GetAllAsync<Complaint>();
                Complaints.Clear();
                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                foreach (var c in list.Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.CreatedAt))
                {
                    Complaints.Add(c);
                }
            });
        }

        [RelayCommand]
        public async Task SubmitComplaintAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Description))
                {
                    StatusMessage = "Please enter subject and description.";
                    return;
                }

                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                var complaint = new Complaint
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Category = SelectedCategory,
                    Subject = Subject,
                    Description = Description,
                    Status = ComplaintStatus.Open,
                    CreatedAt = DateTime.Now
                };

                await DbService.InsertWithSyncAsync(complaint);

                Subject = string.Empty;
                Description = string.Empty;
                StatusMessage = "Complaint registered! Reference ID #" + complaint.Id;

                await LoadComplaintsAsync();
            });
        }
    }
}
