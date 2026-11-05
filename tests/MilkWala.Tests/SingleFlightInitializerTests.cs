using MilkWala.Helpers;
using Xunit;

namespace MilkWala.Tests;

public class SingleFlightInitializerTests
{
    [Fact]
    public async Task Concurrent_callers_share_one_initialisation()
    {
        int runs = 0;
        var gate = new TaskCompletionSource();
        var initializer = new SingleFlightInitializer(async () =>
        {
            Interlocked.Increment(ref runs);
            await gate.Task;
        });

        var callers = Enumerable.Range(0, 50).Select(_ => Task.Run(initializer.EnsureAsync)).ToArray();
        gate.SetResult();
        await Task.WhenAll(callers);

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Later_calls_after_success_do_not_run_again()
    {
        int runs = 0;
        var initializer = new SingleFlightInitializer(() => { runs++; return Task.CompletedTask; });

        await initializer.EnsureAsync();
        await initializer.EnsureAsync();

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Failure_is_surfaced_and_the_next_call_retries()
    {
        int runs = 0;
        var initializer = new SingleFlightInitializer(() =>
        {
            runs++;
            return runs == 1 ? Task.FromException(new InvalidOperationException("db locked")) : Task.CompletedTask;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(initializer.EnsureAsync);
        Assert.Equal("db locked", ex.Message);

        await initializer.EnsureAsync();
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task Synchronous_throw_does_not_cache_the_failed_task()
    {
        int runs = 0;
        var initializer = new SingleFlightInitializer(() =>
        {
            runs++;
            if (runs == 1) throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(initializer.EnsureAsync);
        await initializer.EnsureAsync();

        Assert.Equal(2, runs);
    }

    [Fact]
    public void Null_routine_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SingleFlightInitializer(null!));
    }
}
