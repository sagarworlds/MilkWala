using MilkWala.Models;

namespace MilkWala.Services.Scheduling
{
    /// <summary>Deliveries to create for one day, plus anything that could not be planned and why.</summary>
    public sealed record DeliveryPlan(IReadOnlyList<Delivery> NewDeliveries, IReadOnlyList<string> Warnings);

    /// <summary>Turns subscriptions into the day's delivery rows. Pure, so it is unit tested without a database.</summary>
    public static class DeliveryPlanner
    {
        /// <summary>
        /// Plans one <see cref="Delivery"/> per due subscription of an active customer.
        /// Subscriptions under a vacation hold produce a zero-litre <see cref="DeliveryStatus.Holiday"/> row so the
        /// calendar shows the hold. Running the plan again with the rows it produced as <paramref name="existingForDay"/>
        /// plans nothing, which makes generation safe to repeat.
        /// </summary>
        /// <param name="date">The day to plan (time of day ignored).</param>
        /// <param name="subscriptions">All subscriptions.</param>
        /// <param name="customers">All customers.</param>
        /// <param name="products">Milk price list; a subscription whose milk type has no price is reported, not guessed.</param>
        /// <param name="existingForDay">Deliveries already stored for that day.</param>
        public static DeliveryPlan Plan(
            DateTime date,
            IEnumerable<Subscription> subscriptions,
            IEnumerable<Customer> customers,
            IEnumerable<Product> products,
            IEnumerable<Delivery> existingForDay)
        {
            var day = date.Date;
            var customersById = customers.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            var priceByMilk = products.GroupBy(p => p.MilkType).ToDictionary(g => g.Key, g => g.First().PricePerLitre);
            var taken = existingForDay
                .Where(d => d.DeliveryDate.Date == day)
                .Select(d => (d.CustomerId, d.MilkType))
                .ToHashSet();

            var created = new List<Delivery>();
            var warnings = new List<string>();

            foreach (var subscription in subscriptions)
            {
                if (!customersById.TryGetValue(subscription.CustomerId, out var customer) || !customer.IsActive)
                    continue;
                if (!SubscriptionRules.IsDueOn(subscription, day))
                    continue;
                if (!taken.Add((subscription.CustomerId, subscription.MilkType)))
                    continue;

                bool onHold = SubscriptionRules.IsPausedOn(subscription, day);
                if (!onHold && subscription.QuantityLitres <= 0)
                    continue;

                priceByMilk.TryGetValue(subscription.MilkType, out var price);
                if (!onHold && !priceByMilk.ContainsKey(subscription.MilkType))
                {
                    warnings.Add($"No price is set for {subscription.MilkType} milk, so {customer.Name}'s delivery was not created.");
                    taken.Remove((subscription.CustomerId, subscription.MilkType));
                    continue;
                }

                created.Add(new Delivery
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Address = customer.Address,
                    DeliveryDate = day,
                    MilkType = subscription.MilkType,
                    QuantityLitres = onHold ? 0 : subscription.QuantityLitres,
                    RatePerLitre = price,
                    Status = onHold ? DeliveryStatus.Holiday : DeliveryStatus.Pending
                });
            }

            return new DeliveryPlan(created, warnings);
        }
    }
}
