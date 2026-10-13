namespace Daoris.Driver;

/// <summary>
/// Live session output, batched for whoever is showing it (D49 §2): subscribes to every line and
/// hands them out a window at a time.
/// </summary>
/// <remarks>
/// <para><b>The mechanics are <see cref="BatchRelay{T}"/>'s</b>, shared with the conversation's
/// <see cref="EventRelay"/> (D76): batched on a short timer, grouped by session, a failing reader
/// costing only its own view. The buffer holds the sequence numbers, so a reader that misses a batch
/// asks the driver for the gap.</para>
///
/// <para>It lives in the library rather than the shell so the batching is testable without an event
/// bus — the same split <see cref="DriverWatch"/> makes: the mechanics here, what a batch BECOMES
/// with whoever has a door to put it through.</para>
/// </remarks>
public sealed class ConsoleRelay : IDisposable
{
    /// <summary>How long lines accumulate before a batch goes out. Short enough to read as live.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(120);

    private readonly BatchRelay<ConsoleLine> _relay;

    /// <param name="emit">What a batch becomes — an IPC event in the shell, a list in a test.</param>
    /// <param name="window">How long to accumulate. The default reads as live to a person.</param>
    public ConsoleRelay(
        SessionOutput output, Func<string, IReadOnlyList<ConsoleLine>, Task> emit, TimeSpan? window = null) =>
        _relay = new BatchRelay<ConsoleLine>(
            handler => output.Lined += handler, handler => output.Lined -= handler, emit, window ?? DefaultWindow);

    /// <summary>Hand out everything queued, grouped by session. Internal so a test need not wait.</summary>
    internal Task FlushAsync() => _relay.FlushAsync();

    public void Dispose() => _relay.Dispose();
}
