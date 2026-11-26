using MilkWala.Localization;
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
                throw new BusinessRuleException(Strings.ErrExtraDayAhead);
            if (day > lastDay)
                throw new BusinessRuleException(string.Format(Strings.FmtErrExtraMaxDays, MaxDaysAhead));
            if (litres < MinLitres || litres > MaxLitres)
                throw new BusinessRuleException(string.Format(Strings.FmtErrExtraQuantityRange, MinLitres, MaxLitres));

            var customer = connection.Find<Customer>(customerId)
                ?? throw new RecordNotFoundException(nameof(Customer), customerId);

            bool duplicate = connection.Table<SpecialOrder>().ToList().Any(o =>
                o.CustomerId == customerId && o.TargetDate.Date == day && o.MilkType == milkType && o.IsApproved);
            if (duplicate)
                throw new BusinessRuleException(string.Format(Strings.FmtErrExtraDuplicate, EnumText.For(milkType), day));

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
                throw new BusinessRuleException(Strings.ErrExtraOtherCustomer);
            if (order.TargetDate.Date <= today.Date)
                throw new BusinessRuleException(Strings.ErrExtraCannotCancel);

            connection.Delete(order);
            SyncQueueWriter.Enqueue(connection, order, SyncOperation.Delete);
        }
    }
}
