using MilkWala.Models;
using MilkWala.Services.Reporting;
using MilkWala.Services.Scheduling;
using Xunit;

namespace MilkWala.Tests;

public class DemandForecasterTests
{
    private static readonly DateTime Tomorrow = new(2026, 11, 6);

    private static Customer Cust(int id, bool active = true) => new() { Id = id, Name = $"C{id}", IsActive = active };

    private static Subscription Sub(int customerId, MilkType type, double litres, string frequency = "Daily") =>
        new() { CustomerId = customerId, MilkType = type, QuantityLitres = litres, DeliveryFrequency = frequency, StartDate = Tomorrow.AddDays(-30) };

    private static DemandForecastResult For(IEnumerable<DemandForecastResult> results, MilkType type) =>
        results.Single(r => r.MilkType == type);

    [Fact]
    public void Sums_litres_per_milk_type_and_lists_every_type()
    {
        var results = DemandForecaster.Forecast(Tomorrow,
            new[] { Sub(1, MilkType.Cow, 1.5), Sub(2, MilkType.Cow, 2), Sub(3, MilkType.Buffalo, 1) },
            new[] { Cust(1), Cust(2), Cust(3) }, Array.Empty<SpecialOrder>());

        Assert.Equal(Enum.GetValues<MilkType>().Length, results.Count);
        Assert.Equal(3.5, For(results, MilkType.Cow).PredictedRequiredLitres);
        Assert.Equal(1, For(results, MilkType.Buffalo).PredictedRequiredLitres);
        Assert.Equal(0, For(results, MilkType.Organic).PredictedRequiredLitres);
    }

    [Fact]
    public void Inactive_customers_are_not_counted()
    {
        var results = DemandForecaster.Forecast(Tomorrow,
            new[] { Sub(1, MilkType.Cow, 1.5), Sub(2, MilkType.Cow, 2) },
            new[] { Cust(1), Cust(2, active: false) }, Array.Empty<SpecialOrder>());

        Assert.Equal(1.5, For(results, MilkType.Cow).PredictedRequiredLitres);
    }

    [Fact]
    public void A_hold_covering_the_day_is_subtracted_but_an_expired_hold_is_not()
    {
        var onHold = Sub(1, MilkType.Cow, 2);
        onHold.IsPaused = true;
        onHold.PauseStartDate = Tomorrow.AddDays(-1);
        onHold.PauseEndDate = Tomorrow.AddDays(2);
        var expired = Sub(2, MilkType.Cow, 1);
        expired.IsPaused = true;
        expired.PauseStartDate = Tomorrow.AddDays(-9);
        expired.PauseEndDate = Tomorrow.AddDays(-1);

        var cow = For(DemandForecaster.Forecast(Tomorrow, new[] { onHold, expired }, new[] { Cust(1), Cust(2) }, Array.Empty<SpecialOrder>()), MilkType.Cow);

        Assert.Equal(3, cow.SubscribedLitres);
        Assert.Equal(2, cow.VacationPausedLitres);
        Assert.Equal(1, cow.PredictedRequiredLitres);
    }

    [Fact]
    public void An_alternate_day_plan_only_counts_on_its_delivery_days()
    {
        var alternate = Sub(1, MilkType.Cow, 2, "Alternate");
        alternate.StartDate = Tomorrow; // tomorrow is day 0 of the cycle => delivers
        var offDay = Sub(2, MilkType.Cow, 4, "Alternate");
        offDay.StartDate = Tomorrow.AddDays(-1); // tomorrow is day 1 => skips

        var cow = For(DemandForecaster.Forecast(Tomorrow, new[] { alternate, offDay }, new[] { Cust(1), Cust(2) }, Array.Empty<SpecialOrder>()), MilkType.Cow);

        Assert.Equal(2, cow.PredictedRequiredLitres);
    }

    [Fact]
    public void Only_approved_extra_orders_for_that_day_are_added()
    {
        var extras = new[]
        {
            new SpecialOrder { CustomerId = 1, MilkType = MilkType.Cow, ExtraQuantityLitres = 2, TargetDate = Tomorrow, IsApproved = true },
            new SpecialOrder { CustomerId = 1, MilkType = MilkType.Cow, ExtraQuantityLitres = 9, TargetDate = Tomorrow, IsApproved = false },
            new SpecialOrder { CustomerId = 1, MilkType = MilkType.Cow, ExtraQuantityLitres = 9, TargetDate = Tomorrow.AddDays(1), IsApproved = true },
        };

        var cow = For(DemandForecaster.Forecast(Tomorrow, new[] { Sub(1, MilkType.Cow, 1) }, new[] { Cust(1) }, extras), MilkType.Cow);

        Assert.Equal(2, cow.SpecialOrderExtraLitres);
        Assert.Equal(3, cow.PredictedRequiredLitres);
    }

    [Fact]
    public void The_forecast_matches_the_deliveries_that_will_actually_be_generated()
    {
        // One scenario with every rule in play: holds, an expired hold, alternate days, inactive customer, not started.
        var held = Sub(1, MilkType.Cow, 2);
        held.IsPaused = true; held.PauseStartDate = Tomorrow; held.PauseEndDate = Tomorrow;
        var alternateOff = Sub(2, MilkType.Cow, 3, "Alternate");
        alternateOff.StartDate = Tomorrow.AddDays(-1);
        var notStarted = Sub(3, MilkType.Cow, 5);
        notStarted.StartDate = Tomorrow.AddDays(1);
        var subs = new[]
        {
            held, alternateOff, notStarted,
            Sub(4, MilkType.Cow, 1.5), Sub(5, MilkType.Buffalo, 2), Sub(6, MilkType.Cow, 7),
        };
        var customers = new[] { Cust(1), Cust(2), Cust(3), Cust(4), Cust(5), Cust(6, active: false) };
        var prices = new[] { new Product { MilkType = MilkType.Cow, PricePerLitre = 60m }, new Product { MilkType = MilkType.Buffalo, PricePerLitre = 76m } };

        var forecast = DemandForecaster.Forecast(Tomorrow, subs, customers, Array.Empty<SpecialOrder>());
        var plan = DeliveryPlanner.Plan(Tomorrow, subs, customers, prices, Array.Empty<Delivery>());

        foreach (var type in Enum.GetValues<MilkType>())
        {
            var planned = plan.NewDeliveries.Where(d => d.MilkType == type).Sum(d => d.QuantityLitres);
            Assert.Equal(planned, For(forecast, type).PredictedRequiredLitres);
        }
    }
}
