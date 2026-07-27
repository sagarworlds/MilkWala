using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MilkWala.Models;
using Microsoft.Maui.ApplicationModel;

namespace MilkWala.Services
{
    public class InvoiceService
    {
        public string GenerateMonthlyInvoiceSummary(Customer customer, List<Delivery> monthDeliveries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🧾 *MilkWala Monthly Statement*");
            sb.AppendLine($"Customer: *{customer.Name}*");
            sb.AppendLine($"Mobile: {customer.Mobile}");
            sb.AppendLine($"Period: {DateTime.Now:MMMM yyyy}");
            sb.AppendLine("--------------------------------");

            double totalLitres = 0;
            decimal totalAmount = 0;
            int deliveredCount = 0;

            foreach (var d in monthDeliveries)
            {
                if (d.Status == DeliveryStatus.Delivered || d.Status == DeliveryStatus.Extra)
                {
                    totalLitres += d.QuantityLitres;
                    totalAmount += d.TotalPrice;
                    deliveredCount++;
                }
            }

            sb.AppendLine($"✅ Total Days Delivered: {deliveredCount}");
            sb.AppendLine($"🥛 Total Litres: {totalLitres:F1} Litres");
            sb.AppendLine($"💰 Total Bill: ₹{totalAmount:N2}");
            sb.AppendLine($"💳 Wallet Balance: ₹{customer.WalletBalance:N2}");
            sb.AppendLine($"⚠️ Outstanding Dues: ₹{customer.OutstandingBalance:N2}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Thank you for subscribing with MilkWala Dairy!");

            return sb.ToString();
        }

        public async Task ShareInvoiceOnWhatsAppAsync(Customer customer, List<Delivery> monthDeliveries)
        {
            string message = GenerateMonthlyInvoiceSummary(customer, monthDeliveries);
            string encodedText = Uri.EscapeDataString(message);
            string cleanMobile = customer.Mobile.Replace(" ", "").Replace("+", "");

            if (!cleanMobile.StartsWith("91") && cleanMobile.Length == 10)
            {
                cleanMobile = "91" + cleanMobile;
            }

            string whatsappUrl = $"https://wa.me/{cleanMobile}?text={encodedText}";
            await Launcher.Default.OpenAsync(whatsappUrl);
        }
    }
}
