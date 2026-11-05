using Microsoft.Extensions.Logging;
using MilkWala.Services.Data;

namespace MilkWala.Services.Scheduling
{
    /// <summary>Generates the day's deliveries in one transaction on the local database.</summary>
    public sealed class DeliveryScheduleService : IDeliveryScheduleService
    {
        private readonly LocalDbService _db;
        private readonly ILogger<DeliveryScheduleService> _logger;

        public DeliveryScheduleService(LocalDbService db, ILogger<DeliveryScheduleService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> EnsureDeliveriesAsync(DateTime date)
        {
            DeliveryPlan? plan = null;
            await _db.RunInTransactionAsync(connection => plan = DeliveryGenerator.EnsureForDay(connection, date));

            foreach (var warning in plan!.Warnings)
                _logger.LogWarning("Delivery generation for {Date:d}: {Warning}", date, warning);
            if (plan.NewDeliveries.Count > 0)
                _logger.LogInformation("Created {Count} deliveries for {Date:d}", plan.NewDeliveries.Count, date);

            return plan.NewDeliveries.Count;
        }
    }
}
