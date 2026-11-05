using MilkWala.Models;
using MilkWala.Services.Scheduling;
using Xunit;

namespace MilkWala.Tests;

public class DeliveryPlannerTests
{
    private static readonly DateTime Day = new(2026, 11, 5);

    private static readonly Product[] Prices =
    {
        new() { MilkType = MilkType.Cow, PricePerLitre = 62m },
        new() { MilkType = MilkType.Buffalo, PricePerLitre = 76m },
    };

    private static Customer Cust(int id, bool active = true) =>
        new() { Id = id, Name = $"Customer {id}", Address = $"House {id}", IsActive = active };

    private static Subscription Sub(int customerId, MilkType type = MilkType.Cow, double litres = 1.5) =>
        new() { CustomerId = customerId, MilkType = type, QuantityLitres = litres, StartDate = Day.AddDays(-30) };

    private static DeliveryPlan Plan(IEnumerable<Subscription> subs, IEnumerable<Customer> customers, IEnumerable<Delivery>? existing = null) =>
        DeliveryPlanner.Plan(Day.AddHours(14), subs, customers, Prices, existing ?? Array.Empty<Delivery>());

    [Fact]
    public void Creates_a_pending_delivery_with_the_customer_details_and_current_price()
    {
        var plan = Plan(new[] { Sub(1, MilkType.Buffalo, 2) }, new[] { Cust(1) });

        var d = Assert.Single(plan.NewDeliveries);
        Assert.Equal(1, d.CustomerId);
        Assert.Equal("Customer 1", d.CustomerName);
        Assert.Equal("House 1", d.Address);
        Assert.Equal(Day, d.DeliveryDate); // time of day dropped
        Assert.Equal(MilkType.Buffalo, d.MilkType);
        Assert.Equal(2, d.QuantityLitres);
        Assert.Equal(76m, d.RatePerLitre);
        Assert.Equal(DeliveryStatus.Pending, d.Status);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Inactive_and_unknown_customers_get_no_delivery()
    {
        var plan = Plan(new[] { Sub(1), Sub(2), Sub(99) }, new[] { Cust(1, active: false), Cust(2) });

        Assert.Equal(new[] { 2 }, plan.NewDeliveries.Select(d => d.CustomerId));
    }

    [Fact]
    public void Planning_again_after_the_rows_exist_creates_nothing()
    {
        var subs = new[] { Sub(1), Sub(2) };
        var customers = new[] { Cust(1), Cust(2) };

        var first = Plan(subs, customers);
        var second = Plan(subs, customers, existing: first.NewDeliveries);

        Assert.Equal(2, first.NewDeliveries.Count);
        Assert.Empty(second.NewDeliveries);
    }

    [Fact]
    public void An_existing_delivery_on_another_day_does_not_block_today()
    {
        var yesterday = new Delivery { CustomerId = 1, MilkType = MilkType.Cow, DeliveryDate = Day.AddDays(-1) };

        var plan = Plan(new[] { Sub(1) }, new[] { Cust(1) }, new[] { yesterday });

        Assert.Single(plan.NewDeliveries);
    }

    [Fact]
    public void A_vacation_hold_becomes_a_zero_litre_holiday_row_so_the_calendar_shows_it()
    {
        var sub = Sub(1);
        sub.IsPaused = true;
        sub.PauseStartDate = Day.AddDays(-1);
        sub.PauseEndDate = Day.AddDays(3);

        var d = Assert.Single(Plan(new[] { sub }, new[] { Cust(1) }).NewDeliveries);

        Assert.Equal(DeliveryStatus.Holiday, d.Status);
        Assert.Equal(0, d.QuantityLitres);
        Assert.Equal(0m, d.TotalPrice);
    }

    [Fact]
    public void An_expired_hold_delivers_normally()
    {
        var sub = Sub(1);
        sub.IsPaused = true;
        sub.PauseStartDate = Day.AddDays(-10);
        sub.PauseEndDate = Day.AddDays(-1);

        var d = Assert.Single(Plan(new[] { sub }, new[] { Cust(1) }).NewDeliveries);

        Assert.Equal(DeliveryStatus.Pending, d.Status);
        Assert.Equal(1.5, d.QuantityLitres);
    }

    [Fact]
    public void Alternate_day_plan_is_skipped_on_its_off_day()
    {
        var sub = Sub(1);
        sub.DeliveryFrequency = "Alternate";
        sub.StartDate = Day.AddDays(-1); // today is day 1 of the cycle => off day

        Assert.Empty(Plan(new[] { sub }, new[] { Cust(1) }).NewDeliveries);
    }

    [Fact]
    public void A_subscription_that_has_not_started_yet_is_skipped()
    {
        var sub = Sub(1);
        sub.StartDate = Day.AddDays(2);

        Assert.Empty(Plan(new[] { sub }, new[] { Cust(1) }).NewDeliveries);
    }

    [Fact]
    public void A_milk_type_without_a_price_is_reported_not_guessed()
    {
        var plan = Plan(new[] { Sub(1, MilkType.Organic), Sub(2) }, new[] { Cust(1), Cust(2) });

        Assert.Equal(new[] { 2 }, plan.NewDeliveries.Select(d => d.CustomerId));
        var warning = Assert.Single(plan.Warnings);
        Assert.Contains("Organic", warning);
        Assert.Contains("Customer 1", warning);
    }

    [Fact]
    public void Zero_litre_subscriptions_are_ignored()
    {
        Assert.Empty(Plan(new[] { Sub(1, litres: 0) }, new[] { Cust(1) }).NewDeliveries);
    }

    [Fact]
    public void One_customer_with_two_milk_types_gets_two_deliveries()
    {
        var plan = Plan(new[] { Sub(1, MilkType.Cow), Sub(1, MilkType.Buffalo) }, new[] { Cust(1) });

        Assert.Equal(2, plan.NewDeliveries.Count);
    }
}
