using System.Diagnostics;
using Polly;
using Polly.Retry;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Central execution helper. Every meaningful operation goes through <see cref="RunAsync{T}"/> /
/// <see cref="Run{T}"/> so it is, in one place:
///  (1) logged at start AND end, with its inputs, result and measured duration (the spec's hard rule),
///  (2) wrapped in a Polly retry pipeline for transient failures,
///  (3) optionally bounded by a per-attempt timeout that is enforced both via the CancellationToken and
///      a <c>WaitAsync</c> guard, so an action that ignores the token still cannot hang (rule 43).
/// These methods never swallow — when retries are exhausted they rethrow. Only the outermost
/// <see cref="GuardAsync"/> swallows: it logs the final error and asks the user whether to retry (or,
/// for a fatal error, whether to restart the app and retry).
/// </summary>
internal static class Resilience
{
    /// <summary>Default transient-failure retry: 3 attempts, exponential backoff + jitter. Cancellation
    /// is never retried.</summary>
    public static readonly ResiliencePipeline DefaultPipeline = BuildPipeline(maxRetryAttempts: 3, baseDelaySeconds: 1);

    /// <summary>A heavier pipeline for flaky network work (more attempts, longer backoff).</summary>
    public static readonly ResiliencePipeline NetworkPipeline = BuildPipeline(maxRetryAttempts: 5, baseDelaySeconds: 2);

    static ResiliencePipeline BuildPipeline(int maxRetryAttempts, double baseDelaySeconds) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
                MaxRetryAttempts = maxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(baseDelaySeconds),
                OnRetry = args =>
                {
                    Log($"↻ retry #{args.AttemptNumber + 1} after {args.RetryDelay.TotalSeconds:F1}s " +
                        $"(reason: {args.Outcome.Exception?.Message})", LogLevel.Warning);
                    return default;
                },
            })
            .Build();

    /// <summary>
    /// Runs <paramref name="action"/> through the retry pipeline while logging start/end/duration.
    /// Rethrows on final failure (does NOT swallow). <paramref name="input"/> is logged as the
    /// operation's inputs (pass a redacted descriptor — never raw secrets).
    /// </summary>
    public static async Task<T> RunAsync<T>(string operationName, Func<CancellationToken, Task<T>> action,
        object? input = null, TimeSpan? perAttemptTimeout = null, ResiliencePipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        pipeline ??= DefaultPipeline;
        var stopwatch = Stopwatch.StartNew();
        Log($"▶ START {operationName}{(input is null ? "" : " | input: " + Describe(input))}", LogLevel.Info);
        try
        {
            T result = await pipeline.ExecuteAsync(async token =>
            {
                if (perAttemptTimeout is { } timeout)
                {
                    using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutSource.CancelAfter(timeout);
                    // Bound the await itself too: even if the action ignores the token, WaitAsync returns.
                    return await action(timeoutSource.Token).WaitAsync(timeout, token);
                }
                return await action(token);
            }, cancellationToken);

            stopwatch.Stop();
            Log($"■ END {operationName} | {stopwatch.ElapsedMilliseconds} ms | result: {Describe(result)}", LogLevel.Info);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Log($"⏹ CANCELLED {operationName} | {stopwatch.ElapsedMilliseconds} ms", LogLevel.Warning);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log($"✗ FAIL {operationName} | {stopwatch.ElapsedMilliseconds} ms | {ex.GetType().Name}: {ex.Message}", LogLevel.Warning);
            throw;
        }
    }

    /// <summary>Void-returning overload of <see cref="RunAsync{T}"/>.</summary>
    public static Task RunAsync(string operationName, Func<CancellationToken, Task> action,
        object? input = null, TimeSpan? perAttemptTimeout = null, ResiliencePipeline? pipeline = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(operationName, async token => { await action(token); return true; },
            input, perAttemptTimeout, pipeline, cancellationToken);

    /// <summary>Synchronous overload for non-async operations (still logged + retried).</summary>
    public static T Run<T>(string operationName, Func<T> action, object? input = null, ResiliencePipeline? pipeline = null) =>
        RunAsync(operationName, _ => Task.FromResult(action()), input, null, pipeline).GetAwaiter().GetResult();

    /// <summary>
    /// Outermost guard. Runs <paramref name="action"/>; on any unhandled error it logs the error (this is
    /// the ONLY place that swallows) and asks the user whether to retry. For a fatal error the prompt
    /// offers to restart the app and retry instead.
    /// </summary>
    public static async Task GuardAsync(string operationName, Func<Task> action, bool fatal = false)
    {
        while (true)
        {
            try { await action(); return; }
            catch (OperationCanceledException) { Log($"Guard [{operationName}] cancelled by user.", LogLevel.Info); return; }
            catch (Exception ex)
            {
                Log($"OUTERMOST swallow [{operationName}]: {ex}", LogLevel.Error);
                string prompt = fatal
                    ? string.Format(Strings.FatalRestartPromptFormat, ex.Message)
                    : string.Format(Strings.RetryPromptFormat, ex.Message);
                bool retry = await NativeMessageBox.ConfirmAsync(prompt);
                if (!retry) return;
                if (fatal) { RestartApp(); return; }
            }
        }
    }

    /// <summary>Launches a fresh instance of this exe (forwarding the original args) and exits the current
    /// one. Used by the fatal-error "restart and retry" path.</summary>
    public static void RestartApp()
    {
        try
        {
            var startInfo = new ProcessStartInfo(AppConstants.ThisExePath)
            {
                UseShellExecute = true,
                Arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1).Select(a => $"\"{a}\"")),
            };
            Process.Start(startInfo);
            Log("Restarting app after fatal error.", LogLevel.Info);
        }
        catch (Exception ex) { Log("Restart failed: " + ex.Message, LogLevel.Error); }
        finally { Environment.Exit(1); }
    }

    /// <summary>Compact, secret-safe description of a value for the operation log.</summary>
    static string Describe(object? value)
    {
        const int maxLength = 200;
        if (value is null) return "null";
        if (value is bool) return value.ToString()!;
        string text = value.ToString() ?? "";
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }
}
