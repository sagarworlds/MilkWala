using MilkWala.Helpers;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Delivery staff accounts. Run inside a transaction.</summary>
    public static class StaffLedger
    {
        /// <summary>
        /// Creates a delivery-staff account. The person signs in with this mobile number and the delivery role.
        /// </summary>
        /// <exception cref="BusinessRuleException">The name or mobile is invalid, or another delivery person already has that number.</exception>
        public static User AddDeliveryStaff(SQLiteConnection connection, string name, string mobile)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new BusinessRuleException("Enter the delivery person's name.");

            var normalized = MobileNumber.Normalize(mobile);
            if (normalized.Length == 0)
                throw new BusinessRuleException("Enter a valid 10-digit mobile number.");

            // Sign-in is by number AND role, so the same number may also be a customer; two staff may not share one.
            var duplicate = connection.Table<User>().ToList()
                .FirstOrDefault(u => u.Role == UserRole.DeliveryBoy && MobileNumber.Normalize(u.MobileNumber) == normalized);
            if (duplicate != null)
                throw new BusinessRuleException($"This mobile number already belongs to delivery person {duplicate.Name}.");

            var user = new User { Name = name, MobileNumber = normalized, Role = UserRole.DeliveryBoy, IsActive = true };
            connection.Insert(user);
            SyncQueueWriter.Enqueue(connection, user, SyncOperation.Insert);
            return user;
        }
    }
}
