using MilkWala.Helpers;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>The checks shared by adding and editing a customer, so the two can never disagree.</summary>
    internal static class CustomerRules
    {
        /// <summary>
        /// Validates the details and returns the cleaned name and 10-digit mobile.
        /// </summary>
        /// <param name="excludeCustomerId">When editing, the customer being edited, so keeping their own number is allowed.</param>
        /// <exception cref="BusinessRuleException">A field is missing or invalid, the route does not exist, or another customer already has the mobile.</exception>
        public static (string Name, string Mobile) Validate(
            SQLiteConnection connection,
            string? name,
            string? mobile,
            double litres,
            int routeId,
            int? excludeCustomerId = null)
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
                throw new BusinessRuleException("Enter the customer's name.");

            var cleanMobile = MobileNumber.Normalize(mobile);
            if (cleanMobile.Length == 0)
                throw new BusinessRuleException("Enter a valid 10-digit mobile number.");
            if (litres <= 0)
                throw new BusinessRuleException("Daily quantity must be greater than zero.");
            if (routeId > 0 && connection.Find<DeliveryRoute>(routeId) == null)
                throw new BusinessRuleException("The selected route no longer exists. Pick another route.");

            var duplicate = connection.Table<Customer>().ToList()
                .FirstOrDefault(c => c.Id != excludeCustomerId && MobileNumber.Normalize(c.Mobile) == cleanMobile);
            if (duplicate != null)
                throw new BusinessRuleException($"This mobile number already belongs to {duplicate.Name}.");

            return (cleanName, cleanMobile);
        }
    }
}
