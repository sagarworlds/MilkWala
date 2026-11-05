using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>The signed-in person, so screens never need a hardcoded customer id.</summary>
    public interface ICurrentUserService
    {
        /// <summary>The role chosen at login, or null when nobody is signed in.</summary>
        UserRole? Role { get; }

        /// <summary>
        /// The customer profile of the signed-in user.
        /// </summary>
        /// <returns>The profile, or null when the signed-in user is not a customer or has no profile.</returns>
        Task<Customer?> GetCurrentCustomerAsync();
    }
}
