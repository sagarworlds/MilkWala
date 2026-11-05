using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Identity;

namespace MilkWala.ViewModels
{
    /// <summary>Base for customer-portal screens: resolves the signed-in customer instead of assuming id 1.</summary>
    public abstract class CustomerViewModelBase : BaseViewModel
    {
        protected readonly ICurrentUserService CurrentUser;

        protected CustomerViewModelBase(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory,
            ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            CurrentUser = currentUser;
        }

        /// <summary>
        /// The signed-in customer's profile. When there is none, the shared error banner explains why
        /// and null is returned so the caller can stop.
        /// </summary>
        protected async Task<Customer?> RequireCustomerAsync()
        {
            var customer = await CurrentUser.GetCurrentCustomerAsync();
            if (customer == null)
                ErrorMessage = "No customer profile was found for the signed-in mobile number.";
            return customer;
        }
    }
}
