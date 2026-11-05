using MilkWala.Helpers;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>What the vendor enters to add a customer.</summary>
    /// <param name="Name">Full name.</param>
    /// <param name="Mobile">10-digit mobile; also how the customer logs in.</param>
    /// <param name="Address">Delivery address.</param>
    /// <param name="MilkType">Milk the customer wants.</param>
    /// <param name="Litres">Daily quantity.</param>
    /// <param name="RouteId">Delivery route (0 = none yet).</param>
    /// <param name="StartDate">First delivery day.</param>
    public sealed record NewCustomerRequest(
        string Name,
        string Mobile,
        string Address,
        MilkType MilkType,
        double Litres,
        int RouteId,
        DateTime StartDate);

    /// <summary>Adds a customer together with the subscription that makes the daily deliveries happen.</summary>
    public static class CustomerOnboarding
    {
        /// <summary>
        /// Creates the customer and a daily subscription in one transaction. The wallet starts empty: a balance
        /// is money, and it only comes from a recharge.
        /// </summary>
        /// <exception cref="BusinessRuleException">A field is missing/invalid, the route does not exist or the mobile is already registered.</exception>
        public static Customer Add(SQLiteConnection connection, NewCustomerRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var name = (request.Name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new BusinessRuleException("Enter the customer's name.");

            var mobile = MobileNumber.Normalize(request.Mobile);
            if (mobile.Length == 0)
                throw new BusinessRuleException("Enter a valid 10-digit mobile number.");
            if (request.Litres <= 0)
                throw new BusinessRuleException("Daily quantity must be greater than zero.");
            if (request.RouteId > 0 && connection.Find<DeliveryRoute>(request.RouteId) == null)
                throw new BusinessRuleException("The selected route no longer exists. Pick another route.");

            var duplicate = connection.Table<Customer>().ToList()
                .FirstOrDefault(c => MobileNumber.Normalize(c.Mobile) == mobile);
            if (duplicate != null)
                throw new BusinessRuleException($"This mobile number already belongs to {duplicate.Name}.");

            var customer = new Customer
            {
                Name = name,
                Mobile = mobile,
                Address = (request.Address ?? string.Empty).Trim(),
                RouteId = request.RouteId,
                PreferredMilkType = request.MilkType,
                PreferredQuantity = request.Litres,
                WalletBalance = 0m,
                IsActive = true
            };
            connection.Insert(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Insert);

            var subscription = new Subscription
            {
                CustomerId = customer.Id,
                MilkType = request.MilkType,
                QuantityLitres = request.Litres,
                DeliveryFrequency = "Daily",
                StartDate = request.StartDate.Date
            };
            connection.Insert(subscription);
            SyncQueueWriter.Enqueue(connection, subscription, SyncOperation.Insert);

            return customer;
        }
    }
}
