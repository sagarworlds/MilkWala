using MilkWala.Helpers;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Farmer onboarding and payouts. Run inside a transaction.</summary>
    public static class SupplierLedger
    {
        /// <summary>Result of a payout.</summary>
        /// <param name="Supplier">The farmer after the payment.</param>
        /// <param name="PurchasesSettled">How many purchases are now fully paid because of this payment.</param>
        public sealed record PayoutResult(FarmerSupplier Supplier, int PurchasesSettled);

        /// <summary>Registers a farmer.</summary>
        /// <exception cref="BusinessRuleException">A required field is missing/invalid or the mobile is already registered.</exception>
        public static FarmerSupplier Add(
            SQLiteConnection connection,
            string name,
            string mobile,
            MilkType milkType,
            double defaultFatPercent,
            decimal defaultRatePerLitre)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new BusinessRuleException("Enter the farmer's name.");

            var normalizedMobile = MobileNumber.Normalize(mobile);
            if (normalizedMobile.Length == 0)
                throw new BusinessRuleException("Enter a valid 10-digit mobile number.");
            if (defaultFatPercent <= 0 || defaultFatPercent > 15)
                throw new BusinessRuleException("Fat % must be between 0 and 15.");
            if (defaultRatePerLitre <= 0)
                throw new BusinessRuleException("Enter a rate per litre greater than zero.");

            var duplicate = connection.Table<FarmerSupplier>().ToList()
                .FirstOrDefault(s => MobileNumber.Normalize(s.Mobile) == normalizedMobile);
            if (duplicate != null)
                throw new BusinessRuleException($"This mobile number is already registered to {duplicate.Name}.");

            var supplier = new FarmerSupplier
            {
                Name = name,
                Mobile = normalizedMobile,
                MilkType = milkType,
                DefaultFatPercent = defaultFatPercent,
                DefaultRatePerLitre = defaultRatePerLitre
            };
            connection.Insert(supplier);
            SyncQueueWriter.Enqueue(connection, supplier, SyncOperation.Insert);
            return supplier;
        }

        /// <summary>
        /// Pays the farmer: lowers the outstanding payout and marks purchases as paid, oldest first
        /// (a purchase is only "Paid" once its full amount is covered).
        /// </summary>
        /// <exception cref="RecordNotFoundException">The farmer does not exist.</exception>
        /// <exception cref="BusinessRuleException">The amount is not positive or exceeds what is owed.</exception>
        public static PayoutResult Pay(SQLiteConnection connection, int supplierId, decimal amount)
        {
            if (amount <= 0)
                throw new BusinessRuleException("Enter a payment amount greater than zero.");

            var supplier = connection.Find<FarmerSupplier>(supplierId)
                ?? throw new RecordNotFoundException(nameof(FarmerSupplier), supplierId);

            if (amount > supplier.OutstandingPayout)
                throw new BusinessRuleException($"Only ₹{supplier.OutstandingPayout:N2} is owed to {supplier.Name}.");

            decimal remaining = amount;
            int settled = 0;
            var unpaid = connection.Table<MilkProcurement>()
                .Where(p => p.SupplierId == supplierId && p.IsPaid == false)
                .ToList()
                .OrderBy(p => p.Date);

            foreach (var purchase in unpaid)
            {
                if (remaining <= 0)
                    break;

                decimal applied = Math.Min(purchase.TotalAmount - purchase.AmountPaid, remaining);
                purchase.AmountPaid += applied;
                purchase.IsPaid = purchase.AmountPaid >= purchase.TotalAmount;
                remaining -= applied;
                if (purchase.IsPaid)
                    settled++;

                connection.Update(purchase);
                SyncQueueWriter.Enqueue(connection, purchase, SyncOperation.Update);
            }

            // Any amount left over belongs to an opening balance that has no purchase record; it still reduces the debt.
            supplier.OutstandingPayout -= amount;
            connection.Update(supplier);
            SyncQueueWriter.Enqueue(connection, supplier, SyncOperation.Update);

            return new PayoutResult(supplier, settled);
        }
    }
}
