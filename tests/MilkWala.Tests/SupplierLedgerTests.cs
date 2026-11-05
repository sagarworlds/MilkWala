using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class SupplierLedgerTests
{
    private static FarmerSupplier Add(TestDb db, string name = "Ramesh Farm", string mobile = "9811223344", double fat = 4.3, decimal rate = 44m)
    {
        FarmerSupplier supplier = null!;
        db.InTransaction(c => supplier = SupplierLedger.Add(c, name, mobile, MilkType.Cow, fat, rate));
        return supplier;
    }

    private static MilkProcurement Buy(TestDb db, FarmerSupplier supplier, double litres, decimal rate, DateTime date)
    {
        MilkProcurement purchase = null!;
        db.InTransaction(c => purchase = ProcurementLedger.Record(c, supplier.Id, litres, 4.5, 8.5, rate, date));
        return purchase;
    }

    private static SupplierLedger.PayoutResult Pay(TestDb db, FarmerSupplier supplier, decimal amount)
    {
        SupplierLedger.PayoutResult result = null!;
        db.InTransaction(c => result = SupplierLedger.Pay(c, supplier.Id, amount));
        return result;
    }

    // ---- Add

    [Fact]
    public void Add_registers_the_farmer_with_a_normalised_mobile_and_queues_it()
    {
        using var db = new TestDb();

        var supplier = Add(db, "  Ramesh Farm  ", "+91 98112-23344");

        var stored = db.Connection.Get<FarmerSupplier>(supplier.Id);
        Assert.Equal("Ramesh Farm", stored.Name);
        Assert.Equal("9811223344", stored.Mobile);
        Assert.Equal("FarmerSuppliers", db.Connection.Table<SyncQueueItem>().Single().TableName);
    }

    [Theory]
    [InlineData("", "9811223344", 4.3, 44, "name")]
    [InlineData("Farm", "123", 4.3, 44, "10-digit")]
    [InlineData("Farm", "9811223344", 0, 44, "Fat")]
    [InlineData("Farm", "9811223344", 16, 44, "Fat")]
    [InlineData("Farm", "9811223344", 4.3, 0, "rate")]
    public void Add_rejects_invalid_input(string name, string mobile, double fat, decimal rate, string messagePart)
    {
        using var db = new TestDb();

        var ex = Assert.Throws<BusinessRuleException>(
            () => db.InTransaction(c => SupplierLedger.Add(c, name, mobile, MilkType.Cow, fat, rate)));

        Assert.Contains(messagePart, ex.Message);
        Assert.Equal(0, db.Count<FarmerSupplier>());
    }

    [Fact]
    public void Add_rejects_a_mobile_that_is_already_registered_even_in_another_format()
    {
        using var db = new TestDb();
        Add(db, "Ramesh Farm", "9811223344");

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, "Someone Else", "+91 98112 23344"));

        Assert.Contains("Ramesh Farm", ex.Message);
        Assert.Equal(1, db.Count<FarmerSupplier>());
    }

    // ---- Pay

    [Fact]
    public void Paying_everything_settles_all_purchases_and_clears_the_balance()
    {
        using var db = new TestDb();
        var farm = Add(db);
        Buy(db, farm, 100, 44m, new DateTime(2026, 11, 1)); // 4400
        Buy(db, farm, 50, 44m, new DateTime(2026, 11, 2));  // 2200

        var result = Pay(db, farm, 6600m);

        Assert.Equal(2, result.PurchasesSettled);
        Assert.Equal(0m, db.Connection.Get<FarmerSupplier>(farm.Id).OutstandingPayout);
        Assert.All(db.Connection.Table<MilkProcurement>().ToList(), p => Assert.True(p.IsPaid));
    }

    [Fact]
    public void A_part_payment_settles_the_oldest_purchase_first()
    {
        using var db = new TestDb();
        var farm = Add(db);
        var oldest = Buy(db, farm, 100, 44m, new DateTime(2026, 11, 1)); // 4400
        var newest = Buy(db, farm, 50, 44m, new DateTime(2026, 11, 2));  // 2200

        var result = Pay(db, farm, 5000m);

        Assert.Equal(1, result.PurchasesSettled);
        var a = db.Connection.Get<MilkProcurement>(oldest.Id);
        var b = db.Connection.Get<MilkProcurement>(newest.Id);
        Assert.True(a.IsPaid);
        Assert.Equal(4400m, a.AmountPaid);
        Assert.False(b.IsPaid);          // only part of it is covered...
        Assert.Equal(600m, b.AmountPaid); // ...and the part is remembered
        Assert.Equal(1600m, db.Connection.Get<FarmerSupplier>(farm.Id).OutstandingPayout);
    }

    [Fact]
    public void A_second_payment_continues_where_the_first_stopped()
    {
        using var db = new TestDb();
        var farm = Add(db);
        var purchase = Buy(db, farm, 100, 44m, new DateTime(2026, 11, 1)); // 4400

        Pay(db, farm, 1000m);
        var second = Pay(db, farm, 3400m);

        Assert.Equal(1, second.PurchasesSettled);
        var stored = db.Connection.Get<MilkProcurement>(purchase.Id);
        Assert.True(stored.IsPaid);
        Assert.Equal(4400m, stored.AmountPaid);
    }

    [Fact]
    public void An_opening_balance_without_purchase_records_can_still_be_paid()
    {
        using var db = new TestDb();
        var farm = Add(db);
        farm.OutstandingPayout = 1200m; // carried over from before the app, no purchase rows
        db.Connection.Update(farm);

        var result = Pay(db, farm, 1200m);

        Assert.Equal(0, result.PurchasesSettled);
        Assert.Equal(0m, db.Connection.Get<FarmerSupplier>(farm.Id).OutstandingPayout);
    }

    [Fact]
    public void Paying_more_than_is_owed_is_refused_and_changes_nothing()
    {
        using var db = new TestDb();
        var farm = Add(db);
        var purchase = Buy(db, farm, 10, 40m, new DateTime(2026, 11, 1)); // 400
        var queuedBefore = db.Count<SyncQueueItem>();

        var ex = Assert.Throws<BusinessRuleException>(() => Pay(db, farm, 500m));

        Assert.Contains("₹400.00", ex.Message);
        Assert.Equal(400m, db.Connection.Get<FarmerSupplier>(farm.Id).OutstandingPayout);
        Assert.Equal(0m, db.Connection.Get<MilkProcurement>(purchase.Id).AmountPaid);
        Assert.Equal(queuedBefore, db.Count<SyncQueueItem>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Non_positive_payments_are_refused(int amount)
    {
        using var db = new TestDb();
        var farm = Add(db);
        Assert.Throws<BusinessRuleException>(() => Pay(db, farm, amount));
    }

    [Fact]
    public void A_payment_is_all_or_nothing()
    {
        using var db = new TestDb();
        var farm = Add(db);
        var purchase = Buy(db, farm, 10, 40m, new DateTime(2026, 11, 1));

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            SupplierLedger.Pay(c, farm.Id, 400m);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(400m, db.Connection.Get<FarmerSupplier>(farm.Id).OutstandingPayout);
        Assert.False(db.Connection.Get<MilkProcurement>(purchase.Id).IsPaid);
    }

    [Fact]
    public void Unknown_farmer_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => SupplierLedger.Pay(db.Connection, 999, 10m));
    }
}
