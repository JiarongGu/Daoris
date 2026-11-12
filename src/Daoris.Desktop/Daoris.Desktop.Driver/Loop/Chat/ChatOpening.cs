namespace Daoris.Driver;

/// <summary>
/// A conversation's opening, from its record's start to its harness opening its conversation, in phases each held to one bound: the
/// plugins' servers brought up, left out past it with the opening going on; its harness started, and its conversation opened, where the
/// servers Daoris's own work needs come up, failing the opening past it with what did not come up said. The person's stop reaches every
/// phase before the process exists; after, it ends the process as any stop does.
/// </summary>
internal sealed class ChatOpening : IDisposable
{
    /// <summary>Each phase's bound until measured figures move it (D280 §2.8).</summary>
    public static readonly TimeSpan DefaultBound = TimeSpan.FromSeconds(60);

    private readonly CancellationTokenSource _stop = new();
    private readonly IDisposable _held;
    private CancellationTokenSource? _phase;

    public ChatOpening(OpeningStops stops, string sessionId, TimeSpan bound)
    {
        Bound = bound;
        _held = stops.Hold(sessionId, () =>
        {
            try
            {
                _stop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The opening ended between the look and the stop: its process, if any, is the stop's to end.
            }
        });
    }

    public TimeSpan Bound { get; }

    /// <summary>Whether the person stopped the opening.</summary>
    public bool Stopped => _stop.IsCancellationRequested;

    /// <summary>
    /// The phase begins: its token ends at its bound, at the person's stop, or with <paramref name="ct"/>; the phase before it is over.
    /// </summary>
    public CancellationToken Enter(CancellationToken ct)
    {
        _phase?.Dispose();
        _phase = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        _phase.CancelAfter(Bound);
        return _phase.Token;
    }

    /// <summary>Whether <paramref name="error"/> is the phase under way passing its bound, neither the person's stop nor <paramref name="ct"/>'s cancel.</summary>
    public bool PastBound(Exception error, CancellationToken ct) =>
        error is OperationCanceledException && !Stopped && !ct.IsCancellationRequested && _phase?.IsCancellationRequested == true;

    /// <summary>
    /// What did not come up when an opening on <paramref name="agent"/> passed its bound: its harness, and the servers Daoris's own work
    /// needs among those it was handed, which it opens its conversation only once they are up. A plugin's server is never one of them.
    /// </summary>
    public static string Waited(string agent, IEnumerable<ServerEntry> handed) =>
        string.Join(", ", [$"`{agent}`", .. handed.Where(server => server.Name == KnowledgeConnector.ServerName).Select(server => $"`{server.Name}`")]);

    /// <summary>The opening ended past its bound, said on the conversation: Ask Daoris's in its own words, any other conversation's in its.</summary>
    public static Noted NotUp(bool help, string what, TimeSpan bound)
    {
        var seconds = (int)bound.TotalSeconds;
        var rest = $"{what} did not come up within {seconds} s. Your words are kept, and your next send starts it again.";
        return help
            ? Noted.Of(ChatNoteCodes.HelpNotUp, $"Ask Daoris could not start: {rest}", ("what", what), ("seconds", seconds))
            : Noted.Of(ChatNoteCodes.ChatNotUp, $"This conversation could not start: {rest}", ("what", what), ("seconds", seconds));
    }

    /// <summary>The plugins' servers left out past their bound, the opening going on without them.</summary>
    public static Noted LeftOut(TimeSpan bound)
    {
        var seconds = (int)bound.TotalSeconds;
        return Noted.Of(
            ChatNoteCodes.ServersLeftOut,
            $"The plugins' servers did not come up within {seconds} s and were left out; the conversation goes on without them.",
            ("seconds", seconds));
    }

    public void Dispose()
    {
        _held.Dispose();
        _phase?.Dispose();
        _stop.Dispose();
    }
}
