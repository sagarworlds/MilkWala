using MilkWala.Models;

namespace MilkWala.Services.Reporting
{
    /// <summary>
    /// Single definition of which deliveries count as "fulfilled" so every screen agrees on revenue and litres.
    /// </summary>
    public static class DeliveryStats
    {
        /// <summary>True for deliveries that reached the customer and are billable (Delivered or Extra).</summary>
        public static bool IsFulfilled(DeliveryStatus status) =>
            status == DeliveryStatus.Delivered || status == DeliveryStatus.Extra;

        /// <summary>Sum of the billable value of fulfilled deliveries.</summary>
        public static decimal Revenue(IEnumerable<Delivery> deliveries) =>
            deliveries.Where(d => IsFulfilled(d.Status)).Sum(d => d.TotalPrice);

        /// <summary>Sum of litres of fulfilled deliveries.</summary>
        public static double Litres(IEnumerable<Delivery> deliveries) =>
            deliveries.Where(d => IsFulfilled(d.Status)).Sum(d => d.QuantityLitres);

        /// <summary>The <paramref name="count"/> latest deliveries, newest first.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
        public static IReadOnlyList<Delivery> MostRecent(IEnumerable<Delivery> deliveries, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return deliveries.OrderByDescending(d => d.DeliveryDate).Take(count).ToList();
        }
    }
}
