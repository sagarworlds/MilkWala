namespace MilkWala.Helpers
{
    /// <summary>
    /// Runs an asynchronous initialisation routine exactly once, even when many callers
    /// request it at the same time. Every concurrent caller awaits the same task.
    /// If the routine fails, the failure is surfaced to the callers and the next call retries,
    /// so a transient error (e.g. a locked database file) does not poison the app until restart.
    /// </summary>
    public sealed class SingleFlightInitializer
    {
        private readonly Func<Task> _initialize;
        private readonly object _gate = new();
        private Task? _inFlight;

        /// <param name="initialize">The routine to run. Must be safe to re-run after a failure.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="initialize"/> is null.</exception>
        public SingleFlightInitializer(Func<Task> initialize)
        {
            _initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
        }

        /// <summary>
        /// Returns the shared initialisation task, starting it if it is not running or completed yet.
        /// </summary>
        /// <exception cref="Exception">Whatever the initialisation routine throws is rethrown to the awaiting caller.</exception>
        public Task EnsureAsync()
        {
            lock (_gate)
            {
                return _inFlight ??= RunAsync();
            }
        }

        private async Task RunAsync()
        {
            // Yield first so the caller has stored the task in _inFlight before any failure handling runs.
            // Without it a synchronously failing routine would clear the field *before* the assignment
            // and leave the faulted task cached forever.
            await Task.Yield();

            try
            {
                await _initialize().ConfigureAwait(false);
            }
            catch
            {
                lock (_gate)
                {
                    _inFlight = null; // allow the next caller to retry
                }
                throw;
            }
        }
    }
}
