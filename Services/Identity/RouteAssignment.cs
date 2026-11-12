using MilkWala.Models;

namespace MilkWala.Services.Identity
{
    /// <summary>Which routes and deliveries belong to a delivery person.</summary>
    public static class RouteAssignment
    {
        /// <summary>The routes assigned to the delivery person with this user id, in route order.</summary>
        public static IReadOnlyList<DeliveryRoute> RoutesFor(int deliveryUserId, IEnumerable<DeliveryRoute> routes) =>
            routes.Where(r => r.AssignedDeliveryBoyId == deliveryUserId)
                  .OrderBy(r => r.SequenceOrder)
                  .ThenBy(r => r.Id)
                  .ToList();

        /// <summary>
        /// Only the deliveries whose customer lives on one of <paramref name="myRoutes"/>, in route order and then by
        /// customer name. A delivery whose customer has no route, or is unknown, belongs to nobody's run and is left out.
        /// </summary>
        public static IReadOnlyList<Delivery> DeliveriesOnRoutes(
            IEnumerable<Delivery> deliveries,
            IEnumerable<Customer> customers,
            IReadOnlyList<DeliveryRoute> myRoutes)
        {
            var routeOrder = myRoutes
                .Select((route, index) => (route.Id, index))
                .ToDictionary(x => x.Id, x => x.index);
            var routeOfCustomer = customers
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().RouteId);

            return deliveries
                .Where(d => routeOfCustomer.TryGetValue(d.CustomerId, out var routeId) && routeOrder.ContainsKey(routeId))
                .OrderBy(d => routeOrder[routeOfCustomer[d.CustomerId]])
                .ThenBy(d => d.CustomerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
