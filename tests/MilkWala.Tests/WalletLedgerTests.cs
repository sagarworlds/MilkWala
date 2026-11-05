using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class WalletLedgerTests
{
    [Fact]
    public void Credit_raises_the_balance_and_records_a_credit_transaction()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        WalletTransaction tx = null!;
        db.InTransaction(c => tx = WalletLedger.Credit(c, customer.Id, 500m, "Recharge"));

        Assert.Equal(600m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(500m, tx.Amount);
        Assert.Equal("Credit", tx.TransactionType);
        Assert.Equal(600m, tx.BalanceAfter);
        Assert.Equal(1, db.Count<WalletTransaction>());
    }

    [Fact]
    public void Credit_queues_both_the_customer_update_and_the_transaction_for_sync()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        db.InTransaction(c => WalletLedger.Credit(c, customer.Id, 50m, "Recharge"));

        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).OrderBy(t => t).ToList();
        Assert.Equal(new[] { "Customers", "WalletTransactions" }, tables);
    }

    [Fact]
    public void Debit_lowers_the_balance_and_records_a_negative_debit_transaction()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        WalletTransaction tx = null!;
        db.InTransaction(c => tx = WalletLedger.Debit(c, customer.Id, 40m, "Bought curd"));

        Assert.Equal(60m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(-40m, tx.Amount);
        Assert.Equal("Debit", tx.TransactionType);
        Assert.Equal(60m, tx.BalanceAfter);
    }

    [Fact]
    public void Debit_beyond_the_balance_throws_and_changes_nothing()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(30m);

        var ex = Assert.Throws<InsufficientBalanceException>(
            () => db.InTransaction(c => WalletLedger.Debit(c, customer.Id, 40m, "Too much")));

        Assert.Equal(30m, ex.Available);
        Assert.Equal(40m, ex.Required);
        Assert.Equal(30m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(0, db.Count<WalletTransaction>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Debit_of_the_exact_balance_is_allowed()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(40m);

        db.InTransaction(c => WalletLedger.Debit(c, customer.Id, 40m, "Exact"));

        Assert.Equal(0m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
    }

    [Fact]
    public void A_failure_after_the_ledger_step_rolls_back_the_whole_unit_of_work()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            WalletLedger.Credit(c, customer.Id, 500m, "Recharge");
            throw new InvalidOperationException("something later in the same operation failed");
        }));

        Assert.Equal(100m, db.Connection.Get<Customer>(customer.Id).WalletBalance);
        Assert.Equal(0, db.Count<WalletTransaction>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_amounts_are_rejected(int amount)
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => WalletLedger.Credit(db.Connection, customer.Id, amount, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => WalletLedger.Debit(db.Connection, customer.Id, amount, "x"));
    }

    [Fact]
    public void Unknown_customer_is_reported_instead_of_ignored()
    {
        using var db = new TestDb();

        var ex = Assert.Throws<RecordNotFoundException>(() => WalletLedger.Credit(db.Connection, 999, 10m, "x"));

        Assert.Equal(999, ex.RecordId);
        Assert.Equal(nameof(Customer), ex.EntityName);
    }
}
