using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class SpecialOrderLedgerTests
{
    private static readonly DateTime Today = new(2026, 11, 12, 15, 30, 0);
    private static readonly DateTime Tomorrow = new(2026, 11, 13);

    private static SpecialOrder Request(TestDb db, Customer customer, DateTime? date = null, double litres = 2,
        MilkType milk = MilkType.Cow, string? note = "Guests visiting")
    {
        SpecialOrder order = null!;
        db.InTransaction(c => order = SpecialOrderLedger.Request(c, customer.Id, milk, litres, date ?? Tomorrow, note, Today));
        return order;
    }

    [Fact]
    public void A_request_for_tomorrow_is_accepted_straight_away_and_queued_for_sync()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(100m);

        var order = Request(db, customer, note: "  Guests visiting  ");

        var stored = db.Connection.Get<SpecialOrder>(order.Id);
        Assert.True(stored.IsApproved);
        Assert.Equal(Tomorrow, stored.TargetDate);
        Assert.Equal(2, stored.ExtraQuantityLitres);
        Assert.Equal("Guests visiting", stored.SpecialInstruction);
        Assert.Equal(MilkType.Cow, stored.MilkType);
        var queued = db.Connection.Table<SyncQueueItem>().Single();
        Assert.Equal("Orders", queued.TableName);
        Assert.Equal(SyncOperation.Insert, queued.Operation);
    }

    [Fact]
    public void The_time_of_day_in_the_chosen_date_is_dropped()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        var order = Request(db, customer, date: Tomorrow.AddHours(17));

        Assert.Equal(Tomorrow, db.Connection.Get<SpecialOrder>(order.Id).TargetDate);
    }

    [Theory]
    [InlineData(0)]   // today
    [InlineData(-3)]  // the past
    public void Today_and_past_days_are_refused(int dayOffsetFromToday)
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        var ex = Assert.Throws<BusinessRuleException>(() => Request(db, customer, date: Today.Date.AddDays(dayOffsetFromToday)));

        Assert.Contains("a day ahead", ex.Message);
        Assert.Equal(0, db.Count<SpecialOrder>());
    }

    [Fact]
    public void The_window_is_thirty_days()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        Request(db, customer, date: Today.Date.AddDays(30)); // last allowed day
        var ex = Assert.Throws<BusinessRuleException>(() => Request(db, customer, date: Today.Date.AddDays(31), milk: MilkType.Buffalo));

        Assert.Contains("30 days", ex.Message);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.5)]
    public void The_quantity_must_be_between_half_a_litre_and_ten(double litres)
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        Assert.Throws<BusinessRuleException>(() => Request(db, customer, litres: litres));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(10)]
    public void The_limits_themselves_are_allowed(double litres)
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        Request(db, customer, litres: litres);

        Assert.Equal(1, db.Count<SpecialOrder>());
    }

    [Fact]
    public void The_same_milk_on_the_same_day_cannot_be_ordered_twice_but_other_milk_or_days_can()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        Request(db, customer);

        var ex = Assert.Throws<BusinessRuleException>(() => Request(db, customer));
        Assert.Contains("Cancel it first", ex.Message);

        Request(db, customer, milk: MilkType.Buffalo);
        Request(db, customer, date: Tomorrow.AddDays(1));
        Assert.Equal(3, db.Count<SpecialOrder>());
    }

    [Fact]
    public void An_unknown_customer_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => SpecialOrderLedger.Request(db.Connection, 999, MilkType.Cow, 1, Tomorrow, null, Today));
    }

    [Fact]
    public void A_customer_can_cancel_their_own_future_order_and_the_cancellation_is_queued()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);
        var order = Request(db, customer);

        db.InTransaction(c => SpecialOrderLedger.Cancel(c, order.Id, customer.Id, Today));

        Assert.Equal(0, db.Count<SpecialOrder>());
        var last = db.Connection.Table<SyncQueueItem>().ToList().Last();
        Assert.Equal(SyncOperation.Delete, last.Operation);
        Assert.Equal(order.Id, last.RecordId);
    }

    [Fact]
    public void Cancelling_is_refused_once_the_day_has_started_or_for_someone_elses_order()
    {
        using var db = new TestDb();
        var mine = db.AddCustomer(0m);
        var other = db.AddCustomer(0m);
        var future = Request(db, mine);
        var dueToday = new SpecialOrder { CustomerId = mine.Id, MilkType = MilkType.Cow, ExtraQuantityLitres = 1, TargetDate = Today.Date, IsApproved = true };
        db.Connection.Insert(dueToday);

        Assert.Contains("today", Assert.Throws<BusinessRuleException>(
            () => SpecialOrderLedger.Cancel(db.Connection, dueToday.Id, mine.Id, Today)).Message);
        Assert.Contains("another customer", Assert.Throws<BusinessRuleException>(
            () => SpecialOrderLedger.Cancel(db.Connection, future.Id, other.Id, Today)).Message);
        Assert.Equal(2, db.Count<SpecialOrder>());
    }

    [Fact]
    public void Cancelling_an_unknown_order_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => SpecialOrderLedger.Cancel(db.Connection, 999, 1, Today));
    }

    [Fact]
    public void A_failure_after_requesting_rolls_back_the_order_and_its_queue_entry()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer(0m);

        Assert.Throws<InvalidOperationException>(() => db.InTransaction(c =>
        {
            SpecialOrderLedger.Request(c, customer.Id, MilkType.Cow, 2, Tomorrow, null, Today);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Equal(0, db.Count<SpecialOrder>());
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }
}
