namespace Daoris.Driver;

/// <summary>
/// A loop's half of the requests (SESSUX1g, D126 §7.1): every loop on the home watches <c>&lt;home&gt;/sessions/requests/</c>,
/// and acts on a request for a session its own registry runs as its own route does, recording the person's move as the
/// person's, then removes it. A request for a session another loop runs is that loop's, and is left.
/// </summary>
/// <remarks>
/// <para><b>As the routes do.</b> A stop is <see cref="SessionProcesses.Stop"/>, which <c>STOP_SESSION</c> makes: the
/// driver concludes the record <c>stopped</c> as the person's, and holds its quest (SESSUX1b). A parked session's stop, a
/// finish and a decline are <c>RESOLVE_SESSION</c>'s: the process goes first, then the ledger moves the record with the
/// person's words (<see cref="SessionMoves.ResolveAsync"/>); one is taken only once this loop's service answers.</para>
///
/// <para><b>Looked at every second</b>, not every tick: the asking door waits ten seconds for the record to move, and a
/// tick may be a minute apart. A look lists one folder, which is nothing.</para>
///
/// <para>The shell's loop and the headless host's run one each, with the registry their conversations and driven sessions
/// share. A terminal's <c>daoris-driver chat</c> runs none: a stop asked of its session is withdrawn after the wait, and
/// it stops with its own Ctrl+C.</para>
///
/// <para><b>A terminal's words</b> (MSG1e, D137 §5.2) are a say, handed to <see cref="Say"/> and answered beside the request. A
/// say for a session this registry runs is this loop's; one for a session nothing on this machine runs is any loop's whose
/// service answers, which keeps the words on the record, once, since a say is taken once; one another process here runs is
/// left for that process's loop.</para>
/// </remarks>
public sealed class SessionRequestWatch : IAsyncDisposable
{
    private readonly SessionRequests _requests;
    private readonly Func<string, bool> _runsHere;
    private readonly Func<string, string?, bool> _stop;
    private readonly Func<ServiceClient?> _service;
    private readonly Func<DateTimeOffset> _clock;

    // A say's two questions (MSG1e): whether this registry hears the session's words, a driven session's inbox among them,
    // and whether a process for it lives anywhere on this machine. The seams answer both by what runs here.
    private readonly Func<string, bool> _hears;
    private readonly Func<string, bool> _aliveHere;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Task _watching;

    /// <summary>
    /// A loop's half of a terminal's words (MSG1e, D137 §5.2): what it does with a say it takes, answered beside the request.
    /// A loop on a registry has <see cref="LoopWords"/>'s by default; a shell hands its own screen's judge. Null takes no say.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<WordsHeld>>? Say { get; init; }

    /// <summary>A loop's watch over its own registry, looking every <paramref name="every"/> (a second) until it is disposed.</summary>
    /// <param name="service">The loop's service once it answers; null before, when a request that needs the ledger waits.</param>
    public SessionRequestWatch(string home, SessionProcesses processes, Func<ServiceClient?> service, TimeSpan? every = null)
        : this(
            home, id => processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase), (id, note) => processes.Stop(id, note: note),
            service, clock: null)
    {
        _hears = id => processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) || processes.InboxOf(id) is not null;
        _aliveHere = processes.AliveOnThisMachine;
        Say = new LoopWords(processes, service).HoldAsync;
        _watching = WatchAsync(every ?? TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The test seam: what this loop runs, and how it stops one, in place of a registry of real processes. It looks only when
    /// <see cref="HonourAsync"/> is called.
    /// </summary>
    public SessionRequestWatch(
        string home, Func<string, bool> runsHere, Func<string, bool> stop, Func<ServiceClient?> service, Func<DateTimeOffset>? clock = null)
        : this(home, runsHere, (id, _) => stop(id), service, clock)
    {
    }

    /// <summary>
    /// The test seam that also hears the note a stop carries: a pause's words for the record (PAUSE1b), which the registry's
    /// stop writes on it as the person's.
    /// </summary>
    public SessionRequestWatch(
        string home, Func<string, bool> runsHere, Func<string, string?, bool> stop, Func<ServiceClient?> service, Func<DateTimeOffset>? clock = null)
    {
        _requests = new SessionRequests(home);
        _runsHere = runsHere;
        _stop = stop;
        _service = service;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _hears = runsHere;
        _aliveHere = runsHere;
        _watching = Task.CompletedTask;
    }

    /// <summary>
    /// One look: each request for a session this loop runs, taken and acted on, in the order they were asked. What was
    /// honoured comes back. A move the ledger refused is not retried: the asking door sees the record unmoved and says so.
    /// </summary>
    public async Task<IReadOnlyList<SessionRequest>> HonourAsync(CancellationToken ct = default)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var honoured = new List<SessionRequest>();
            foreach (var pending in _requests.Pending(_clock()))
            {
                if (pending.Move == SessionMove.Say)
                {
                    if (await SayAsync(pending, ct).ConfigureAwait(false) is { } said) honoured.Add(said);
                    continue;
                }

                if (!_runsHere(pending.Session)) continue;

                var resolves = pending.Move != SessionMove.Stop || pending.Parked;
                var service = _service();
                if (resolves && service is null) continue;
                if (_requests.Take(pending.Session) is not { } request) continue;

                try
                {
                    // A stop's note is a pause's words for the record (PAUSE1b), or none for the plain stop.
                    if (!resolves) _stop(request.Session, request.Note);
                    else
                    {
                        var state = request.Move switch
                        {
                            SessionMove.Finish => "completed",
                            SessionMove.Decline => "declined",
                            _ => "stopped",
                        };
                        await SessionMoves.ResolveAsync(id => _stop(id, null), service!, request.Session, state, request.Note, ct).ConfigureAwait(false);
                    }

                    honoured.Add(request);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException)
                {
                    // The ledger refused, or did not answer: the record did not move, and the door that asked says so.
                }
            }

            return honoured;
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>
    /// A say (MSG1e), taken where it is this loop's and handed to <see cref="Say"/>; what became of the words is written beside
    /// it for the door that asked, a failure included, so the asker is never left to wait out its time for an answer that will
    /// not come. Null where it is not this loop's, or another loop took it first.
    /// </summary>
    private async Task<SessionRequest?> SayAsync(SessionRequest pending, CancellationToken ct)
    {
        if (Say is not { } say) return null;

        // Not this registry's: another process here runs it, which its own loop answers, or the record's half waits for a service.
        if (!_hears(pending.Session) && (_aliveHere(pending.Session) || _service() is null)) return null;
        if (_requests.Take(pending) is not { } request) return null;

        WordsHeld held;
        try
        {
            held = await say(request, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is DriverException or HttpRequestException or System.Text.Json.JsonException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            held = WordsHeld.Failed(error.Message);
        }

        try
        {
            _requests.Answer(request, held);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unwritten: the door that asked waits out its time and says it could not tell where the words went.
        }

        return request;
    }

    private async Task WatchAsync(TimeSpan every)
    {
        using var timer = new PeriodicTimer(every);
        try
        {
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    await HonourAsync(_stopping.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A folder held for a moment: the next look reads it.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The loop is going.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _watching.ConfigureAwait(false);
        _stopping.Dispose();
        _one.Dispose();
    }
}
