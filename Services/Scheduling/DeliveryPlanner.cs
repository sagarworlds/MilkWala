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
        /// <param name="specialOrders">One-off extra-milk orders; each approved one for the day becomes its own delivery.</param>
        public static DeliveryPlan Plan(
            DateTime date,
            IEnumerable<Subscription> subscriptions,
            IEnumerable<Customer> customers,
            IEnumerable<Product> products,
            IEnumerable<Delivery> existingForDay,
            IEnumerable<SpecialOrder>? specialOrders = null)
        {
            var day = date.Date;
            var customersById = customers.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            var priceByMilk = products.GroupBy(p => p.MilkType).ToDictionary(g => g.Key, g => g.First().PricePerLitre);
            var existing = existingForDay.Where(d => d.DeliveryDate.Date == day).ToList();
            // Regular deliveries are one per customer and milk type; extras are tracked by the order they fulfil,
            // so an extra of the same milk type does not collide with the subscription delivery.
            var taken = existing
                .Where(d => d.SpecialOrderId == 0)
                .Select(d => (d.CustomerId, d.MilkType))
                .ToHashSet();
            var fulfilledExtras = existing.Where(d => d.SpecialOrderId > 0).Select(d => d.SpecialOrderId).ToHashSet();

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

            foreach (var order in specialOrders ?? Array.Empty<SpecialOrder>())
            {
                if (!order.IsApproved || order.TargetDate.Date != day || order.ExtraQuantityLitres <= 0)
                    continue;
                if (!customersById.TryGetValue(order.CustomerId, out var customer) || !customer.IsActive)
                    continue;
                if (!fulfilledExtras.Add(order.Id))
                    continue;

                if (!priceByMilk.TryGetValue(order.MilkType, out var extraPrice))
                {
                    warnings.Add($"No price is set for {order.MilkType} milk, so {customer.Name}'s extra order was not created.");
                    fulfilledExtras.Remove(order.Id);
                    continue;
                }

                created.Add(new Delivery
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Address = customer.Address,
                    DeliveryDate = day,
                    MilkType = order.MilkType,
                    QuantityLitres = order.ExtraQuantityLitres,
                    RatePerLitre = extraPrice,
                    Status = DeliveryStatus.Pending,
                    SpecialOrderId = order.Id,
                    Notes = string.IsNullOrWhiteSpace(order.SpecialInstruction)
                        ? "Extra order"
                        : $"Extra order: {order.SpecialInstruction.Trim()}"
                });
            }

            return new DeliveryPlan(created, warnings);
        }
    }
}
