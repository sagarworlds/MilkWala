using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Records milk bought from a farmer and the payout owed for it, as one unit of work.</summary>
    public static class ProcurementLedger
    {
        /// <summary>
        /// Inserts the purchase and adds its value to the supplier's outstanding payout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="litres"/> is not positive.</exception>
        /// <exception cref="RecordNotFoundException">The supplier does not exist.</exception>
        public static MilkProcurement Record(
            SQLiteConnection connection,
            int supplierId,
            double litres,
            double fatPercent,
            double snfPercent,
            decimal ratePerLitre,
            DateTime recordedAt)
        {
            if (litres <= 0)
                throw new ArgumentOutOfRangeException(nameof(litres), litres, "Litres must be greater than zero.");

            var supplier = connection.Find<FarmerSupplier>(supplierId)
                ?? throw new RecordNotFoundException(nameof(FarmerSupplier), supplierId);

            var procurement = new MilkProcurement
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                Date = recordedAt,
                MilkType = supplier.MilkType,
                Litres = litres,
                FatPercent = fatPercent,
                SnfPercent = snfPercent,
                RatePerLitre = ratePerLitre,
                IsPaid = false
            };
            connection.Insert(procurement);
            SyncQueueWriter.Enqueue(connection, procurement, SyncOperation.Insert);

            supplier.OutstandingPayout += procurement.TotalAmount;
            connection.Update(supplier);
            SyncQueueWriter.Enqueue(connection, supplier, SyncOperation.Update);

            return procurement;
        }
    }
}
