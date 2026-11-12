using MilkWala.Models;
using MilkWala.Services.Identity;
using Xunit;

namespace MilkWala.Tests;

public class RouteAssignmentTests
{
    private static DeliveryRoute Route(int id, int boyId, int sequence) =>
        new() { Id = id, RouteName = $"Route {id}", AssignedDeliveryBoyId = boyId, SequenceOrder = sequence };

    private static Customer Cust(int id, int routeId) => new() { Id = id, Name = $"C{id}", RouteId = routeId };

    private static Delivery Del(int customerId, string name) => new() { CustomerId = customerId, CustomerName = name };

    [Fact]
    public void RoutesFor_returns_only_that_persons_routes_in_route_order()
    {
        var routes = new[] { Route(1, boyId: 3, sequence: 2), Route(2, boyId: 9, sequence: 1), Route(3, boyId: 3, sequence: 1) };

        var mine = RouteAssignment.RoutesFor(3, routes);

        Assert.Equal(new[] { 3, 1 }, mine.Select(r => r.Id));
    }

    [Fact]
    public void RoutesFor_is_empty_when_nothing_is_assigned()
    {
        Assert.Empty(RouteAssignment.RoutesFor(5, new[] { Route(1, 3, 1) }));
    }

    [Fact]
    public void Only_deliveries_on_my_routes_are_returned()
    {
        var mine = new[] { Route(1, 3, 1) };
        var customers = new[] { Cust(10, routeId: 1), Cust(11, routeId: 2) };
        var deliveries = new[] { Del(10, "Mine"), Del(11, "Someone else's") };

        var result = RouteAssignment.DeliveriesOnRoutes(deliveries, customers, mine);

        Assert.Equal(new[] { "Mine" }, result.Select(d => d.CustomerName));
    }

    [Fact]
    public void Deliveries_follow_route_order_then_customer_name()
    {
        var mine = new[] { Route(5, 3, 1), Route(2, 3, 2) }; // already in route order
        var customers = new[] { Cust(1, 2), Cust(2, 5), Cust(3, 5), Cust(4, 2) };
        var deliveries = new[] { Del(1, "Zara"), Del(2, "Meera"), Del(3, "anita"), Del(4, "Bina") };

        var result = RouteAssignment.DeliveriesOnRoutes(deliveries, customers, mine);

        Assert.Equal(new[] { "anita", "Meera", "Bina", "Zara" }, result.Select(d => d.CustomerName));
    }

    [Fact]
    public void Customers_with_no_route_or_unknown_customers_are_left_out()
    {
        var mine = new[] { Route(1, 3, 1) };
        var customers = new[] { Cust(10, routeId: 0), Cust(11, routeId: 1) };
        var deliveries = new[] { Del(10, "No route"), Del(11, "On route"), Del(99, "Unknown customer") };

        var result = RouteAssignment.DeliveriesOnRoutes(deliveries, customers, mine);

        Assert.Equal(new[] { "On route" }, result.Select(d => d.CustomerName));
    }

    [Fact]
    public void With_no_routes_the_person_gets_no_deliveries_not_everyone_s()
    {
        var customers = new[] { Cust(10, 1) };

        var result = RouteAssignment.DeliveriesOnRoutes(new[] { Del(10, "Anyone") }, customers, Array.Empty<DeliveryRoute>());

        Assert.Empty(result);
    }
}
