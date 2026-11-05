using MilkWala.Helpers;
using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>Finds which customer profile belongs to a signed-in mobile number.</summary>
    public static class CustomerLookup
    {
        /// <summary>
        /// Matches the mobile to a customer-role user and that user's profile first; if there is no user
        /// account (profiles added by the vendor have none yet) falls back to the profile's own mobile.
        /// </summary>
        /// <returns>The customer, or null when nothing matches — never "the first customer".</returns>
        public static Customer? Find(string? mobile, IEnumerable<User> users, IEnumerable<Customer> customers)
        {
            var key = MobileNumber.Normalize(mobile);
            if (key.Length == 0)
                return null;

            var customerList = customers as IList<Customer> ?? customers.ToList();

            var user = users.FirstOrDefault(u => u.Role == UserRole.Customer && MobileNumber.Normalize(u.MobileNumber) == key);
            if (user != null)
            {
                var profile = customerList.FirstOrDefault(c => c.UserId == user.Id);
                if (profile != null)
                    return profile;
            }

            return customerList.FirstOrDefault(c => MobileNumber.Normalize(c.Mobile) == key);
        }
    }
}
