namespace Daoris.Driver;

/// <summary>
/// A terminal that owns a conversation (CLITURN1): while it lives it tells the home where the conversation's turns stand
/// (<see cref="TurnMarks"/>, which a window shows) and honours a stop asked of that one session through the request folder
/// (SESSUX1g), as its own Ctrl+C at rest would. A window's words and turn stops stay on the terminal's input: no other move is taken.
/// </summary>
public sealed class TerminalOwner : IAsyncDisposable
{
    /// <summary>How often the terminal looks for a stop asked of its conversation: well inside the asking door's wait.</summary>
    public static readonly TimeSpan Looking = TimeSpan.FromMilliseconds(500);

    private readonly TurnMarks _marks;
    private readonly string _id;
    private readonly ChatRunner _runner;
    private readonly Action<string, ChatQueue> _told;
    private readonly SessionRequestWatch _asked;
    private readonly CancellationTokenSource _leaving = new();
    private readonly Task _honouring;

    public TerminalOwner(string home, string id, ChatRunner runner, SessionProcesses processes, TimeSpan? looking = null)
    {
        _marks = new TurnMarks(home);
        _id = id;
        _runner = runner;
        _told = (session, queue) =>
        {
            if (string.Equals(session, id, StringComparison.OrdinalIgnoreCase)) _marks.Write(id, queue);
        };
        runner.QueueChanged += _told;
        _marks.Write(id, runner.Queue(id));
        _asked = new SessionRequestWatch(
            home, session => string.Equals(session, id, StringComparison.OrdinalIgnoreCase), (session, note) => processes.Stop(session, note: note), () => null);
        _honouring = HonourAsync(looking ?? Looking, _leaving.Token);
    }

    /// <summary>Look for requests until the owner leaves; a folder held for a moment is looked at again.</summary>
    private async Task HonourAsync(TimeSpan looking, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _asked.HonourAsync(ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Looked at again.
                }

                await Task.Delay(looking, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The conversation is over.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _runner.QueueChanged -= _told;
        await _leaving.CancelAsync().ConfigureAwait(false);
        await _honouring.ConfigureAwait(false);
        await _asked.DisposeAsync().ConfigureAwait(false);
        _marks.Remove(_id);
        _leaving.Dispose();
    }
}
