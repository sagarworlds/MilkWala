using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class DeliveryGeneratorTests
{
    private static readonly DateTime Day = new(2026, 11, 5);

    private static void Seed(TestDb db)
    {
        db.Connection.Insert(new Product { MilkType = MilkType.Cow, PricePerLitre = 62m });
        var rahul = db.AddCustomer(100m);
        db.Connection.Insert(new Subscription { CustomerId = rahul.Id, MilkType = MilkType.Cow, QuantityLitres = 1.5, StartDate = Day.AddDays(-5) });
    }

    [Fact]
    public void Creates_the_days_deliveries_and_queues_them_for_sync()
    {
        using var db = new TestDb();
        Seed(db);

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));

        var delivery = db.Connection.Table<Delivery>().Single();
        Assert.Equal(Day, delivery.DeliveryDate);
        Assert.Equal(93m, delivery.TotalPrice);
        var queued = db.Connection.Table<SyncQueueItem>().Single();
        Assert.Equal("Deliveries", queued.TableName);
        Assert.Equal(SyncOperation.Insert, queued.Operation);
    }

    [Fact]
    public void Calling_it_again_never_creates_duplicates()
    {
        using var db = new TestDb();
        Seed(db);

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));
        var second = (Services.Scheduling.DeliveryPlan?)null;
        db.InTransaction(c => second = DeliveryGenerator.EnsureForDay(c, Day.AddHours(18)));

        Assert.Equal(1, db.Count<Delivery>());
        Assert.Empty(second!.NewDeliveries);
        Assert.Equal(1, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void A_delivery_already_marked_delivered_today_is_left_alone()
    {
        using var db = new TestDb();
        Seed(db);
        var customer = db.Connection.Table<Customer>().Single();
        db.AddDelivery(customer, 1.5, 62m, DeliveryStatus.Delivered, Day);

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));

        Assert.Equal(DeliveryStatus.Delivered, db.Connection.Table<Delivery>().Single().Status);
    }

    [Fact]
    public void A_failure_after_generating_rolls_the_whole_day_back()
    {
        using var db = new TestDb();
        Seed(db);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            DeliveryGenerator.EnsureForDay(c, Day);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(0, db.Count<Delivery>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }
}
