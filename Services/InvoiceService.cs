using Microsoft.Maui.ApplicationModel;
using MilkWala.Helpers;
using MilkWala.Models;
using MilkWala.Services.Data;
using MilkWala.Services.Reporting;

namespace MilkWala.Services
{
    /// <summary>Sends a customer their monthly statement.</summary>
    public interface IStatementService
    {
        /// <summary>
        /// Opens WhatsApp with the customer's statement for <paramref name="month"/> ready to send.
        /// </summary>
        /// <returns>False when the device could not open WhatsApp (for example it is not installed).</returns>
        /// <exception cref="BusinessRuleException">The customer's mobile number is not a valid WhatsApp number.</exception>
        Task<bool> ShareOnWhatsAppAsync(Customer customer, IEnumerable<Delivery> deliveries, DateTime month);
    }

    public class InvoiceService : IStatementService
    {
        public async Task<bool> ShareOnWhatsAppAsync(Customer customer, IEnumerable<Delivery> deliveries, DateTime month)
        {
            var message = StatementFormatter.Build(customer, deliveries, month);
            var link = WhatsAppLink.Build(customer.Mobile, message)
                ?? throw new BusinessRuleException($"{customer.Name}'s mobile number is not valid for WhatsApp.");

            return await Launcher.Default.OpenAsync(new Uri(link));
        }
    }
}
