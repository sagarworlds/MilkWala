using MilkWala.Models;
using MilkWala.Services.Reporting;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>What a status change did to the delivery and to the customer's money.</summary>
    /// <param name="Delivery">The delivery after the change.</param>
    /// <param name="ChargedToWallet">Taken from the wallet by this change.</param>
    /// <param name="AddedToDues">Added to outstanding dues because the wallet could not cover it.</param>
    /// <param name="RefundedToWallet">Given back to the wallet (a billed delivery was corrected).</param>
    /// <param name="DuesReduced">Taken off outstanding dues (a billed delivery was corrected).</param>
    public sealed record DeliveryChange(
        Delivery Delivery,
        decimal ChargedToWallet,
        decimal AddedToDues,
        decimal RefundedToWallet,
        decimal DuesReduced)
    {
        /// <summary>One-line, user-facing description of the change.</summary>
        public string Summary()
        {
            var name = Delivery.CustomerName;
            var text = Delivery.Status switch
            {
                DeliveryStatus.Delivered or DeliveryStatus.Extra => $"Delivered to {name}.",
                DeliveryStatus.Skipped => $"Skipped for {name}.",
                _ => $"{name} marked {Delivery.Status}."
            };

            if (ChargedToWallet > 0) text += $" ₹{ChargedToWallet:N2} charged to wallet.";
            if (AddedToDues > 0) text += $" ₹{AddedToDues:N2} added to dues.";
            if (RefundedToWallet > 0) text += $" ₹{RefundedToWallet:N2} refunded to wallet.";
            if (DuesReduced > 0) text += $" ₹{DuesReduced:N2} removed from dues.";
            return text;
        }
    }

    /// <summary>
    /// Changes a delivery's status and bills or un-bills the customer in the same transaction.
    /// Billing happens exactly once per delivery: only the move into a billable status (Delivered/Extra) charges,
    /// and only the move out of one reverses, so repeated taps and corrections cannot double-charge.
    /// </summary>
    public static class DeliveryLedger
    {
        /// <summary>
        /// Sets the delivery status. Billable status: the price is taken from the wallet first and any shortfall is added
        /// to the customer's dues. Leaving a billable status: the recorded split is reversed exactly.
        /// </summary>
        /// <param name="connection">Connection of the surrounding transaction.</param>
        /// <param name="deliveryId">The delivery to change (re-read inside the transaction).</param>
        /// <param name="newStatus">The new status.</param>
        /// <param name="now">Local time used as the delivery time.</param>
        /// <exception cref="RecordNotFoundException">The delivery or its customer does not exist.</exception>
        public static DeliveryChange ChangeStatus(SQLiteConnection connection, int deliveryId, DeliveryStatus newStatus, DateTime now)
        {
            var delivery = connection.Find<Delivery>(deliveryId)
                ?? throw new RecordNotFoundException(nameof(Delivery), deliveryId);

            bool wasBilled = DeliveryStats.IsFulfilled(delivery.Status);
            bool willBill = DeliveryStats.IsFulfilled(newStatus);

            decimal charged = 0, addedToDues = 0, refunded = 0, duesReduced = 0;
            if (!wasBilled && willBill)
                (charged, addedToDues) = Charge(connection, delivery);
            else if (wasBilled && !willBill)
                (refunded, duesReduced) = Reverse(connection, delivery);

            delivery.Status = newStatus;
            // Only a completed delivery has a delivery time; leaving one clears it.
            delivery.DeliveredAt = willBill ? (wasBilled ? delivery.DeliveredAt : now) : default;
            delivery.UpdatedAt = DateTime.UtcNow;
            connection.Update(delivery);
            SyncQueueWriter.Enqueue(connection, delivery, SyncOperation.Update);

            return new DeliveryChange(delivery, charged, addedToDues, refunded, duesReduced);
        }

        private static (decimal FromWallet, decimal ToDues) Charge(SQLiteConnection connection, Delivery delivery)
        {
            decimal amount = delivery.TotalPrice;
            if (amount <= 0)
                return (0, 0);

            var customer = connection.Find<Customer>(delivery.CustomerId)
                ?? throw new RecordNotFoundException(nameof(Customer), delivery.CustomerId);

            decimal fromWallet = Math.Min(Math.Max(customer.WalletBalance, 0), amount);
            decimal toDues = amount - fromWallet;

            if (fromWallet > 0)
                WalletLedger.Debit(connection, customer.Id, fromWallet, Describe(delivery));
            if (toDues > 0)
                WalletLedger.AdjustOutstanding(connection, customer.Id, toDues);

            delivery.AmountChargedToWallet = fromWallet;
            delivery.AmountAddedToDues = toDues;
            return (fromWallet, toDues);
        }

        private static (decimal Refunded, decimal DuesReduced) Reverse(SQLiteConnection connection, Delivery delivery)
        {
            decimal fromWallet = delivery.AmountChargedToWallet;
            decimal toDues = delivery.AmountAddedToDues;
            decimal refunded = fromWallet;
            decimal duesReduced = 0;

            if (toDues > 0)
            {
                var customer = connection.Find<Customer>(delivery.CustomerId)
                    ?? throw new RecordNotFoundException(nameof(Customer), delivery.CustomerId);

                // If the customer has already paid part of those dues off (a recharge clears dues), that part is no
                // longer owed, so it goes back to their wallet instead of driving the dues negative.
                duesReduced = Math.Min(customer.OutstandingBalance, toDues);
                refunded += toDues - duesReduced;
                if (duesReduced > 0)
                    WalletLedger.AdjustOutstanding(connection, customer.Id, -duesReduced);
            }

            if (refunded > 0)
                WalletLedger.Credit(connection, delivery.CustomerId, refunded, $"Refund: {Describe(delivery)}");

            delivery.AmountChargedToWallet = 0;
            delivery.AmountAddedToDues = 0;
            return (refunded, duesReduced);
        }

        private static string Describe(Delivery delivery) =>
            $"Milk delivery {delivery.DeliveryDate:dd MMM}: {delivery.QuantityLitres} L {delivery.MilkType}";
    }
}
