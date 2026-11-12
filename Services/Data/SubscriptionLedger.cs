using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Changing which milk a customer gets and how much. Run inside a transaction.</summary>
    public static class SubscriptionLedger
    {
        /// <summary>
        /// Sets the milk type and daily quantity of the customer's subscription, creating a daily subscription
        /// when there is none and <paramref name="startIfMissing"/> is given. The subscription is what delivery
        /// generation uses, so this is the change that actually alters tomorrow's delivery.
        /// </summary>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        /// <exception cref="BusinessRuleException">The quantity is not positive, or there is no subscription and none may be created.</exception>
        public static Subscription SetSubscription(
            SQLiteConnection connection,
            int customerId,
            MilkType milkType,
            double litres,
            DateTime? startIfMissing = null)
        {
            if (litres <= 0)
                throw new BusinessRuleException("Daily quantity must be greater than zero.");
            if (connection.Find<Customer>(customerId) == null)
                throw new RecordNotFoundException(nameof(Customer), customerId);

            var subscription = connection.Table<Subscription>().Where(s => s.CustomerId == customerId).ToList().FirstOrDefault();
            if (subscription == null)
            {
                if (startIfMissing == null)
                    throw new BusinessRuleException("You do not have an active subscription yet. Please contact your vendor.");

                subscription = new Subscription
                {
                    CustomerId = customerId,
                    MilkType = milkType,
                    QuantityLitres = litres,
                    DeliveryFrequency = "Daily",
                    StartDate = startIfMissing.Value.Date
                };
                connection.Insert(subscription);
                SyncQueueWriter.Enqueue(connection, subscription, SyncOperation.Insert);
                return subscription;
            }

            subscription.MilkType = milkType;
            subscription.QuantityLitres = litres;
            subscription.UpdatedAt = DateTime.UtcNow;
            connection.Update(subscription);
            SyncQueueWriter.Enqueue(connection, subscription, SyncOperation.Update);
            return subscription;
        }

        /// <summary>
        /// A customer changing their own plan: the subscription changes and the customer's remembered preference
        /// follows, so the vendor's directory shows what the customer actually gets.
        /// </summary>
        public static Subscription SetPlan(SQLiteConnection connection, int customerId, MilkType milkType, double litres)
        {
            var subscription = SetSubscription(connection, customerId, milkType, litres);

            var customer = connection.Find<Customer>(customerId)!;
            customer.PreferredMilkType = milkType;
            customer.PreferredQuantity = litres;
            customer.UpdatedAt = DateTime.UtcNow;
            connection.Update(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Update);

            return subscription;
        }
    }
}
