using Microsoft.Maui.Storage;
using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>Resolves the signed-in user from the stored session and the local database.</summary>
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly LocalDbService _db;
        private readonly IPreferences _preferences;

        public CurrentUserService(LocalDbService db, IPreferences preferences)
        {
            _db = db;
            _preferences = preferences;
        }

        public UserRole? Role =>
            Enum.TryParse<UserRole>(_preferences.Get(SessionKeys.ActiveRole, string.Empty), out var role) ? role : null;

        public async Task<Customer?> GetCurrentCustomerAsync()
        {
            if (Role != UserRole.Customer)
                return null;

            var users = await _db.GetAllAsync<User>();
            var customers = await _db.GetAllAsync<Customer>();
            return CustomerLookup.Find(_preferences.Get(SessionKeys.UserMobile, string.Empty), users, customers);
        }
    }
}
