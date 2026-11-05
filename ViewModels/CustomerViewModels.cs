using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Reporting;

namespace MilkWala.ViewModels
{
    public partial class CustomerDashboardViewModel : BaseViewModel
    {
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

        public CustomerDashboardViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Customer Dashboard";
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            IsBusy = true;
            await DbService.InitAsync();

            CurrentCustomer = await DbService.GetCustomerByUserIdAsync(1) ?? (await DbService.GetAllAsync<Customer>()).FirstOrDefault();

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

            IsBusy = false;
        }
    }

    public partial class CustomerSubscriptionViewModel : BaseViewModel
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

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<MilkType> AvailableMilkTypes { get; } = new()
        {
            MilkType.Cow, MilkType.Buffalo, MilkType.Toned, MilkType.FullCream, MilkType.Organic
        };

        public CustomerSubscriptionViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Manage Subscription";
        }

        [RelayCommand]
        public async Task LoadSubscriptionAsync()
        {
            await DbService.InitAsync();
            var subs = await DbService.GetAllAsync<Subscription>();
            CurrentSubscription = subs.FirstOrDefault();

            if (CurrentSubscription != null)
            {
                SelectedMilkType = CurrentSubscription.MilkType;
                QuantityLitres = CurrentSubscription.QuantityLitres;
                IsPaused = CurrentSubscription.IsPaused;
            }
        }

        [RelayCommand]
        public async Task SaveSubscriptionAsync()
        {
            if (CurrentSubscription == null) return;

            CurrentSubscription.MilkType = SelectedMilkType;
            CurrentSubscription.QuantityLitres = QuantityLitres;
            CurrentSubscription.IsPaused = IsPaused;
            CurrentSubscription.UpdatedAt = DateTime.UtcNow;

            await DbService.UpdateAsync(CurrentSubscription);
            await DbService.EnqueueSyncAsync("Subscriptions", CurrentSubscription.Id, SyncOperation.Update, CurrentSubscription);

            StatusMessage = "Subscription details saved successfully!";
        }

        [RelayCommand]
        public async Task ApplyVacationHoldAsync()
        {
            if (CurrentSubscription == null) return;

            CurrentSubscription.IsPaused = true;
            CurrentSubscription.PauseStartDate = VacationStartDate;
            CurrentSubscription.PauseEndDate = VacationEndDate;
            CurrentSubscription.UpdatedAt = DateTime.UtcNow;

            await DbService.UpdateAsync(CurrentSubscription);
            await DbService.EnqueueSyncAsync("Subscriptions", CurrentSubscription.Id, SyncOperation.Update, CurrentSubscription);

            IsPaused = true;
            StatusMessage = $"Vacation hold applied from {VacationStartDate:dd MMM} to {VacationEndDate:dd MMM}.";
        }

        [RelayCommand]
        public async Task ResumeDeliveryAsync()
        {
            if (CurrentSubscription == null) return;

            CurrentSubscription.IsPaused = false;
            CurrentSubscription.PauseStartDate = null;
            CurrentSubscription.PauseEndDate = null;
            CurrentSubscription.UpdatedAt = DateTime.UtcNow;

            await DbService.UpdateAsync(CurrentSubscription);
            await DbService.EnqueueSyncAsync("Subscriptions", CurrentSubscription.Id, SyncOperation.Update, CurrentSubscription);

            IsPaused = false;
            StatusMessage = "Subscription resumed! Daily delivery is active.";
        }
    }

    public partial class CustomerCalendarViewModel : BaseViewModel
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

        public CustomerCalendarViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Delivery Calendar";
        }

        [RelayCommand]
        public async Task LoadCalendarAsync()
        {
            IsBusy = true;
            await DbService.InitAsync();

            var list = await DbService.GetCustomerDeliveriesAsync(1, SelectedMonth);
            CalendarDeliveries.Clear();
            foreach (var d in list)
            {
                CalendarDeliveries.Add(d);
            }

            TotalDeliveredDays = list.Count(d => DeliveryStats.IsFulfilled(d.Status));
            TotalSkippedDays = list.Count(d => d.Status == DeliveryStatus.Skipped || d.Status == DeliveryStatus.Holiday);
            TotalLitres = DeliveryStats.Litres(list);

            IsBusy = false;
        }

        [RelayCommand]
        public async Task PreviousMonthAsync()
        {
            SelectedMonth = SelectedMonth.AddMonths(-1);
            await LoadCalendarAsync();
        }

        [RelayCommand]
        public async Task NextMonthAsync()
        {
            SelectedMonth = SelectedMonth.AddMonths(1);
            await LoadCalendarAsync();
        }
    }

    public partial class CustomerWalletViewModel : BaseViewModel
    {
        [ObservableProperty]
        private decimal _balance;

        [ObservableProperty]
        private decimal _rechargeAmount = 500;

        [ObservableProperty]
        private ObservableCollection<WalletTransaction> _transactions = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public CustomerWalletViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Wallet & Payments";
        }

        [RelayCommand]
        public async Task LoadWalletAsync()
        {
            await DbService.InitAsync();
            var customer = await DbService.GetCustomerByUserIdAsync(1);
            if (customer != null)
            {
                Balance = customer.WalletBalance;
            }

            var txs = await DbService.GetAllAsync<WalletTransaction>();
            Transactions.Clear();
            foreach (var tx in txs.Where(t => t.CustomerId == 1).OrderByDescending(t => t.Date))
            {
                Transactions.Add(tx);
            }
        }

        [RelayCommand]
        public async Task AddMoneyAsync()
        {
            if (RechargeAmount <= 0) return;

            IsBusy = true;
            await DbService.InitAsync();

            var customer = await DbService.GetCustomerByUserIdAsync(1);
            if (customer != null)
            {
                customer.WalletBalance += RechargeAmount;
                await DbService.UpdateAsync(customer);
                Balance = customer.WalletBalance;

                var tx = new WalletTransaction
                {
                    CustomerId = 1,
                    Amount = RechargeAmount,
                    TransactionType = "Credit",
                    Description = $"Online Recharge via UPI (Ref: TXN{Random.Shared.Next(1000, 9999)})",
                    Date = DateTime.Now,
                    BalanceAfter = Balance
                };
                await DbService.InsertAsync(tx);
                await DbService.EnqueueSyncAsync("WalletTransactions", tx.Id, SyncOperation.Insert, tx);

                Transactions.Insert(0, tx);
                StatusMessage = $"₹{RechargeAmount} added to wallet successfully!";
            }
            IsBusy = false;
        }
    }

    public partial class CustomerComplaintsViewModel : BaseViewModel
    {
        [ObservableProperty]
        private ComplaintCategory _selectedCategory = ComplaintCategory.Quality;

        [ObservableProperty]
        private string _subject = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

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

        public CustomerComplaintsViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Complaints & Support";
        }

        [RelayCommand]
        public async Task LoadComplaintsAsync()
        {
            await DbService.InitAsync();
            var list = await DbService.GetAllAsync<Complaint>();
            Complaints.Clear();
            foreach (var c in list.Where(x => x.CustomerId == 1).OrderByDescending(x => x.CreatedAt))
            {
                Complaints.Add(c);
            }
        }

        [RelayCommand]
        public async Task SubmitComplaintAsync()
        {
            if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Description))
            {
                StatusMessage = "Please enter subject and description.";
                return;
            }

            IsBusy = true;
            var complaint = new Complaint
            {
                CustomerId = 1,
                CustomerName = "Rahul Sharma",
                Category = SelectedCategory,
                Subject = Subject,
                Description = Description,
                Status = ComplaintStatus.Open,
                CreatedAt = DateTime.UtcNow
            };

            await DbService.InsertAsync(complaint);
            await DbService.EnqueueSyncAsync("Complaints", complaint.Id, SyncOperation.Insert, complaint);

            Subject = string.Empty;
            Description = string.Empty;
            StatusMessage = "Complaint registered! Reference ID #" + complaint.Id;

            await LoadComplaintsAsync();
            IsBusy = false;
        }
    }
}
