using MilkWala.Models;

namespace MilkWala.Services.Scheduling
{
    /// <summary>
    /// The single definition of when a subscription delivers, shared by delivery generation and the demand forecast
    /// so the two can never disagree about a vacation hold or an alternate-day plan.
    /// </summary>
    public static class SubscriptionRules
    {
        /// <summary>
        /// True while a vacation hold covers <paramref name="date"/>. A hold whose end date has passed no longer applies,
        /// so deliveries resume by themselves even if the customer never taps "Resume".
        /// </summary>
        public static bool IsPausedOn(Subscription subscription, DateTime date)
        {
            if (!subscription.IsPaused)
                return false;

            var day = date.Date;
            return (subscription.PauseStartDate == null || subscription.PauseStartDate.Value.Date <= day)
                && (subscription.PauseEndDate == null || subscription.PauseEndDate.Value.Date >= day);
        }

        /// <summary>
        /// True when the plan delivers on <paramref name="date"/>, ignoring holds: not before the start date, and on
        /// every second day counted from the start date for "Alternate". "Daily" and anything unrecognised
        /// (e.g. "Custom", which has no schedule data yet) deliver every day.
        /// </summary>
        public static bool IsDueOn(Subscription subscription, DateTime date)
        {
            var daysSinceStart = (date.Date - subscription.StartDate.Date).Days;
            if (daysSinceStart < 0)
                return false;

            return string.Equals(subscription.DeliveryFrequency, "Alternate", StringComparison.OrdinalIgnoreCase)
                ? daysSinceStart % 2 == 0
                : true;
        }
    }
}
