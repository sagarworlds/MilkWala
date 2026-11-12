using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>One row of the vendor's route list.</summary>
    public sealed record RouteSummary(int Id, string RouteName, string AreaDescription, string StaffName, int CustomerCount)
    {
        /// <summary>"3 customers" / "1 customer", for display.</summary>
        public string CustomerCountText => CustomerCount == 1 ? "1 customer" : $"{CustomerCount} customers";
    }

    /// <summary>Builds the route list the vendor sees.</summary>
    public static class RouteSummaries
    {
        public const string Unassigned = "Unassigned";

        /// <summary>
        /// Routes in run order with the delivery person's name and how many customers are on each. A route whose
        /// person is missing or deactivated says so, so the vendor can see which routes nobody can deliver.
        /// </summary>
        public static IReadOnlyList<RouteSummary> Build(
            IEnumerable<DeliveryRoute> routes,
            IEnumerable<User> users,
            IEnumerable<Customer> customers)
        {
            var userById = users.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
            var customersPerRoute = customers.GroupBy(c => c.RouteId).ToDictionary(g => g.Key, g => g.Count());

            return routes
                .OrderBy(r => r.SequenceOrder)
                .ThenBy(r => r.Id)
                .Select(r => new RouteSummary(
                    r.Id,
                    r.RouteName,
                    r.AreaDescription,
                    StaffName(r.AssignedDeliveryBoyId, userById),
                    customersPerRoute.TryGetValue(r.Id, out var count) ? count : 0))
                .ToList();
        }

        private static string StaffName(int userId, IReadOnlyDictionary<int, User> userById)
        {
            if (userId == 0)
                return Unassigned;
            if (!userById.TryGetValue(userId, out var user))
                return "Unknown (account removed)";
            return user.IsActive ? user.Name : $"{user.Name} (deactivated)";
        }
    }
}
