using MilkWala.Helpers;
using MilkWala.Localization;
using Xunit;

namespace MilkWala.Tests;

[Collection("Language")]
public class OperationGuardTests : IDisposable
{
    public OperationGuardTests() => AppLanguage.Set("en");

    public void Dispose() => AppLanguage.Set("en");

    [Fact]
    public void The_failure_message_is_the_translated_generic_text()
    {
        Assert.Equal("Something went wrong. Please try again.", OperationGuard.DescribeFailure("LoadWalletAsync"));

        AppLanguage.Set("hi");
        Assert.Equal("कुछ गड़बड़ हो गई। कृपया फिर कोशिश करें।", OperationGuard.DescribeFailure("LoadWalletAsync"));

        AppLanguage.Set("mr");
        Assert.Equal("काहीतरी चूक झाली. कृपया पुन्हा प्रयत्न करा.", OperationGuard.DescribeFailure("LoadWalletAsync"));
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
    public async Task Failure_is_logged_with_the_exception_and_turned_into_a_message_in_the_current_language()
    {
        AppLanguage.Set("hi");
        Exception? logged = null;
        var boom = new InvalidOperationException("db locked");

        var outcome = await OperationGuard.RunAsync(() => throw boom, ex => logged = ex, "SaveSubscriptionAsync");

        Assert.False(outcome.Succeeded);
        Assert.Equal(Strings.ErrSomethingWentWrong, outcome.ErrorMessage);
        Assert.Contains("गड़बड़", outcome.ErrorMessage);
        Assert.Same(boom, logged); // the full exception reaches the logs; the user only sees the short message
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
