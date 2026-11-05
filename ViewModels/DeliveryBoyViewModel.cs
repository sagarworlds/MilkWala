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

namespace MilkWala.ViewModels
{
    public partial class DeliveryBoyViewModel : BaseViewModel
    {
        private readonly IDeliveryScheduleService _deliveryScheduler;

        [ObservableProperty]
        private string _assignedRouteName = "Route A - Model Town & Green Park";

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
                IDeliveryScheduleService deliveryScheduler)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            _deliveryScheduler = deliveryScheduler;
            Title = "Delivery Staff Module";
        }

        [RelayCommand]
        public async Task LoadRouteDeliveriesAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();
                await _deliveryScheduler.EnsureDeliveriesAsync(DateTime.Today);

                var deliveries = await DbService.GetTodayDeliveriesAsync(DateTime.Today);
                RouteDeliveries.Clear();
                foreach (var d in deliveries)
                {
                    RouteDeliveries.Add(d);
                }

                CompletedDeliveries = deliveries.Count(d => d.Status == DeliveryStatus.Delivered || d.Status == DeliveryStatus.Skipped || d.Status == DeliveryStatus.Extra);
                TotalPendingDeliveries = deliveries.Count(d => d.Status == DeliveryStatus.Pending);
            });
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
