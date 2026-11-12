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
        Connection.CreateTable<Subscription>();
        Connection.CreateTable<Product>();
        Connection.CreateTable<Delivery>();
        Connection.CreateTable<Payment>();
        Connection.CreateTable<Complaint>();
        Connection.CreateTable<DeliveryRoute>();
        Connection.CreateTable<User>();
    }

    public Delivery AddDelivery(Customer customer, double litres, decimal rate, DeliveryStatus status = DeliveryStatus.Pending, DateTime? date = null)
    {
        var delivery = new Delivery
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            DeliveryDate = (date ?? DateTime.Today).Date,
            MilkType = MilkType.Cow,
            QuantityLitres = litres,
            RatePerLitre = rate,
            Status = status
        };
        Connection.Insert(delivery);
        return delivery;
    }

    public User AddUser(string name, UserRole role = UserRole.DeliveryBoy, string mobile = "9123456789", bool active = true)
    {
        var user = new User { Name = name, Role = role, MobileNumber = mobile, IsActive = active };
        Connection.Insert(user);
        return user;
    }

    public Customer Reload(Customer customer) => Connection.Get<Customer>(customer.Id);

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
