using System.Text.Json;
using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class SyncQueueWriterTests
{
    [Fact]
    public void Enqueue_uses_the_mapped_table_name_and_primary_key()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        SyncQueueWriter.Enqueue(db.Connection, customer, SyncOperation.Update);

        var entry = Assert.Single(db.Connection.Table<SyncQueueItem>().ToList());
        Assert.Equal("Customers", entry.TableName); // from [Table("Customers")], not a caller-typed string
        Assert.Equal(customer.Id, entry.RecordId);
        Assert.Equal(SyncOperation.Update, entry.Operation);
        Assert.Equal(SyncStatus.Pending, entry.Status);
        // SQLite stores ticks, so Kind is lost on read-back; check the instant is UTC-based instead.
        Assert.InRange(entry.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Enqueue_serialises_the_runtime_type_of_the_entity()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(250.5m);

        SyncQueueWriter.Enqueue(db.Connection, customer, SyncOperation.Insert);

        var entry = db.Connection.Table<SyncQueueItem>().Single();
        using var json = JsonDocument.Parse(entry.PayloadJson);
        Assert.Equal("Test Customer", json.RootElement.GetProperty("Name").GetString());
        Assert.Equal(250.5m, json.RootElement.GetProperty("WalletBalance").GetDecimal());
    }

    [Fact]
    public void Enqueue_rejects_null_arguments()
    {
        using var db = new TestDb();
        Assert.Throws<ArgumentNullException>(() => SyncQueueWriter.Enqueue(null!, new Customer(), SyncOperation.Insert));
        Assert.Throws<ArgumentNullException>(() => SyncQueueWriter.Enqueue(db.Connection, null!, SyncOperation.Insert));
    }
}
