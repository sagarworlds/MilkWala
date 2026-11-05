using System.Text.RegularExpressions;

namespace MilkWala.Helpers
{
    /// <summary>Result of a guarded UI operation.</summary>
    /// <param name="Succeeded">True when the operation finished without throwing.</param>
    /// <param name="ErrorMessage">User-facing text when it failed; null on success.</param>
    public sealed record OperationOutcome(bool Succeeded, string? ErrorMessage);

    /// <summary>
    /// Runs a UI command so a failure is logged and turned into a readable message instead of
    /// crashing the app or silently leaving the screen stuck in its busy state.
    /// </summary>
    public static class OperationGuard
    {
        /// <summary>
        /// Executes <paramref name="work"/>. Any failure is passed to <paramref name="logFailure"/> (so the full
        /// exception is kept in the logs) and reported as a short, actionable message.
        /// </summary>
        /// <param name="work">The operation to run.</param>
        /// <param name="logFailure">Receives the exception for logging.</param>
        /// <param name="operation">Method name such as "LoadDashboardDataAsync"; used to word the message.</param>
        /// <exception cref="OperationCanceledException">Cancellation is not a failure and is rethrown to the caller.</exception>
        public static async Task<OperationOutcome> RunAsync(Func<Task> work, Action<Exception> logFailure, string operation)
        {
            ArgumentNullException.ThrowIfNull(work);
            ArgumentNullException.ThrowIfNull(logFailure);

            try
            {
                await work();
                return new OperationOutcome(true, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Deliberate catch-all: this is the UI boundary. Logged with full detail, never swallowed silently.
                logFailure(ex);
                return new OperationOutcome(false, DescribeFailure(operation));
            }
        }

        /// <summary>"LoadDashboardDataAsync" becomes "Could not load dashboard data. Please try again."</summary>
        public static string DescribeFailure(string operation) => $"Could not {Humanize(operation)}. Please try again.";

        /// <summary>Turns a PascalCase method name into lower-case words, dropping a trailing "Async".</summary>
        public static string Humanize(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                return "complete the action";

            var name = operation.EndsWith("Async", StringComparison.Ordinal) ? operation[..^"Async".Length] : operation;
            var words = Regex.Replace(name, "(?<!^)(?=[A-Z])", " ").Trim().ToLowerInvariant();
            return words.Length == 0 ? "complete the action" : words;
        }
    }
}
