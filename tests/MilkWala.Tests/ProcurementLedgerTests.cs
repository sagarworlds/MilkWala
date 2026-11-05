using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class ProcurementLedgerTests
{
    private static readonly DateTime When = new(2026, 11, 5, 6, 30, 0);

    [Fact]
    public void Record_stores_the_purchase_and_raises_the_supplier_payout()
    {
        using var db = new TestDb();
        var supplier = db.AddSupplier(outstandingPayout: 1000m);

        MilkProcurement proc = null!;
        db.InTransaction(c => proc = ProcurementLedger.Record(c, supplier.Id, 100, 4.5, 8.5, 44m, When));

        Assert.Equal(4400m, proc.TotalAmount);
        Assert.Equal(When, proc.Date);
        Assert.Equal("Test Farm", proc.SupplierName);
        Assert.Equal(5400m, db.Connection.Get<FarmerSupplier>(supplier.Id).OutstandingPayout);
        Assert.Equal(1, db.Count<MilkProcurement>());
    }

    [Fact]
    public void Record_queues_the_purchase_and_the_supplier_update_for_sync()
    {
        using var db = new TestDb();
        var supplier = db.AddSupplier();

        db.InTransaction(c => ProcurementLedger.Record(c, supplier.Id, 10, 4, 8.5, 40m, When));

        var tables = db.Connection.Table<SyncQueueItem>().ToList().Select(q => q.TableName).OrderBy(t => t).ToList();
        Assert.Equal(new[] { "FarmerSuppliers", "MilkProcurement" }, tables);
    }

    [Fact]
    public void A_failure_after_recording_rolls_back_purchase_payout_and_queue()
    {
        using var db = new TestDb();
        var supplier = db.AddSupplier(outstandingPayout: 1000m);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            ProcurementLedger.Record(c, supplier.Id, 100, 4.5, 8.5, 44m, When);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(1000m, db.Connection.Get<FarmerSupplier>(supplier.Id).OutstandingPayout);
        Assert.Equal(0, db.Count<MilkProcurement>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Unknown_supplier_and_non_positive_litres_are_rejected()
    {
        using var db = new TestDb();
        var supplier = db.AddSupplier();

        Assert.Throws<RecordNotFoundException>(() => ProcurementLedger.Record(db.Connection, 999, 10, 4, 8.5, 40m, When));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcurementLedger.Record(db.Connection, supplier.Id, 0, 4, 8.5, 40m, When));
    }
}
