using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class WalletRechargeTests
{
    private static WalletLedger.RechargeResult Recharge(TestDb db, Customer customer, decimal amount)
    {
        WalletLedger.RechargeResult result = null!;
        db.InTransaction(c => result = WalletLedger.Recharge(c, customer.Id, amount, PaymentMode.UPI, "TXN1234", "Online Recharge via UPI"));
        return result;
    }

    [Fact]
    public void Recharge_credits_the_wallet_and_records_a_collection_for_the_vendor()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        var result = Recharge(db, customer, 500m);

        Assert.Equal(600m, result.BalanceAfter);
        Assert.Equal(0m, result.DuesCleared);
        var payment = db.Connection.Table<Payment>().Single();
        Assert.Equal(500m, payment.Amount);
        Assert.Equal(PaymentMode.UPI, payment.Mode);
        Assert.Equal("TXN1234", payment.ReferenceNumber);
        Assert.Equal(customer.Id, payment.CustomerId);
        Assert.Equal(DateTime.Today, payment.PaymentDate.Date); // counts in "today's collection"
    }

    [Fact]
    public void Recharge_pays_off_outstanding_dues_first()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        customer.OutstandingBalance = 180m;
        db.Connection.Update(customer);

        var result = Recharge(db, customer, 500m);

        Assert.Equal(180m, result.DuesCleared);
        Assert.Equal(320m, result.BalanceAfter);
        var after = db.Reload(customer);
        Assert.Equal(0m, after.OutstandingBalance);
        Assert.Equal(320m, after.WalletBalance);
        // the credit and the dues payment both appear in the wallet history
        Assert.Equal(new[] { -180m, 500m }, db.Connection.Table<WalletTransaction>().ToList().Select(t => t.Amount).OrderBy(a => a).ToArray());
    }

    [Fact]
    public void A_small_recharge_only_partly_clears_the_dues()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        customer.OutstandingBalance = 180m;
        db.Connection.Update(customer);

        var result = Recharge(db, customer, 100m);

        Assert.Equal(100m, result.DuesCleared);
        var after = db.Reload(customer);
        Assert.Equal(80m, after.OutstandingBalance);
        Assert.Equal(0m, after.WalletBalance);
    }

    [Fact]
    public void Recharge_is_all_or_nothing()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            WalletLedger.Recharge(c, customer.Id, 500m, PaymentMode.UPI, "T", "Recharge");
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(100m, db.Reload(customer).WalletBalance);
        Assert.Equal(0, db.Count<Payment>());
        Assert.Equal(0, db.Count<WalletTransaction>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Recharge_rejects_non_positive_amounts()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        Assert.Throws<ArgumentOutOfRangeException>(() => Recharge(db, customer, 0m));
    }
}
