using MilkWala.Helpers;
using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>Finds the user account for a signed-in mobile number and role.</summary>
    public static class UserLookup
    {
        /// <summary>
        /// The active user with this role whose mobile matches (any common number format).
        /// The role must match, so a customer's number cannot sign in as delivery staff.
        /// </summary>
        /// <returns>The user, or null when there is no such active account.</returns>
        public static User? Find(string? mobile, UserRole role, IEnumerable<User> users)
        {
            var key = MobileNumber.Normalize(mobile);
            if (key.Length == 0)
                return null;

            return users.FirstOrDefault(u =>
                u.IsActive && u.Role == role && MobileNumber.Normalize(u.MobileNumber) == key);
        }
    }
}
