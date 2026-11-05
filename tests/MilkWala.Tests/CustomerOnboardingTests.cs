using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class CustomerOnboardingTests
{
    private static readonly DateTime Tomorrow = new(2026, 11, 6);

    private static NewCustomerRequest Request(
        string name = "Meera Joshi",
        string mobile = "9822334455",
        double litres = 2,
        int routeId = 0,
        DateTime? start = null) =>
        new(name, mobile, "Flat 7, Lake View", MilkType.Buffalo, litres, routeId, start ?? Tomorrow);

    private static Customer Add(TestDb db, NewCustomerRequest request)
    {
        Customer customer = null!;
        db.InTransaction(c => customer = CustomerOnboarding.Add(c, request));
        return customer;
    }

    [Fact]
    public void Adds_the_customer_and_a_daily_subscription_together()
    {
        using var db = new TestDb();

        var customer = Add(db, Request());

        var stored = db.Connection.Get<Customer>(customer.Id);
        Assert.Equal("Meera Joshi", stored.Name);
        Assert.Equal("9822334455", stored.Mobile);
        Assert.True(stored.IsActive);
        var sub = db.Connection.Table<Subscription>().Single();
        Assert.Equal(customer.Id, sub.CustomerId);
        Assert.Equal(MilkType.Buffalo, sub.MilkType);
        Assert.Equal(2, sub.QuantityLitres);
        Assert.Equal("Daily", sub.DeliveryFrequency);
        Assert.Equal(Tomorrow, sub.StartDate);
    }

    [Fact]
    public void The_wallet_starts_empty_no_free_credit()
    {
        using var db = new TestDb();

        var customer = Add(db, Request());

        Assert.Equal(0m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(0, db.Count<WalletTransaction>());
    }

    [Fact]
    public void Both_records_are_queued_for_sync()
    {
        using var db = new TestDb();

        Add(db, Request());

        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "Customers", "Subscriptions" }, tables);
    }

    [Fact]
    public void The_mobile_is_stored_in_its_ten_digit_form()
    {
        using var db = new TestDb();

        var customer = Add(db, Request(mobile: "+91 98223-34455"));

        Assert.Equal("9822334455", db.Connection.Get<Customer>(customer.Id).Mobile);
    }

    [Fact]
    public void A_mobile_already_in_use_is_refused_in_any_format()
    {
        using var db = new TestDb();
        Add(db, Request());

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, Request(name: "Other", mobile: "+91 9822334455")));

        Assert.Contains("Meera Joshi", ex.Message);
        Assert.Equal(1, db.Count<Customer>());
        Assert.Equal(1, db.Count<Subscription>());
    }

    [Theory]
    [InlineData("", "9822334455", 2, "name")]
    [InlineData("Meera", "12345", 2, "10-digit")]
    [InlineData("Meera", "9822334455", 0, "quantity")]
    public void Invalid_input_is_refused_before_anything_is_saved(string name, string mobile, double litres, string messagePart)
    {
        using var db = new TestDb();

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, Request(name, mobile, litres)));

        Assert.Contains(messagePart, ex.Message);
        Assert.Equal(0, db.Count<Customer>());
        Assert.Equal(0, db.Count<Subscription>());
    }

    [Fact]
    public void A_route_that_does_not_exist_is_refused_but_no_route_is_fine()
    {
        using var db = new TestDb();

        Assert.Throws<BusinessRuleException>(() => Add(db, Request(routeId: 42)));

        Add(db, Request(routeId: 0));
        db.Connection.Insert(new DeliveryRoute { RouteName = "Route A" });
        var route = db.Connection.Table<DeliveryRoute>().Single();
        var customer = Add(db, Request(name: "Second", mobile: "9000011111", routeId: route.Id));
        Assert.Equal(route.Id, db.Connection.Get<Customer>(customer.Id).RouteId);
    }

    [Fact]
    public void A_failure_after_adding_rolls_back_customer_subscription_and_queue()
    {
        using var db = new TestDb();

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            CustomerOnboarding.Add(c, Request());
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(0, db.Count<Customer>());
        Assert.Equal(0, db.Count<Subscription>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void A_new_customer_gets_deliveries_from_their_start_date_not_before()
    {
        using var db = new TestDb();
        db.Connection.Insert(new Product { MilkType = MilkType.Buffalo, PricePerLitre = 76m });
        Add(db, Request(start: Tomorrow));

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Tomorrow.AddDays(-1)));
        Assert.Equal(0, db.Count<Delivery>());

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Tomorrow));
        var delivery = db.Connection.Table<Delivery>().Single();
        Assert.Equal("Meera Joshi", delivery.CustomerName);
        Assert.Equal(152m, delivery.TotalPrice); // 2 L x 76
    }
}
