using MilkWala.Models;
using MilkWala.Services.Scheduling;

namespace MilkWala.Services.Reporting
{
    /// <summary>Litres of one milk type to have ready for a day.</summary>
    public sealed class DemandForecastResult
    {
        public MilkType MilkType { get; set; }

        /// <summary>Litres on plans that deliver that day (holds not yet subtracted).</summary>
        public double SubscribedLitres { get; set; }

        /// <summary>Part of <see cref="SubscribedLitres"/> that is on vacation hold that day.</summary>
        public double VacationPausedLitres { get; set; }

        /// <summary>Approved one-off extra orders for that day.</summary>
        public double SpecialOrderExtraLitres { get; set; }

        public double PredictedRequiredLitres => Math.Max(0, (SubscribedLitres - VacationPausedLitres) + SpecialOrderExtraLitres);
    }

    /// <summary>
    /// Works out how much milk to have ready, using the same rules as delivery generation so the forecast matches
    /// what will actually be on tomorrow's delivery list. It is a rule-based count, not a prediction model.
    /// </summary>
    public static class DemandForecaster
    {
        /// <summary>
        /// One result per milk type for <paramref name="date"/>. Plans of inactive customers are ignored, an
        /// alternate-day plan only counts on its delivery days, and holds are applied per day.
        /// </summary>
        public static IReadOnlyList<DemandForecastResult> Forecast(
            DateTime date,
            IEnumerable<Subscription> subscriptions,
            IEnumerable<Customer> customers,
            IEnumerable<SpecialOrder> specialOrders)
        {
            var day = date.Date;
            var activeCustomerIds = customers.Where(c => c.IsActive).Select(c => c.Id).ToHashSet();

            var dueToday = subscriptions
                .Where(s => activeCustomerIds.Contains(s.CustomerId) && SubscriptionRules.IsDueOn(s, day))
                .ToList();
            var extras = specialOrders
                .Where(o => o.IsApproved && o.TargetDate.Date == day && activeCustomerIds.Contains(o.CustomerId))
                .ToList();

            var results = new List<DemandForecastResult>();
            foreach (MilkType milkType in Enum.GetValues(typeof(MilkType)))
            {
                var ofType = dueToday.Where(s => s.MilkType == milkType).ToList();
                results.Add(new DemandForecastResult
                {
                    MilkType = milkType,
                    SubscribedLitres = ofType.Sum(s => s.QuantityLitres),
                    VacationPausedLitres = ofType.Where(s => SubscriptionRules.IsPausedOn(s, day)).Sum(s => s.QuantityLitres),
                    SpecialOrderExtraLitres = extras.Where(o => o.MilkType == milkType).Sum(o => o.ExtraQuantityLitres)
                });
            }

            return results;
        }
    }
}
