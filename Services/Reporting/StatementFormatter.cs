using MilkWala.Models;

namespace MilkWala.Services.Reporting
{
    /// <summary>Builds the monthly statement text a vendor sends to a customer (for example over WhatsApp).</summary>
    public static class StatementFormatter
    {
        /// <summary>
        /// Statement for the calendar month containing <paramref name="month"/>: one line per billed delivery,
        /// the totals, and what the customer currently holds or owes. Deliveries from other months are ignored,
        /// so the period shown always matches the figures. Amounts use fixed (invariant) formatting so the text is
        /// the same on every device.
        /// </summary>
        public static string Build(Customer customer, IEnumerable<Delivery> deliveries, DateTime month)
        {
            ArgumentNullException.ThrowIfNull(customer);

            var inMonth = deliveries
                .Where(d => d.DeliveryDate.Year == month.Year && d.DeliveryDate.Month == month.Month)
                .OrderBy(d => d.DeliveryDate)
                .ToList();
            var billed = inMonth.Where(d => DeliveryStats.IsFulfilled(d.Status)).ToList();
            int notDelivered = inMonth.Count(d => d.Status is DeliveryStatus.Skipped or DeliveryStatus.Holiday or DeliveryStatus.Missed);

            var lines = new List<string>
            {
                "🧾 *MilkWala Monthly Statement*",
                FormattableString.Invariant($"Customer: *{customer.Name}*"),
                FormattableString.Invariant($"Mobile: {customer.Mobile}"),
                FormattableString.Invariant($"Period: {month:MMMM yyyy}"),
                "--------------------------------"
            };

            foreach (var d in billed)
                lines.Add(FormattableString.Invariant($"{d.DeliveryDate:dd MMM}  {d.QuantityLitres:0.0#} L {d.MilkType}  ₹{d.TotalPrice:N2}"));
            if (billed.Count == 0)
                lines.Add("No deliveries this month.");

            lines.Add("--------------------------------");
            lines.Add(FormattableString.Invariant($"✅ Days delivered: {billed.Count}"));
            if (notDelivered > 0)
                lines.Add(FormattableString.Invariant($"⏸️ Days skipped / on hold: {notDelivered}"));
            lines.Add(FormattableString.Invariant($"🥛 Total litres: {DeliveryStats.Litres(inMonth):0.0#} L"));
            lines.Add(FormattableString.Invariant($"💰 Total bill: ₹{DeliveryStats.Revenue(inMonth):N2}"));
            lines.Add(FormattableString.Invariant($"💳 Wallet balance: ₹{customer.WalletBalance:N2}"));
            lines.Add(FormattableString.Invariant($"⚠️ Outstanding dues: ₹{customer.OutstandingBalance:N2}"));
            lines.Add("--------------------------------");
            lines.Add("Thank you for choosing MilkWala Dairy!");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
