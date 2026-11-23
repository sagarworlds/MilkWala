using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class CustomerLedgerTests
{
    private static readonly DateTime Today = new(2026, 11, 12);

    private static (Customer Customer, Subscription Sub) Seed(TestDb db, string mobile = "9822334455")
    {
        var customer = new Customer { Name = "Meera Joshi", Mobile = mobile, Address = "Flat 7", IsActive = true, RouteId = 0 };
        db.Connection.Insert(customer);
        var sub = new Subscription { CustomerId = customer.Id, MilkType = MilkType.Cow, QuantityLitres = 1, StartDate = Today.AddDays(-9) };
        db.Connection.Insert(sub);
        return (customer, sub);
    }

    private static CustomerEdit Edit(Customer customer, string? name = null, string? mobile = null, string address = "Flat 9, Lake View",
        MilkType milk = MilkType.Buffalo, double litres = 2, int routeId = 0) =>
        new(customer.Id, name ?? customer.Name, mobile ?? customer.Mobile, address, milk, litres, routeId, Today);

    private static void Update(TestDb db, CustomerEdit edit) => db.InTransaction(c => CustomerLedger.Update(c, edit));

    [Fact]
    public void Edits_the_details_route_and_plan_and_queues_both_records()
    {
        using var db = new TestDb();
        var (customer, sub) = Seed(db);
        db.Connection.Insert(new DeliveryRoute { RouteName = "Route A" });
        var route = db.Connection.Table<DeliveryRoute>().Single();

        Update(db, Edit(customer, name: "  Meera J  ", routeId: route.Id));

        var stored = db.Reload(customer);
        Assert.Equal("Meera J", stored.Name);
        Assert.Equal("Flat 9, Lake View", stored.Address);
        Assert.Equal(route.Id, stored.RouteId);
        Assert.Equal(MilkType.Buffalo, stored.PreferredMilkType);
        Assert.Equal(2, stored.PreferredQuantity);
        var storedSub = db.Connection.Get<Subscription>(sub.Id);
        Assert.Equal(MilkType.Buffalo, storedSub.MilkType);
        Assert.Equal(2, storedSub.QuantityLitres);
        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).Distinct().OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "Customers", "Subscriptions" }, tables);
    }

    [Fact]
    public void Keeping_your_own_mobile_is_fine_but_taking_another_customers_is_refused()
    {
        using var db = new TestDb();
        var (meera, _) = Seed(db, "9822334455");
        var other = new Customer { Name = "Priya", Mobile = "9811112222", IsActive = true };
        db.Connection.Insert(other);

        Update(db, Edit(meera, mobile: "+91 98223 34455")); // same number, other format: allowed

        var ex = Assert.Throws<BusinessRuleException>(() => Update(db, Edit(meera, mobile: "9811112222")));
        Assert.Contains("Priya", ex.Message);
        Assert.Equal("9822334455", db.Reload(meera).Mobile);
    }

    [Theory]
    [InlineData("", "9822334455", 2, "name")]
    [InlineData("Meera", "123", 2, "10-digit")]
    [InlineData("Meera", "9822334455", 0, "quantity")]
    public void Invalid_edits_are_refused_and_change_nothing(string name, string mobile, double litres, string messagePart)
    {
        using var db = new TestDb();
        var (customer, sub) = Seed(db);

        var ex = Assert.Throws<BusinessRuleException>(() => Update(db, Edit(customer, name: name, mobile: mobile, litres: litres)));

        Assert.Contains(messagePart, ex.Message);
        Assert.Equal("Meera Joshi", db.Reload(customer).Name);
        Assert.Equal(1, db.Connection.Get<Subscription>(sub.Id).QuantityLitres);
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void A_route_that_no_longer_exists_is_refused()
    {
        using var db = new TestDb();
        var (customer, _) = Seed(db);

        Assert.Throws<BusinessRuleException>(() => Update(db, Edit(customer, routeId: 42)));
    }

    [Fact]
    public void A_customer_without_a_subscription_gets_one_starting_tomorrow()
    {
        using var db = new TestDb();
        var customer = new Customer { Name = "Old record", Mobile = "9822334455", IsActive = true };
        db.Connection.Insert(customer);

        Update(db, Edit(customer, milk: MilkType.Toned, litres: 1.5));

        var sub = db.Connection.Table<Subscription>().Single();
        Assert.Equal(customer.Id, sub.CustomerId);
        Assert.Equal(Today.AddDays(1), sub.StartDate);
        Assert.Equal(MilkType.Toned, sub.MilkType);
    }

    [Fact]
    public void The_sign_in_account_follows_a_changed_mobile_number()
    {
        using var db = new TestDb();
        var user = db.AddUser("Meera", UserRole.Customer, mobile: "9822334455");
        var (customer, _) = Seed(db);
        customer.UserId = user.Id;
        db.Connection.Update(customer);

        Update(db, Edit(customer, mobile: "9000000009"));

        Assert.Equal("9000000009", db.Connection.Get<User>(user.Id).MobileNumber);
        Assert.Contains(db.Connection.Table<SyncQueueItem>().ToList(), q => q.TableName == "Users");
    }

    [Fact]
    public void An_unchanged_mobile_leaves_the_account_alone()
    {
        using var db = new TestDb();
        var user = db.AddUser("Meera", UserRole.Customer, mobile: "9822334455");
        var (customer, _) = Seed(db);
        customer.UserId = user.Id;
        db.Connection.Update(customer);

        Update(db, Edit(customer));

        Assert.DoesNotContain(db.Connection.Table<SyncQueueItem>().ToList(), q => q.TableName == "Users");
    }

    [Fact]
    public void Pending_deliveries_from_today_show_the_new_name_and_address_but_history_is_untouched()
    {
        using var db = new TestDb();
        var (customer, _) = Seed(db);
        var today = db.AddDelivery(customer, 1, 60m, DeliveryStatus.Pending, Today);
        var tomorrow = db.AddDelivery(customer, 1, 60m, DeliveryStatus.Pending, Today.AddDays(1));
        var delivered = db.AddDelivery(customer, 1, 60m, DeliveryStatus.Delivered, Today);
        var past = db.AddDelivery(customer, 1, 60m, DeliveryStatus.Pending, Today.AddDays(-2));

        Update(db, Edit(customer, name: "Meera Joshi-Rao", address: "New address"));

        Assert.Equal("New address", db.Connection.Get<Delivery>(today.Id).Address);
        Assert.Equal("Meera Joshi-Rao", db.Connection.Get<Delivery>(tomorrow.Id).CustomerName);
        Assert.Equal("Meera Joshi", db.Connection.Get<Delivery>(delivered.Id).CustomerName); // already delivered
        Assert.Equal("Meera Joshi", db.Connection.Get<Delivery>(past.Id).CustomerName);      // before today
    }

    [Fact]
    public void An_unknown_customer_is_reported_and_a_failure_rolls_everything_back()
    {
        using var db = new TestDb();
        var (customer, sub) = Seed(db);

        Assert.Throws<RecordNotFoundException>(() => CustomerLedger.Update(db.Connection, new CustomerEdit(999, "x", "9822334455", "", MilkType.Cow, 1, 0, Today)));

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            CustomerLedger.Update(c, Edit(customer, name: "Changed"));
            throw new InvalidOperationException("later step failed");
        }));
        Assert.Equal("Meera Joshi", db.Reload(customer).Name);
        Assert.Equal(1, db.Connection.Get<Subscription>(sub.Id).QuantityLitres);
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }
}
