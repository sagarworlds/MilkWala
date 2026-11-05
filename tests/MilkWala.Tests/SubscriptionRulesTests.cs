using MilkWala.Models;
using MilkWala.Services.Scheduling;
using Xunit;

namespace MilkWala.Tests;

public class SubscriptionRulesTests
{
    private static readonly DateTime Start = new(2026, 11, 1);

    private static Subscription Sub(string frequency = "Daily") => new() { StartDate = Start, DeliveryFrequency = frequency };

    [Fact]
    public void Not_paused_when_the_flag_is_off_even_inside_the_dates()
    {
        var sub = Sub();
        sub.PauseStartDate = Start;
        sub.PauseEndDate = Start.AddDays(10);

        Assert.False(SubscriptionRules.IsPausedOn(sub, Start.AddDays(2)));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]   // first day of the hold
    [InlineData(7, true)]
    [InlineData(9, true)]   // last day of the hold is included
    [InlineData(10, false)] // hold over: deliveries resume without the customer tapping Resume
    public void A_hold_covers_exactly_its_date_range(int dayOfMonth, bool expectedPaused)
    {
        var sub = Sub();
        sub.IsPaused = true;
        sub.PauseStartDate = new DateTime(2026, 11, 5);
        sub.PauseEndDate = new DateTime(2026, 11, 9);

        Assert.Equal(expectedPaused, SubscriptionRules.IsPausedOn(sub, new DateTime(2026, 11, dayOfMonth, 15, 0, 0)));
    }

    [Fact]
    public void An_open_ended_hold_has_no_end()
    {
        var sub = Sub();
        sub.IsPaused = true;

        Assert.True(SubscriptionRules.IsPausedOn(sub, Start.AddYears(1)));
    }

    [Fact]
    public void Daily_plans_are_due_every_day_from_the_start_date()
    {
        var sub = Sub("Daily");

        Assert.False(SubscriptionRules.IsDueOn(sub, Start.AddDays(-1)));
        Assert.True(SubscriptionRules.IsDueOn(sub, Start));
        Assert.True(SubscriptionRules.IsDueOn(sub, Start.AddDays(1)));
    }

    [Fact]
    public void Alternate_plans_are_due_every_second_day_counted_from_the_start_date()
    {
        var sub = Sub("Alternate");

        Assert.True(SubscriptionRules.IsDueOn(sub, Start));
        Assert.False(SubscriptionRules.IsDueOn(sub, Start.AddDays(1)));
        Assert.True(SubscriptionRules.IsDueOn(sub, Start.AddDays(2)));
        Assert.False(SubscriptionRules.IsDueOn(sub, Start.AddDays(3)));
    }

    [Fact]
    public void Unknown_frequency_delivers_daily_rather_than_silently_never()
    {
        Assert.True(SubscriptionRules.IsDueOn(Sub("Custom"), Start.AddDays(1)));
    }
}
