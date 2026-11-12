using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Data;
using MilkWala.Services.Identity;

namespace MilkWala.ViewModels
{
    /// <summary>The vendor's routes, delivery staff and which customers are on which route.</summary>
    public partial class VendorRoutesViewModel : BaseViewModel
    {
        // Pickers need a real item for "nobody" / "no route", so these stand in for id 0.
        private static readonly User NoStaff = new() { Id = 0, Name = "— Unassigned —" };
        private static readonly RouteSummary NoRoute = new(0, "— No route —", string.Empty, string.Empty, 0);

        [ObservableProperty]
        private ObservableCollection<RouteSummary> _routes = new();

        [ObservableProperty]
        private ObservableCollection<RouteSummary> _routeChoices = new();

        [ObservableProperty]
        private ObservableCollection<RouteSummary> _moveTargets = new();

        [ObservableProperty]
        private ObservableCollection<User> _staffChoices = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        // --- add route
        [ObservableProperty]
        private string _newRouteName = string.Empty;

        [ObservableProperty]
        private string _newRouteArea = string.Empty;

        [ObservableProperty]
        private User? _newRouteStaff;

        // --- assign staff to a route
        [ObservableProperty]
        private RouteSummary? _assignRoute;

        [ObservableProperty]
        private User? _assignStaff;

        // --- add delivery staff
        [ObservableProperty]
        private string _newStaffName = string.Empty;

        [ObservableProperty]
        private string _newStaffMobile = string.Empty;

        // --- move a customer
        [ObservableProperty]
        private Customer? _moveCustomer;

        [ObservableProperty]
        private RouteSummary? _moveTarget;

        public VendorRoutesViewModel(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Routes & Delivery Staff";
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var routes = await DbService.GetAllAsync<DeliveryRoute>();
                var users = await DbService.GetAllAsync<User>();
                var customers = await DbService.GetAllAsync<Customer>();

                var summaries = RouteSummaries.Build(routes, users, customers);
                var staff = users.Where(u => u.Role == UserRole.DeliveryBoy && u.IsActive).OrderBy(u => u.Name);

                // Keep what the vendor had picked (by id), because every reload creates new objects.
                int? assignRouteId = AssignRoute?.Id, moveCustomerId = MoveCustomer?.Id, moveTargetId = MoveTarget?.Id;
                int? assignStaffId = AssignStaff?.Id, newRouteStaffId = NewRouteStaff?.Id;

                Replace(Routes, summaries);
                Replace(RouteChoices, summaries);
                Replace(MoveTargets, summaries.Prepend(NoRoute));
                Replace(StaffChoices, staff.Prepend(NoStaff));
                Replace(Customers, customers.OrderBy(c => c.Name));

                NewRouteStaff = StaffChoices.FirstOrDefault(u => u.Id == newRouteStaffId) ?? NoStaff;
                AssignStaff = StaffChoices.FirstOrDefault(u => u.Id == assignStaffId) ?? NoStaff;
                AssignRoute = RouteChoices.FirstOrDefault(r => r.Id == assignRouteId) ?? RouteChoices.FirstOrDefault();
                MoveCustomer = Customers.FirstOrDefault(c => c.Id == moveCustomerId) ?? Customers.FirstOrDefault();
                MoveTarget = MoveTargets.FirstOrDefault(r => r.Id == moveTargetId) ?? MoveTargets.FirstOrDefault();
            });
        }

        [RelayCommand]
        public Task AddRouteAsync()
        {
            var name = NewRouteName;
            var area = NewRouteArea;
            var staffId = NewRouteStaff?.Id ?? 0;
            return RunRuleAsync(
                connection => RouteLedger.AddRoute(connection, name, area, staffId),
                () =>
                {
                    StatusMessage = $"Route \"{name.Trim()}\" added.";
                    NewRouteName = string.Empty;
                    NewRouteArea = string.Empty;
                });
        }

        [RelayCommand]
        public Task AssignStaffAsync()
        {
            if (AssignRoute == null)
            {
                ErrorMessage = "Pick a route first.";
                return Task.CompletedTask;
            }

            var route = AssignRoute;
            var staff = AssignStaff ?? NoStaff;
            return RunRuleAsync(
                connection => RouteLedger.AssignStaff(connection, route.Id, staff.Id),
                () => StatusMessage = staff.Id == 0
                    ? $"\"{route.RouteName}\" now has nobody assigned."
                    : $"{staff.Name} now runs \"{route.RouteName}\".");
        }

        [RelayCommand]
        public Task AddStaffAsync()
        {
            var name = NewStaffName;
            var mobile = NewStaffMobile;
            return RunRuleAsync(
                connection => StaffLedger.AddDeliveryStaff(connection, name, mobile),
                () =>
                {
                    StatusMessage = $"{name.Trim()} added. They sign in with their mobile number as delivery staff.";
                    NewStaffName = string.Empty;
                    NewStaffMobile = string.Empty;
                });
        }

        [RelayCommand]
        public Task MoveCustomerAsync()
        {
            if (MoveCustomer == null || MoveTarget == null)
            {
                ErrorMessage = "Pick a customer and a route.";
                return Task.CompletedTask;
            }

            var customer = MoveCustomer;
            var target = MoveTarget;
            return RunRuleAsync(
                connection => RouteLedger.MoveCustomer(connection, customer.Id, target.Id),
                () => StatusMessage = target.Id == 0
                    ? $"{customer.Name} is no longer on any route."
                    : $"{customer.Name} moved to \"{target.RouteName}\".");
        }

        [RelayCommand]
        public Task DeleteRouteAsync(RouteSummary route)
        {
            if (route == null)
                return Task.CompletedTask;

            return RunRuleAsync(
                connection => RouteLedger.DeleteRoute(connection, route.Id),
                () => StatusMessage = $"Route \"{route.RouteName}\" deleted.");
        }

        /// <summary>
        /// Runs a rule-checked change in one transaction. A broken rule is shown to the vendor as an error;
        /// on success the message is set, then the lists reload so every picker shows the new state.
        /// </summary>
        private async Task RunRuleAsync(Action<SQLite.SQLiteConnection> change, Action onSuccess)
        {
            await RunSafeAsync(async () =>
            {
                try
                {
                    await DbService.RunInTransactionAsync(change);
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                onSuccess();
                await LoadAsync();
            });
        }

        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items)
                target.Add(item);
        }
    }
}
