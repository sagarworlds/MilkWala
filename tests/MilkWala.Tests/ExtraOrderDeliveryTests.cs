using MilkWala.Models;
using MilkWala.Services.Data;
using MilkWala.Services.Reporting;
using MilkWala.Services.Scheduling;
using Xunit;

namespace MilkWala.Tests;

/// <summary>Extra-milk orders become their own deliveries for the day and are counted in the forecast.</summary>
public class ExtraOrderDeliveryTests
{
    private static readonly DateTime Day = new(2026, 11, 13);

    private static readonly Product[] Prices =
    {
        new() { MilkType = MilkType.Cow, PricePerLitre = 62m },
        new() { MilkType = MilkType.Buffalo, PricePerLitre = 76m },
    };

    private static Customer Cust(int id = 1, bool active = true) => new() { Id = id, Name = $"C{id}", Address = $"House {id}", IsActive = active };

    private static Subscription Sub(int customerId = 1, MilkType type = MilkType.Cow, double litres = 1.5) =>
        new() { CustomerId = customerId, MilkType = type, QuantityLitres = litres, StartDate = Day.AddDays(-30) };

    private static SpecialOrder Extra(int id = 1, int customerId = 1, MilkType type = MilkType.Cow, double litres = 2,
        bool approved = true, string note = "Guests visiting") =>
        new() { Id = id, CustomerId = customerId, MilkType = type, ExtraQuantityLitres = litres, TargetDate = Day, IsApproved = approved, SpecialInstruction = note };

    private static DeliveryPlan Plan(IEnumerable<Subscription> subs, IEnumerable<Customer> customers,
        IEnumerable<SpecialOrder> extras, IEnumerable<Delivery>? existing = null) =>
        DeliveryPlanner.Plan(Day, subs, customers, Prices, existing ?? Array.Empty<Delivery>(), extras);

    [Fact]
    public void An_extra_order_is_its_own_delivery_beside_the_subscription_one_even_for_the_same_milk()
    {
        var plan = Plan(new[] { Sub() }, new[] { Cust() }, new[] { Extra(id: 7) });

        Assert.Equal(2, plan.NewDeliveries.Count);
        var regular = plan.NewDeliveries.Single(d => d.SpecialOrderId == 0);
        var extra = plan.NewDeliveries.Single(d => d.SpecialOrderId == 7);
        Assert.Equal(1.5, regular.QuantityLitres);
        Assert.Equal(2, extra.QuantityLitres);
        Assert.Equal(62m, extra.RatePerLitre);
        Assert.Equal(DeliveryStatus.Pending, extra.Status);
        Assert.Equal("Extra order: Guests visiting", extra.Notes);
    }

    [Fact]
    public void An_extra_order_without_a_note_is_labelled_plainly()
    {
        var plan = Plan(new[] { Sub() }, new[] { Cust() }, new[] { Extra(note: "  ") });

        Assert.Equal("Extra order", plan.NewDeliveries.Single(d => d.SpecialOrderId > 0).Notes);
    }

    [Fact]
    public void A_customer_without_a_subscription_can_still_get_an_extra()
    {
        var plan = Plan(Array.Empty<Subscription>(), new[] { Cust() }, new[] { Extra() });

        Assert.Equal(2, plan.NewDeliveries.Single().QuantityLitres);
    }

    [Fact]
    public void Extras_still_arrive_on_a_day_the_subscription_is_on_hold()
    {
        var held = Sub();
        held.IsPaused = true;
        held.PauseStartDate = Day;
        held.PauseEndDate = Day;

        var plan = Plan(new[] { held }, new[] { Cust() }, new[] { Extra(id: 3) });

        Assert.Equal(DeliveryStatus.Holiday, plan.NewDeliveries.Single(d => d.SpecialOrderId == 0).Status);
        Assert.Equal(DeliveryStatus.Pending, plan.NewDeliveries.Single(d => d.SpecialOrderId == 3).Status);
    }

    [Fact]
    public void Planning_again_never_duplicates_an_extra()
    {
        var first = Plan(new[] { Sub() }, new[] { Cust() }, new[] { Extra(id: 7) });
        var second = Plan(new[] { Sub() }, new[] { Cust() }, new[] { Extra(id: 7) }, existing: first.NewDeliveries);

        Assert.Equal(2, first.NewDeliveries.Count);
        Assert.Empty(second.NewDeliveries);
    }

    [Fact]
    public void Unapproved_other_day_zero_litre_and_inactive_customer_extras_are_ignored()
    {
        var otherDay = Extra(id: 2);
        otherDay.TargetDate = Day.AddDays(1);
        var extras = new[] { Extra(id: 1, approved: false), otherDay, Extra(id: 3, litres: 0), Extra(id: 4, customerId: 2) };

        var plan = Plan(new[] { Sub() }, new[] { Cust(1), Cust(2, active: false) }, extras);

        Assert.Equal(new[] { 0 }, plan.NewDeliveries.Select(d => d.SpecialOrderId)); // only the subscription delivery
    }

    [Fact]
    public void An_extra_for_a_milk_type_without_a_price_is_reported_not_guessed()
    {
        var plan = Plan(new[] { Sub() }, new[] { Cust() }, new[] { Extra(type: MilkType.Organic) });

        Assert.Single(plan.NewDeliveries);
        Assert.Contains("Organic", Assert.Single(plan.Warnings));
    }

    [Fact]
    public void The_generator_reads_extra_orders_from_the_database_once()
    {
        using var db = new TestDb();
        db.Connection.Insert(new Product { MilkType = MilkType.Cow, PricePerLitre = 62m });
        var customer = db.AddCustomer(0m);
        db.Connection.Insert(new SpecialOrder { CustomerId = customer.Id, MilkType = MilkType.Cow, ExtraQuantityLitres = 3, TargetDate = Day, IsApproved = true });

        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));
        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));

        var delivery = db.Connection.Table<Delivery>().Single();
        Assert.Equal(3, delivery.QuantityLitres);
        Assert.True(delivery.SpecialOrderId > 0);
    }

    [Fact]
    public void An_extra_delivery_is_billed_like_any_other_when_delivered()
    {
        using var db = new TestDb();
        db.Connection.Insert(new Product { MilkType = MilkType.Cow, PricePerLitre = 60m });
        var customer = db.AddCustomer(500m);
        db.Connection.Insert(new SpecialOrder { CustomerId = customer.Id, MilkType = MilkType.Cow, ExtraQuantityLitres = 2, TargetDate = Day, IsApproved = true });
        db.InTransaction(c => DeliveryGenerator.EnsureForDay(c, Day));
        var delivery = db.Connection.Table<Delivery>().Single();

        db.InTransaction(c => DeliveryLedger.ChangeStatus(c, delivery.Id, DeliveryStatus.Delivered, Day.AddHours(7)));

        Assert.Equal(380m, db.Reload(customer).WalletBalance); // 2 L x 60
    }

    [Fact]
    public void The_forecast_matches_the_deliveries_that_will_be_generated_including_extras()
    {
        var held = Sub(1, MilkType.Cow, 2);
        held.IsPaused = true; held.PauseStartDate = Day; held.PauseEndDate = Day;
        var subs = new[] { held, Sub(2, MilkType.Cow, 1.5), Sub(3, MilkType.Buffalo, 2) };
        var customers = new[] { Cust(1), Cust(2), Cust(3), Cust(4, active: false) };
        var extras = new[]
        {
            Extra(id: 1, customerId: 1, type: MilkType.Cow, litres: 2),       // arrives although customer 1 is on hold
            Extra(id: 2, customerId: 2, type: MilkType.Cow, litres: 1),       // beside the regular delivery
            Extra(id: 3, customerId: 3, type: MilkType.Buffalo, litres: 3),
            Extra(id: 4, customerId: 4, type: MilkType.Cow, litres: 9),       // inactive customer
            Extra(id: 5, customerId: 2, type: MilkType.Cow, litres: 9, approved: false),
        };

        var forecast = DemandForecaster.Forecast(Day, subs, customers, extras);
        var plan = DeliveryPlanner.Plan(Day, subs, customers, Prices, Array.Empty<Delivery>(), extras);

        foreach (var type in Enum.GetValues<MilkType>())
        {
            var planned = plan.NewDeliveries.Where(d => d.MilkType == type).Sum(d => d.QuantityLitres);
            Assert.Equal(planned, forecast.Single(r => r.MilkType == type).PredictedRequiredLitres);
        }
    }
}
