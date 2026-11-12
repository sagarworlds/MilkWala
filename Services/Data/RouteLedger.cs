using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>
    /// Creating routes, assigning delivery staff to them and moving customers between them. Every method runs inside
    /// a transaction so a change and its sync entry are saved together. A route id or staff id of 0 means "none".
    /// </summary>
    public static class RouteLedger
    {
        /// <summary>Adds a route at the end of the run order, optionally with a delivery person already assigned.</summary>
        /// <exception cref="BusinessRuleException">The name is empty or already used, or the staff member is not an active delivery person.</exception>
        public static DeliveryRoute AddRoute(SQLiteConnection connection, string name, string area, int deliveryUserId)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new BusinessRuleException("Enter a name for the route.");

            var existing = connection.Table<DeliveryRoute>().ToList();
            if (existing.Any(r => string.Equals(r.RouteName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException($"A route called \"{name}\" already exists.");

            RequireActiveDeliveryStaff(connection, deliveryUserId);

            var route = new DeliveryRoute
            {
                RouteName = name,
                AreaDescription = (area ?? string.Empty).Trim(),
                AssignedDeliveryBoyId = deliveryUserId,
                SequenceOrder = existing.Count == 0 ? 1 : existing.Max(r => r.SequenceOrder) + 1
            };
            connection.Insert(route);
            SyncQueueWriter.Enqueue(connection, route, SyncOperation.Insert);
            return route;
        }

        /// <summary>Gives the route to a delivery person, or takes it off everyone with id 0.</summary>
        /// <exception cref="RecordNotFoundException">The route does not exist.</exception>
        /// <exception cref="BusinessRuleException">The user is not an active delivery person.</exception>
        public static DeliveryRoute AssignStaff(SQLiteConnection connection, int routeId, int deliveryUserId)
        {
            var route = connection.Find<DeliveryRoute>(routeId)
                ?? throw new RecordNotFoundException(nameof(DeliveryRoute), routeId);

            RequireActiveDeliveryStaff(connection, deliveryUserId);

            route.AssignedDeliveryBoyId = deliveryUserId;
            connection.Update(route);
            SyncQueueWriter.Enqueue(connection, route, SyncOperation.Update);
            return route;
        }

        /// <summary>Puts a customer on another route, or on none with id 0.</summary>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        /// <exception cref="BusinessRuleException">The route does not exist.</exception>
        public static Customer MoveCustomer(SQLiteConnection connection, int customerId, int routeId)
        {
            var customer = connection.Find<Customer>(customerId)
                ?? throw new RecordNotFoundException(nameof(Customer), customerId);

            if (routeId != 0 && connection.Find<DeliveryRoute>(routeId) == null)
                throw new BusinessRuleException("The selected route no longer exists. Pick another route.");

            customer.RouteId = routeId;
            customer.UpdatedAt = DateTime.UtcNow;
            connection.Update(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Update);
            return customer;
        }

        /// <summary>Deletes a route that has no customers; otherwise they would silently drop off every delivery run.</summary>
        /// <exception cref="RecordNotFoundException">The route does not exist.</exception>
        /// <exception cref="BusinessRuleException">Customers are still on the route.</exception>
        public static void DeleteRoute(SQLiteConnection connection, int routeId)
        {
            var route = connection.Find<DeliveryRoute>(routeId)
                ?? throw new RecordNotFoundException(nameof(DeliveryRoute), routeId);

            int customers = connection.Table<Customer>().Where(c => c.RouteId == routeId).Count();
            if (customers > 0)
                throw new BusinessRuleException(
                    $"{customers} customer(s) are still on \"{route.RouteName}\". Move them to another route first.");

            connection.Delete(route);
            SyncQueueWriter.Enqueue(connection, route, SyncOperation.Delete);
        }

        private static void RequireActiveDeliveryStaff(SQLiteConnection connection, int userId)
        {
            if (userId == 0)
                return; // unassigned

            var user = connection.Find<User>(userId)
                ?? throw new BusinessRuleException("The selected delivery person no longer exists.");
            if (user.Role != UserRole.DeliveryBoy)
                throw new BusinessRuleException($"{user.Name} is not a delivery staff account.");
            if (!user.IsActive)
                throw new BusinessRuleException($"{user.Name}'s account is deactivated.");
        }
    }
}
