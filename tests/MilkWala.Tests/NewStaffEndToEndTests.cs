using MilkWala.Models;
using MilkWala.Services.Data;
using MilkWala.Services.Identity;
using Xunit;

namespace MilkWala.Tests;

/// <summary>The whole point of route management: a new delivery person with a new route sees exactly their own customers.</summary>
public class NewStaffEndToEndTests
{
    [Fact]
    public void A_new_delivery_person_signs_in_and_sees_only_the_customers_moved_onto_their_route()
    {
        using var db = new TestDb();
        var day = DateTime.Today;
        db.Connection.Insert(new Product { MilkType = MilkType.Cow, PricePerLitre = 62m });

        // Two customers who start on route A (run by someone else), each with a daily subscription.
        var amit = db.AddUser("Amit", mobile: "9123456789");
        Route(db, "Route A", amit.Id, out var routeA);
        var meera = Customer(db, "Meera", "9000000001", routeA);
        var zara = Customer(db, "Zara", "9000000002", routeA);

        // The vendor adds a delivery person and a route for them, then moves Meera onto it.
        User ravi = null!;
        DeliveryRoute routeB = null!;
        db.InTransaction(c =>
        {
            ravi = StaffLedger.AddDeliveryStaff(c, "Ravi", "+91 91234 56780");
            routeB = RouteLedger.AddRoute(c, "Route B", "East side", ravi.Id);
            RouteLedger.MoveCustomer(c, meera.Id, routeB.Id);
            DeliveryGenerator.EnsureForDay(c, day);
        });

        // Ravi signs in with his number and the delivery role.
        var found = UserLookup.Find("9123456780", UserRole.DeliveryBoy, db.Connection.Table<User>().ToList())!;
        var routes = RouteAssignment.RoutesFor(found.Id, db.Connection.Table<DeliveryRoute>().ToList());
        var mine = RouteAssignment.DeliveriesOnRoutes(
            db.Connection.Table<Delivery>().ToList(), db.Connection.Table<Customer>().ToList(), routes);

        Assert.Equal(new[] { "Meera" }, mine.Select(d => d.CustomerName));

        // Amit still has only Zara.
        var amitRoutes = RouteAssignment.RoutesFor(amit.Id, db.Connection.Table<DeliveryRoute>().ToList());
        var amits = RouteAssignment.DeliveriesOnRoutes(
            db.Connection.Table<Delivery>().ToList(), db.Connection.Table<Customer>().ToList(), amitRoutes);
        Assert.Equal(new[] { "Zara" }, amits.Select(d => d.CustomerName));
    }

    private static void Route(TestDb db, string name, int staffId, out int routeId)
    {
        var route = new DeliveryRoute { RouteName = name, AssignedDeliveryBoyId = staffId, SequenceOrder = 1 };
        db.Connection.Insert(route);
        routeId = route.Id;
    }

    private static Customer Customer(TestDb db, string name, string mobile, int routeId)
    {
        var customer = new Customer { Name = name, Mobile = mobile, RouteId = routeId, IsActive = true };
        db.Connection.Insert(customer);
        db.Connection.Insert(new Subscription
        {
            CustomerId = customer.Id, MilkType = MilkType.Cow, QuantityLitres = 1, StartDate = DateTime.Today.AddDays(-5)
        });
        return customer;
    }
}
