using MilkWala.Models;
using MilkWala.Services.Scheduling;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Creates a day's deliveries from subscriptions. Run inside a transaction.</summary>
    public static class DeliveryGenerator
    {
        /// <summary>
        /// Makes sure the day has a delivery for every due subscription. Existing rows are read inside the same
        /// transaction as the inserts, so calling this repeatedly or from two screens at once never creates duplicates.
        /// </summary>
        /// <returns>The plan that was applied: the rows created and any warnings (e.g. a milk type without a price).</returns>
        public static DeliveryPlan EnsureForDay(SQLiteConnection connection, DateTime day)
        {
            var start = day.Date;
            var end = start.AddDays(1);

            var plan = DeliveryPlanner.Plan(
                start,
                connection.Table<Subscription>().ToList(),
                connection.Table<Customer>().ToList(),
                connection.Table<Product>().ToList(),
                connection.Table<Delivery>().Where(d => d.DeliveryDate >= start && d.DeliveryDate < end).ToList());

            foreach (var delivery in plan.NewDeliveries)
            {
                connection.Insert(delivery);
                SyncQueueWriter.Enqueue(connection, delivery, SyncOperation.Insert);
            }

            return plan;
        }
    }
}
