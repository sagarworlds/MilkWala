using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class RouteLedgerTests
{
    private static DeliveryRoute Add(TestDb db, string name = "Route A", string area = "Sector 14", int staffId = 0)
    {
        DeliveryRoute route = null!;
        db.InTransaction(c => route = RouteLedger.AddRoute(c, name, area, staffId));
        return route;
    }

    // ---- AddRoute

    [Fact]
    public void Adding_a_route_stores_it_with_the_next_run_order_and_queues_it()
    {
        using var db = new TestDb();
        var staff = db.AddUser("Amit");

        var first = Add(db, "  Route A  ", " Sector 14 ", staff.Id);
        var second = Add(db, "Route B");

        var stored = db.Connection.Get<DeliveryRoute>(first.Id);
        Assert.Equal("Route A", stored.RouteName);
        Assert.Equal("Sector 14", stored.AreaDescription);
        Assert.Equal(staff.Id, stored.AssignedDeliveryBoyId);
        Assert.Equal(1, first.SequenceOrder);
        Assert.Equal(2, second.SequenceOrder);
        Assert.Equal(2, db.Connection.Table<SyncQueueItem>().Count(q => q.TableName == "Routes"));
    }

    [Fact]
    public void A_route_can_be_created_before_anyone_is_assigned()
    {
        using var db = new TestDb();

        var route = Add(db, staffId: 0);

        Assert.Equal(0, db.Connection.Get<DeliveryRoute>(route.Id).AssignedDeliveryBoyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_route_needs_a_name(string name)
    {
        using var db = new TestDb();

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, name));

        Assert.Contains("name", ex.Message);
        Assert.Equal(0, db.Count<DeliveryRoute>());
    }

    [Fact]
    public void Route_names_must_be_unique_ignoring_case_and_spaces()
    {
        using var db = new TestDb();
        Add(db, "Route A");

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, "  route a "));

        Assert.Contains("already exists", ex.Message);
        Assert.Equal(1, db.Count<DeliveryRoute>());
    }

    [Fact]
    public void Only_active_delivery_staff_can_be_assigned_when_adding()
    {
        using var db = new TestDb();
        var customer = db.AddUser("A customer", UserRole.Customer);
        var gone = db.AddUser("Left the job", active: false);

        Assert.Throws<BusinessRuleException>(() => Add(db, "R1", staffId: customer.Id));
        Assert.Contains("deactivated", Assert.Throws<BusinessRuleException>(() => Add(db, "R2", staffId: gone.Id)).Message);
        Assert.Throws<BusinessRuleException>(() => Add(db, "R3", staffId: 999));
        Assert.Equal(0, db.Count<DeliveryRoute>());
    }

    // ---- AssignStaff

    [Fact]
    public void Assigning_changes_who_runs_the_route_and_queues_the_change()
    {
        using var db = new TestDb();
        var amit = db.AddUser("Amit", mobile: "9123456789");
        var ravi = db.AddUser("Ravi", mobile: "9123456780");
        var route = Add(db, staffId: amit.Id);

        db.InTransaction(c => RouteLedger.AssignStaff(c, route.Id, ravi.Id));

        Assert.Equal(ravi.Id, db.Connection.Get<DeliveryRoute>(route.Id).AssignedDeliveryBoyId);
        Assert.Equal(SyncOperation.Update, db.Connection.Table<SyncQueueItem>().ToList().Last().Operation);
    }

    [Fact]
    public void Id_zero_takes_the_route_off_its_person()
    {
        using var db = new TestDb();
        var amit = db.AddUser("Amit");
        var route = Add(db, staffId: amit.Id);

        db.InTransaction(c => RouteLedger.AssignStaff(c, route.Id, 0));

        Assert.Equal(0, db.Connection.Get<DeliveryRoute>(route.Id).AssignedDeliveryBoyId);
    }

    [Fact]
    public void Assigning_the_wrong_kind_of_account_is_refused_and_changes_nothing()
    {
        using var db = new TestDb();
        var amit = db.AddUser("Amit");
        var vendor = db.AddUser("The vendor", UserRole.Vendor);
        var route = Add(db, staffId: amit.Id);
        var queued = db.Count<SyncQueueItem>();

        Assert.Throws<BusinessRuleException>(() => db.InTransaction(c => RouteLedger.AssignStaff(c, route.Id, vendor.Id)));

        Assert.Equal(amit.Id, db.Connection.Get<DeliveryRoute>(route.Id).AssignedDeliveryBoyId);
        Assert.Equal(queued, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Assigning_an_unknown_route_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => RouteLedger.AssignStaff(db.Connection, 999, 0));
    }

    // ---- MoveCustomer

    [Fact]
    public void Moving_a_customer_changes_their_route_and_queues_the_change()
    {
        using var db = new TestDb();
        var a = Add(db, "A");
        var b = Add(db, "B");
        var customer = db.AddCustomer(0m);
        customer.RouteId = a.Id;
        db.Connection.Update(customer);

        db.InTransaction(c => RouteLedger.MoveCustomer(c, customer.Id, b.Id));

        Assert.Equal(b.Id, db.Reload(customer).RouteId);
        Assert.Contains(db.Connection.Table<SyncQueueItem>().ToList(), q => q.TableName == "Customers" && q.Operation == SyncOperation.Update);
    }

    [Fact]
    public void A_customer_can_be_taken_off_all_routes()
    {
        using var db = new TestDb();
        var a = Add(db, "A");
        var customer = db.AddCustomer(0m);
        customer.RouteId = a.Id;
        db.Connection.Update(customer);

        db.InTransaction(c => RouteLedger.MoveCustomer(c, customer.Id, 0));

        Assert.Equal(0, db.Reload(customer).RouteId);
    }

    [Fact]
    public void Moving_to_a_route_that_does_not_exist_is_refused()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        Assert.Throws<BusinessRuleException>(() => RouteLedger.MoveCustomer(db.Connection, customer.Id, 42));
        Assert.Throws<RecordNotFoundException>(() => RouteLedger.MoveCustomer(db.Connection, 999, 0));
    }

    // ---- DeleteRoute

    [Fact]
    public void An_empty_route_can_be_deleted_and_the_deletion_is_queued()
    {
        using var db = new TestDb();
        var route = Add(db);

        db.InTransaction(c => RouteLedger.DeleteRoute(c, route.Id));

        Assert.Equal(0, db.Count<DeliveryRoute>());
        var queued = db.Connection.Table<SyncQueueItem>().ToList().Last();
        Assert.Equal("Routes", queued.TableName);
        Assert.Equal(SyncOperation.Delete, queued.Operation);
        Assert.Equal(route.Id, queued.RecordId);
    }

    [Fact]
    public void A_route_with_customers_cannot_be_deleted()
    {
        using var db = new TestDb();
        var route = Add(db, "Route A");
        var customer = db.AddCustomer(0m);
        customer.RouteId = route.Id;
        db.Connection.Update(customer);

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c => RouteLedger.DeleteRoute(c, route.Id)));

        Assert.Contains("1 customer(s)", ex.Message);
        Assert.Contains("Route A", ex.Message);
        Assert.Equal(1, db.Count<DeliveryRoute>());
    }

    [Fact]
    public void Deleting_an_unknown_route_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => RouteLedger.DeleteRoute(db.Connection, 999));
    }

    [Fact]
    public void A_failure_after_a_change_rolls_back_the_route_and_its_queue_entry()
    {
        using var db = new TestDb();

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            RouteLedger.AddRoute(c, "Route A", "", 0);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(0, db.Count<DeliveryRoute>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }
}
