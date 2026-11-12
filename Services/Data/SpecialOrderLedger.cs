using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>
    /// One-off extra milk for a day (guests, a festival). Orders are accepted straight away: delivery generation
    /// turns each into its own delivery for that day and the demand forecast counts it. Run inside a transaction.
    /// </summary>
    public static class SpecialOrderLedger
    {
        public const double MinLitres = 0.5;
        public const double MaxLitres = 10;

        /// <summary>How far ahead an extra order can be placed.</summary>
        public const int MaxDaysAhead = 30;

        /// <summary>Requests extra milk for a future day.</summary>
        /// <param name="today">The current local date (passed in so the rule can be tested).</param>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        /// <exception cref="BusinessRuleException">The day is not tomorrow-or-later within the window, the quantity is out of range, or that day already has an extra order for the milk type.</exception>
        public static SpecialOrder Request(
            SQLiteConnection connection,
            int customerId,
            MilkType milkType,
            double litres,
            DateTime targetDate,
            string? instruction,
            DateTime today)
        {
            var day = targetDate.Date;
            var firstDay = today.Date.AddDays(1);
            var lastDay = today.Date.AddDays(MaxDaysAhead);

            if (day < firstDay)
                throw new BusinessRuleException("Extra milk must be ordered at least a day ahead. Pick tomorrow or a later date.");
            if (day > lastDay)
                throw new BusinessRuleException($"Extra milk can be ordered up to {MaxDaysAhead} days ahead.");
            if (litres < MinLitres || litres > MaxLitres)
                throw new BusinessRuleException($"Extra quantity must be between {MinLitres:0.0} and {MaxLitres:0} litres.");

            var customer = connection.Find<Customer>(customerId)
                ?? throw new RecordNotFoundException(nameof(Customer), customerId);

            bool duplicate = connection.Table<SpecialOrder>().ToList().Any(o =>
                o.CustomerId == customerId && o.TargetDate.Date == day && o.MilkType == milkType && o.IsApproved);
            if (duplicate)
                throw new BusinessRuleException($"You already have extra {milkType} milk on {day:dd MMM}. Cancel it first to change it.");

            var order = new SpecialOrder
            {
                CustomerId = customer.Id,
                MilkType = milkType,
                ExtraQuantityLitres = litres,
                TargetDate = day,
                SpecialInstruction = (instruction ?? string.Empty).Trim(),
                IsApproved = true
            };
            connection.Insert(order);
            SyncQueueWriter.Enqueue(connection, order, SyncOperation.Insert);
            return order;
        }

        /// <summary>Cancels a customer's own extra order, up to the day before it is due.</summary>
        /// <exception cref="RecordNotFoundException">The order does not exist.</exception>
        /// <exception cref="BusinessRuleException">The order belongs to someone else, or its day has started.</exception>
        public static void Cancel(SQLiteConnection connection, int orderId, int customerId, DateTime today)
        {
            var order = connection.Find<SpecialOrder>(orderId)
                ?? throw new RecordNotFoundException(nameof(SpecialOrder), orderId);

            if (order.CustomerId != customerId)
                throw new BusinessRuleException("That extra order belongs to another customer.");
            if (order.TargetDate.Date <= today.Date)
                throw new BusinessRuleException("This extra order is for today or has passed, so it can no longer be cancelled. Please contact your vendor.");

            connection.Delete(order);
            SyncQueueWriter.Enqueue(connection, order, SyncOperation.Delete);
        }
    }
}
