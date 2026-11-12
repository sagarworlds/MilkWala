using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class SubscriptionLedgerTests
{
    private static Subscription AddSub(TestDb db, Customer customer, MilkType milk = MilkType.Cow, double litres = 1.5)
    {
        var sub = new Subscription { CustomerId = customer.Id, MilkType = milk, QuantityLitres = litres, StartDate = DateTime.Today.AddDays(-5) };
        db.Connection.Insert(sub);
        return sub;
    }

    [Fact]
    public void A_customer_changing_their_plan_updates_the_subscription_and_their_remembered_preference()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var sub = AddSub(db, customer);

        db.InTransaction(c => SubscriptionLedger.SetPlan(c, customer.Id, MilkType.Buffalo, 3));

        var stored = db.Connection.Get<Subscription>(sub.Id);
        Assert.Equal(MilkType.Buffalo, stored.MilkType);
        Assert.Equal(3, stored.QuantityLitres);
        var after = db.Reload(customer);
        Assert.Equal(MilkType.Buffalo, after.PreferredMilkType);
        Assert.Equal(3, after.PreferredQuantity);
        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "Customers", "Subscriptions" }, tables);
    }

    [Fact]
    public void A_customer_without_a_subscription_cannot_create_one_themselves()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        var ex = Assert.Throws<BusinessRuleException>(() => db.InTransaction(c => SubscriptionLedger.SetPlan(c, customer.Id, MilkType.Cow, 1)));

        Assert.Contains("contact your vendor", ex.Message);
        Assert.Equal(0, db.Count<Subscription>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void The_vendor_path_can_create_a_missing_subscription_starting_when_given()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var start = new DateTime(2026, 11, 13, 9, 0, 0);

        db.InTransaction(c => SubscriptionLedger.SetSubscription(c, customer.Id, MilkType.Toned, 2, start));

        var sub = db.Connection.Table<Subscription>().Single();
        Assert.Equal(new DateTime(2026, 11, 13), sub.StartDate);
        Assert.Equal("Daily", sub.DeliveryFrequency);
        Assert.Equal(MilkType.Toned, sub.MilkType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void A_non_positive_quantity_is_refused(double litres)
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        AddSub(db, customer);

        Assert.Throws<BusinessRuleException>(() => SubscriptionLedger.SetPlan(db.Connection, customer.Id, MilkType.Cow, litres));
    }

    [Fact]
    public void An_unknown_customer_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => SubscriptionLedger.SetSubscription(db.Connection, 999, MilkType.Cow, 1));
    }

    [Fact]
    public void A_failure_after_the_change_rolls_back_the_subscription_and_the_preference()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var sub = AddSub(db, customer, MilkType.Cow, 1.5);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            SubscriptionLedger.SetPlan(c, customer.Id, MilkType.Buffalo, 3);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(MilkType.Cow, db.Connection.Get<Subscription>(sub.Id).MilkType);
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }
}
