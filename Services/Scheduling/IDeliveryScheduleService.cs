namespace MilkWala.Services.Scheduling
{
    /// <summary>Makes sure each day has its deliveries, created from the customers' subscriptions.</summary>
    public interface IDeliveryScheduleService
    {
        /// <summary>
        /// Creates the day's missing deliveries. Safe to call repeatedly and from several screens.
        /// </summary>
        /// <param name="date">The day (time of day ignored).</param>
        /// <returns>How many deliveries were created.</returns>
        Task<int> EnsureDeliveriesAsync(DateTime date);
    }
}
