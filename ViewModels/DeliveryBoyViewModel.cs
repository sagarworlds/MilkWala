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
using MilkWala.Services.Identity;
using MilkWala.Services.Scheduling;

namespace MilkWala.ViewModels
{
    public partial class DeliveryBoyViewModel : BaseViewModel
    {
        private readonly IDeliveryScheduleService _deliveryScheduler;
        private readonly ICurrentUserService _currentUser;

        [ObservableProperty]
        private string _assignedRouteName = "Loading route...";

        [ObservableProperty]
        private int _totalPendingDeliveries;

        [ObservableProperty]
        private int _completedDeliveries;

        [ObservableProperty]
        private ObservableCollection<Delivery> _routeDeliveries = new();


        public DeliveryBoyViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                IDeliveryScheduleService deliveryScheduler,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _deliveryScheduler = deliveryScheduler;
            _currentUser = currentUser;
            Title = "Delivery Staff Module";
        }

        [RelayCommand]
        public async Task LoadRouteDeliveriesAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var user = await _currentUser.GetCurrentUserAsync();
                if (user == null || user.Role != UserRole.DeliveryBoy)
                {
                    ShowNothing("No delivery staff account was found for the signed-in mobile number.", "No route");
                    return;
                }

                var routes = RouteAssignment.RoutesFor(user.Id, await DbService.GetAllAsync<DeliveryRoute>());
                if (routes.Count == 0)
                {
                    StatusMessage = "No route is assigned to you yet. Ask the vendor to assign one.";
                    ShowNothing(null, "No route assigned");
                    return;
                }

                await _deliveryScheduler.EnsureDeliveriesAsync(DateTime.Today);

                // Only this person's own run, not every delivery in the dairy.
                var todays = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                var customers = await DbService.GetAllAsync<Customer>();
                var mine = RouteAssignment.DeliveriesOnRoutes(todays, customers, routes);

                AssignedRouteName = string.Join(" + ", routes.Select(r => r.RouteName));
                RouteDeliveries.Clear();
                foreach (var d in mine)
                {
                    RouteDeliveries.Add(d);
                }

                CompletedDeliveries = mine.Count(d => d.Status == DeliveryStatus.Delivered || d.Status == DeliveryStatus.Skipped || d.Status == DeliveryStatus.Extra);
                TotalPendingDeliveries = mine.Count(d => d.Status == DeliveryStatus.Pending);
            });
        }

        /// <summary>Empties the list and counters, optionally with an error, so a stale run is never left on screen.</summary>
        private void ShowNothing(string? error, string routeLabel)
        {
            if (error != null)
                ErrorMessage = error;
            AssignedRouteName = routeLabel;
            RouteDeliveries.Clear();
            CompletedDeliveries = 0;
            TotalPendingDeliveries = 0;
        }

        [RelayCommand]
        public async Task MarkDeliveredAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;

                StatusMessage = await ApplyStatusAsync(delivery, DeliveryStatus.Delivered);
                await LoadRouteDeliveriesAsync();
            });
        }

        [RelayCommand]
        public async Task MarkSkippedAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;

                StatusMessage = await ApplyStatusAsync(delivery, DeliveryStatus.Skipped);
                await LoadRouteDeliveriesAsync();
            });
        }

        /// <summary>Changes the status and bills the customer in one transaction; returns the message to show.</summary>
        private async Task<string> ApplyStatusAsync(Delivery delivery, DeliveryStatus status)
        {
            DeliveryChange? change = null;
            await DbService.RunInTransactionAsync(connection =>
                change = DeliveryLedger.ChangeStatus(connection, delivery.Id, status, DateTime.Now));
            return change!.Summary();
        }
    }
}
