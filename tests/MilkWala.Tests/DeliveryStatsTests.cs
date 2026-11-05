using MilkWala.Models;
using MilkWala.Services.Reporting;
using Xunit;

namespace MilkWala.Tests;

public class DeliveryStatsTests
{
    private static Delivery Make(DateTime date, DeliveryStatus status, double litres = 1, decimal rate = 50m) => new()
    {
        DeliveryDate = date,
        Status = status,
        QuantityLitres = litres,
        RatePerLitre = rate,
    };

    [Theory]
    [InlineData(DeliveryStatus.Delivered, true)]
    [InlineData(DeliveryStatus.Extra, true)]
    [InlineData(DeliveryStatus.Pending, false)]
    [InlineData(DeliveryStatus.Skipped, false)]
    [InlineData(DeliveryStatus.Holiday, false)]
    [InlineData(DeliveryStatus.Missed, false)]
    public void IsFulfilled_only_counts_delivered_and_extra(DeliveryStatus status, bool expected)
    {
        Assert.Equal(expected, DeliveryStats.IsFulfilled(status));
    }

    [Fact]
    public void Revenue_and_litres_ignore_unfulfilled_deliveries()
    {
        var day = new DateTime(2026, 11, 1);
        var deliveries = new[]
        {
            Make(day, DeliveryStatus.Delivered, 1.5, 60m),  // 90
            Make(day.AddDays(1), DeliveryStatus.Extra, 2, 60m), // 120
            Make(day.AddDays(2), DeliveryStatus.Skipped, 5, 60m),
            Make(day.AddDays(3), DeliveryStatus.Pending, 5, 60m),
        };

        Assert.Equal(210m, DeliveryStats.Revenue(deliveries));
        Assert.Equal(3.5, DeliveryStats.Litres(deliveries));
    }

    [Fact]
    public void MostRecent_returns_newest_first_even_when_input_is_oldest_first()
    {
        var start = new DateTime(2026, 11, 1);
        var monthAscending = Enumerable.Range(0, 20).Select(i => Make(start.AddDays(i), DeliveryStatus.Delivered)).ToList();

        var recent = DeliveryStats.MostRecent(monthAscending, 7);

        Assert.Equal(7, recent.Count);
        Assert.Equal(start.AddDays(19), recent[0].DeliveryDate);
        Assert.Equal(start.AddDays(13), recent[^1].DeliveryDate);
    }

    [Fact]
    public void MostRecent_with_fewer_items_than_requested_returns_all()
    {
        var recent = DeliveryStats.MostRecent(new[] { Make(DateTime.Today, DeliveryStatus.Pending) }, 7);
        Assert.Single(recent);
    }

    [Fact]
    public void MostRecent_rejects_negative_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryStats.MostRecent(Array.Empty<Delivery>(), -1));
    }
}
