using MilkWala.Helpers;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>What the vendor changes when editing a customer.</summary>
    /// <param name="CustomerId">The customer being edited.</param>
    /// <param name="Name">Full name.</param>
    /// <param name="Mobile">10-digit mobile; also how the customer logs in.</param>
    /// <param name="Address">Delivery address.</param>
    /// <param name="MilkType">Milk the customer gets.</param>
    /// <param name="Litres">Daily quantity.</param>
    /// <param name="RouteId">Delivery route (0 = none).</param>
    /// <param name="Today">The current local date: pending deliveries from today on get the new name/address, and a missing subscription starts tomorrow.</param>
    public sealed record CustomerEdit(
        int CustomerId,
        string Name,
        string Mobile,
        string Address,
        MilkType MilkType,
        double Litres,
        int RouteId,
        DateTime Today);

    /// <summary>Editing an existing customer. Run inside a transaction.</summary>
    public static class CustomerLedger
    {
        /// <summary>
        /// Saves the vendor's changes to a customer: details, route and daily plan. The plan change goes to the
        /// subscription (which drives deliveries), and a customer who has no subscription yet gets one starting
        /// tomorrow. The sign-in account follows a changed mobile number, and deliveries that are still pending
        /// from today on show the new name and address. Delivered history and billing are never touched.
        /// </summary>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        /// <exception cref="BusinessRuleException">A field is invalid, the route is gone, or another customer has the mobile.</exception>
        public static Customer Update(SQLiteConnection connection, CustomerEdit edit)
        {
            ArgumentNullException.ThrowIfNull(edit);

            var customer = connection.Find<Customer>(edit.CustomerId)
                ?? throw new RecordNotFoundException(nameof(Customer), edit.CustomerId);

            var (name, mobile) = CustomerRules.Validate(
                connection, edit.Name, edit.Mobile, edit.Litres, edit.RouteId, excludeCustomerId: edit.CustomerId);
            var address = (edit.Address ?? string.Empty).Trim();

            bool mobileChanged = MobileNumber.Normalize(customer.Mobile) != mobile;

            customer.Name = name;
            customer.Mobile = mobile;
            customer.Address = address;
            customer.RouteId = edit.RouteId;
            customer.PreferredMilkType = edit.MilkType;
            customer.PreferredQuantity = edit.Litres;
            customer.UpdatedAt = DateTime.UtcNow;
            connection.Update(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Update);

            SubscriptionLedger.SetSubscription(connection, customer.Id, edit.MilkType, edit.Litres, startIfMissing: edit.Today.AddDays(1));

            if (mobileChanged && customer.UserId > 0)
            {
                var user = connection.Find<User>(customer.UserId);
                if (user is { Role: UserRole.Customer })
                {
                    user.MobileNumber = mobile;
                    connection.Update(user);
                    SyncQueueWriter.Enqueue(connection, user, SyncOperation.Update);
                }
            }

            var from = edit.Today.Date;
            var pending = connection.Table<Delivery>()
                .Where(d => d.CustomerId == customer.Id && d.DeliveryDate >= from && d.Status == DeliveryStatus.Pending)
                .ToList();
            foreach (var delivery in pending)
            {
                delivery.CustomerName = name;
                delivery.Address = address;
                delivery.UpdatedAt = DateTime.UtcNow;
                connection.Update(delivery);
                SyncQueueWriter.Enqueue(connection, delivery, SyncOperation.Update);
            }

            return customer;
        }
    }
}
