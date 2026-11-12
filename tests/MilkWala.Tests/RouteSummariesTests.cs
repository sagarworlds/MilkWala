using MilkWala.Models;
using MilkWala.Services.Identity;
using Xunit;

namespace MilkWala.Tests;

public class RouteSummariesTests
{
    private static DeliveryRoute Route(int id, string name, int boyId, int sequence) =>
        new() { Id = id, RouteName = name, AreaDescription = $"Area {id}", AssignedDeliveryBoyId = boyId, SequenceOrder = sequence };

    [Fact]
    public void Lists_routes_in_run_order_with_staff_and_customer_counts()
    {
        var routes = new[] { Route(1, "B", 3, 2), Route(2, "A", 3, 1) };
        var users = new[] { new User { Id = 3, Name = "Amit", Role = UserRole.DeliveryBoy } };
        var customers = new[]
        {
            new Customer { Id = 1, RouteId = 2 }, new Customer { Id = 2, RouteId = 2 }, new Customer { Id = 3, RouteId = 1 },
        };

        var rows = RouteSummaries.Build(routes, users, customers);

        Assert.Equal(new[] { "A", "B" }, rows.Select(r => r.RouteName));
        Assert.Equal(new[] { 2, 1 }, rows.Select(r => r.CustomerCount));
        Assert.All(rows, r => Assert.Equal("Amit", r.StaffName));
    }

    [Fact]
    public void Shows_unassigned_deactivated_and_removed_staff_distinctly()
    {
        var routes = new[] { Route(1, "None", 0, 1), Route(2, "Inactive", 4, 2), Route(3, "Gone", 99, 3) };
        var users = new[] { new User { Id = 4, Name = "Ravi", IsActive = false } };

        var rows = RouteSummaries.Build(routes, users, Array.Empty<Customer>());

        Assert.Equal("Unassigned", rows[0].StaffName);
        Assert.Equal("Ravi (deactivated)", rows[1].StaffName);
        Assert.Equal("Unknown (account removed)", rows[2].StaffName);
    }

    [Fact]
    public void A_route_with_no_customers_shows_zero()
    {
        var rows = RouteSummaries.Build(new[] { Route(1, "A", 0, 1) }, Array.Empty<User>(), new[] { new Customer { RouteId = 0 } });

        Assert.Equal(0, rows[0].CustomerCount);
        Assert.Equal("0 customers", rows[0].CustomerCountText);
    }

    [Theory]
    [InlineData(1, "1 customer")]
    [InlineData(5, "5 customers")]
    public void Customer_count_text_handles_singular_and_plural(int count, string expected)
    {
        Assert.Equal(expected, new RouteSummary(1, "A", "", "x", count).CustomerCountText);
    }
}
