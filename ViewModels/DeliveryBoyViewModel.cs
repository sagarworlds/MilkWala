using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;

namespace MilkWala.ViewModels
{
    public partial class DeliveryBoyViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _assignedRouteName = "Route A - Model Town & Green Park";

        [ObservableProperty]
        private int _totalPendingDeliveries;

        [ObservableProperty]
        private int _completedDeliveries;

        [ObservableProperty]
        private ObservableCollection<Delivery> _routeDeliveries = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public DeliveryBoyViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Delivery Staff Module";
        }

        [RelayCommand]
        public async Task LoadRouteDeliveriesAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

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

                delivery.Status = DeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.Now;
                delivery.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(delivery);

                StatusMessage = $"Marked delivered for {delivery.CustomerName} ({delivery.QuantityLitres}L)";
                await LoadRouteDeliveriesAsync();
            });
        }

        [RelayCommand]
        public async Task MarkSkippedAsync(Delivery delivery)
        {
            await RunSafeAsync(async () =>
            {
                if (delivery == null) return;

                delivery.Status = DeliveryStatus.Skipped;
                delivery.UpdatedAt = DateTime.UtcNow;

                await DbService.UpdateWithSyncAsync(delivery);

                StatusMessage = $"Marked skipped for {delivery.CustomerName}";
                await LoadRouteDeliveriesAsync();
            });
        }
    }
}
