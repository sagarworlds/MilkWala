using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class StoreOrderLedgerTests
{
    private static DairyProduct Paneer(decimal price = 95m) => new() { Id = 7, Name = "Malai Paneer", Price = price };

    [Fact]
    public void Place_charges_the_wallet_and_creates_the_order_together()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(200m);

        StoreOrder order = null!;
        db.InTransaction(c => order = StoreOrderLedger.Place(c, customer.Id, Paneer()));

        Assert.Equal(105m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(7, order.ProductId);
        Assert.Equal(95m, order.TotalPrice);
        Assert.Equal(1, db.Count<StoreOrder>());
        Assert.Equal(1, db.Count<WalletTransaction>());
        // customer update + wallet transaction + order
        Assert.Equal(3, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Place_with_too_little_balance_writes_nothing()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(50m);

        Assert.Throws<InsufficientBalanceException>(
            () => db.InTransaction(c => StoreOrderLedger.Place(c, customer.Id, Paneer())));

        Assert.Equal(50m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(0, db.Count<StoreOrder>());
        Assert.Equal(0, db.Count<WalletTransaction>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Two_orders_cannot_spend_the_same_money_twice()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        db.InTransaction(c => StoreOrderLedger.Place(c, customer.Id, Paneer()));
        // The second order sees the balance left by the first (5), not a stale 100.
        Assert.Throws<InsufficientBalanceException>(
            () => db.InTransaction(c => StoreOrderLedger.Place(c, customer.Id, Paneer())));

        Assert.Equal(5m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(1, db.Count<StoreOrder>());
    }

    [Fact]
    public void Place_rejects_a_missing_product()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);
        Assert.Throws<ArgumentNullException>(() => StoreOrderLedger.Place(db.Connection, customer.Id, null!));
    }
}
