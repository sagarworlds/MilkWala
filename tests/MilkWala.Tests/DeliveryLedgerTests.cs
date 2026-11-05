using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class DeliveryLedgerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 7, 15, 0);

    private static DeliveryChange Change(TestDb db, Delivery delivery, DeliveryStatus status)
    {
        DeliveryChange change = null!;
        db.InTransaction(c => change = DeliveryLedger.ChangeStatus(c, delivery.Id, status, Now));
        return change;
    }

    [Fact]
    public void Delivering_charges_the_wallet_when_it_can_cover_the_price()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);
        var delivery = db.AddDelivery(customer, 1.5, 62m); // 93

        var change = Change(db, delivery, DeliveryStatus.Delivered);

        Assert.Equal(93m, change.ChargedToWallet);
        Assert.Equal(0m, change.AddedToDues);
        var after = db.Reload(customer);
        Assert.Equal(407m, after.WalletBalance);
        Assert.Equal(0m, after.OutstandingBalance);
        var stored = db.Connection.Get<Delivery>(delivery.Id);
        Assert.Equal(DeliveryStatus.Delivered, stored.Status);
        Assert.Equal(Now, stored.DeliveredAt);
        Assert.Equal(93m, stored.AmountChargedToWallet);
        var tx = db.Connection.Table<WalletTransaction>().Single();
        Assert.Equal(-93m, tx.Amount);
        Assert.Contains("1.5 L Cow", tx.Description);
    }

    [Fact]
    public void A_short_wallet_pays_what_it_can_and_the_rest_becomes_dues()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(30m);
        var delivery = db.AddDelivery(customer, 1.5, 62m); // 93

        var change = Change(db, delivery, DeliveryStatus.Delivered);

        Assert.Equal(30m, change.ChargedToWallet);
        Assert.Equal(63m, change.AddedToDues);
        var after = db.Reload(customer);
        Assert.Equal(0m, after.WalletBalance);
        Assert.Equal(63m, after.OutstandingBalance);
        Assert.Contains("₹63.00 added to dues", change.Summary());
    }

    [Fact]
    public void An_empty_wallet_puts_the_whole_price_on_dues_without_a_wallet_entry()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var delivery = db.AddDelivery(customer, 2, 50m);

        Change(db, delivery, DeliveryStatus.Delivered);

        Assert.Equal(100m, db.Reload(customer).OutstandingBalance);
        Assert.Equal(0, db.Count<WalletTransaction>());
    }

    [Fact]
    public void Tapping_delivered_twice_charges_only_once()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);
        var delivery = db.AddDelivery(customer, 1, 60m);

        Change(db, delivery, DeliveryStatus.Delivered);
        var second = Change(db, delivery, DeliveryStatus.Delivered);

        Assert.Equal(0m, second.ChargedToWallet);
        Assert.Equal(440m, db.Reload(customer).WalletBalance);
        Assert.Equal(1, db.Count<WalletTransaction>());
    }

    [Fact]
    public void Skipping_or_a_holiday_never_charges()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);

        Change(db, db.AddDelivery(customer, 1, 60m), DeliveryStatus.Skipped);
        Change(db, db.AddDelivery(customer, 1, 60m), DeliveryStatus.Holiday);

        Assert.Equal(500m, db.Reload(customer).WalletBalance);
        Assert.Equal(0, db.Count<WalletTransaction>());
    }

    [Fact]
    public void Correcting_delivered_to_skipped_refunds_the_wallet_exactly()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);
        var delivery = db.AddDelivery(customer, 1.5, 62m);
        Change(db, delivery, DeliveryStatus.Delivered);

        var change = Change(db, delivery, DeliveryStatus.Skipped);

        Assert.Equal(93m, change.RefundedToWallet);
        Assert.Equal(500m, db.Reload(customer).WalletBalance);
        var stored = db.Connection.Get<Delivery>(delivery.Id);
        Assert.Equal(0m, stored.AmountChargedToWallet);
        Assert.Equal(default, stored.DeliveredAt); // skipping does not keep a delivery time
    }

    [Fact]
    public void Correcting_a_delivery_that_went_on_dues_removes_the_dues()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(30m);
        var delivery = db.AddDelivery(customer, 1.5, 62m);
        Change(db, delivery, DeliveryStatus.Delivered); // 30 wallet + 63 dues

        Change(db, delivery, DeliveryStatus.Skipped);

        var after = db.Reload(customer);
        Assert.Equal(30m, after.WalletBalance);
        Assert.Equal(0m, after.OutstandingBalance);
    }

    [Fact]
    public void Correcting_after_the_customer_already_paid_the_dues_refunds_that_part_to_the_wallet()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var delivery = db.AddDelivery(customer, 1, 100m);
        Change(db, delivery, DeliveryStatus.Delivered); // 100 on dues
        db.InTransaction(c => WalletLedger.Recharge(c, customer.Id, 100m, PaymentMode.UPI, "T1", "Recharge")); // pays the dues
        Assert.Equal(0m, db.Reload(customer).OutstandingBalance);

        Change(db, delivery, DeliveryStatus.Skipped);

        var after = db.Reload(customer);
        Assert.Equal(0m, after.OutstandingBalance); // never negative
        Assert.Equal(100m, after.WalletBalance);    // they paid for milk they did not get
    }

    [Fact]
    public void Changes_are_queued_for_sync()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);
        var delivery = db.AddDelivery(customer, 1, 60m);

        Change(db, delivery, DeliveryStatus.Delivered);

        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).Distinct().OrderBy(t => t).ToList();
        Assert.Equal(new[] { "Customers", "Deliveries", "WalletTransactions" }, tables);
    }

    [Fact]
    public void A_failure_midway_leaves_the_delivery_and_money_untouched()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(500m);
        var delivery = db.AddDelivery(customer, 1, 60m);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            DeliveryLedger.ChangeStatus(c, delivery.Id, DeliveryStatus.Delivered, Now);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(500m, db.Reload(customer).WalletBalance);
        Assert.Equal(DeliveryStatus.Pending, db.Connection.Get<Delivery>(delivery.Id).Status);
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Unknown_delivery_or_customer_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => DeliveryLedger.ChangeStatus(db.Connection, 999, DeliveryStatus.Delivered, Now));

        var orphan = new Delivery { CustomerId = 404, QuantityLitres = 1, RatePerLitre = 50m, Status = DeliveryStatus.Pending };
        db.Connection.Insert(orphan);
        Assert.Throws<RecordNotFoundException>(() => DeliveryLedger.ChangeStatus(db.Connection, orphan.Id, DeliveryStatus.Delivered, Now));
    }

    [Fact]
    public void Summary_describes_each_outcome()
    {
        var d = new Delivery { CustomerName = "Rahul", Status = DeliveryStatus.Skipped };
        Assert.Equal("Skipped for Rahul.", new DeliveryChange(d, 0, 0, 0, 0).Summary());

        d.Status = DeliveryStatus.Delivered;
        Assert.Equal("Delivered to Rahul. ₹93.00 charged to wallet.", new DeliveryChange(d, 93m, 0, 0, 0).Summary());
    }
}
