using MilkWala.Models;
using SQLite;

namespace MilkWala.Tests;

/// <summary>A throw-away in-memory SQLite database with the tables the ledgers touch.</summary>
internal sealed class TestDb : IDisposable
{
    public SQLiteConnection Connection { get; } = new(":memory:");

    public TestDb()
    {
        Connection.CreateTable<Customer>();
        Connection.CreateTable<WalletTransaction>();
        Connection.CreateTable<StoreOrder>();
        Connection.CreateTable<FarmerSupplier>();
        Connection.CreateTable<MilkProcurement>();
        Connection.CreateTable<SyncQueueItem>();
    }

    public Customer AddCustomer(decimal walletBalance)
    {
        var customer = new Customer { Name = "Test Customer", Mobile = "9876543210", WalletBalance = walletBalance };
        Connection.Insert(customer);
        return customer;
    }

    public FarmerSupplier AddSupplier(decimal outstandingPayout = 0m)
    {
        var supplier = new FarmerSupplier { Name = "Test Farm", OutstandingPayout = outstandingPayout };
        Connection.Insert(supplier);
        return supplier;
    }

    /// <summary>Same all-or-nothing behaviour as <c>LocalDbService.RunInTransactionAsync</c>.</summary>
    public void InTransaction(Action<SQLiteConnection> work) => Connection.RunInTransaction(() => work(Connection));

    public int Count<T>() where T : new() => Connection.Table<T>().Count();

    public void Dispose() => Connection.Dispose();
}
