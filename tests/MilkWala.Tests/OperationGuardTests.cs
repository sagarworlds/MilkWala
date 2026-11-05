using MilkWala.Helpers;
using Xunit;

namespace MilkWala.Tests;

public class OperationGuardTests
{
    [Theory]
    [InlineData("LoadDashboardDataAsync", "load dashboard data")]
    [InlineData("AddMoneyAsync", "add money")]
    [InlineData("OrderProduct", "order product")]
    [InlineData("ToggleStatusAsync", "toggle status")]
    [InlineData("Async", "complete the action")]
    [InlineData("", "complete the action")]
    public void Humanize_turns_method_names_into_words(string operation, string expected)
    {
        Assert.Equal(expected, OperationGuard.Humanize(operation));
    }

    [Fact]
    public void DescribeFailure_gives_an_actionable_sentence()
    {
        Assert.Equal("Could not load wallet. Please try again.", OperationGuard.DescribeFailure("LoadWalletAsync"));
    }

    [Fact]
    public async Task Success_reports_success_and_does_not_log()
    {
        bool logged = false;

        var outcome = await OperationGuard.RunAsync(() => Task.CompletedTask, _ => logged = true, "LoadWalletAsync");

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.ErrorMessage);
        Assert.False(logged);
    }

    [Fact]
    public async Task Failure_is_logged_with_the_exception_and_turned_into_a_message()
    {
        Exception? logged = null;
        var boom = new InvalidOperationException("db locked");

        var outcome = await OperationGuard.RunAsync(() => throw boom, ex => logged = ex, "SaveSubscriptionAsync");

        Assert.False(outcome.Succeeded);
        Assert.Equal("Could not save subscription. Please try again.", outcome.ErrorMessage);
        Assert.Same(boom, logged); // full exception reaches the logs; the user only sees the short message
    }

    [Fact]
    public async Task Asynchronous_failures_are_caught_too()
    {
        var outcome = await OperationGuard.RunAsync(
            async () => { await Task.Yield(); throw new InvalidOperationException("later"); },
            _ => { },
            "LoadStoreCatalogAsync");

        Assert.False(outcome.Succeeded);
    }

    [Fact]
    public async Task Cancellation_is_not_treated_as_a_failure()
    {
        bool logged = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OperationGuard.RunAsync(
            () => throw new OperationCanceledException(), _ => logged = true, "LoadWalletAsync"));

        Assert.False(logged);
    }
}
