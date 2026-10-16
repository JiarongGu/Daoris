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
/// <para><b>Looked at when the folder says a request arrived</b> (FABRIC-EVENT2, the design §A4.1), on the
/// <see cref="ChangeWatch"/>: within its quiet window, not at a tick, since the asking door waits ten seconds for the record to
/// move and a tick may be a minute apart. A listing every <see cref="Listing"/> stays behind it, inside the door's ten seconds,
/// for whatever the watcher missed. The folder hears a request or a press arriving, never what a look writes there itself (a
/// take, an answer, a removal: <see cref="Hears"/>), so a look never wakes the next.</para>
///
/// <para>The shell's loop and the headless host's run one each, with the registry their conversations and driven sessions
/// share. A terminal's <c>daoris-driver chat</c> runs none: a stop asked of its session is withdrawn after the wait, and
/// it stops with its own Ctrl+C.</para>
///
/// <para><b>A terminal's words</b> (MSG1e, D137 §5.2) are a say, handed to <see cref="Say"/> and answered beside the request. A
/// say for a session this registry runs is this loop's; one for a session nothing on this machine runs is any loop's whose
/// service answers, which keeps the words on the record, once, since a say is taken once; one another process here runs is
/// left for that process's loop.</para>
///
/// <para><b>And what a terminal pressed</b> (STATEHUB1e, D169 point 8): a <c>trees</c> verb tells its press in the same folder
/// (<see cref="PressesTold"/>), and the loop that hears them, the shell's, publishes it to its windows at the same look.</para>
/// </remarks>
public sealed class SessionRequestWatch : IAsyncDisposable
{
    private readonly SessionRequests _requests;
    private readonly PressesTold _told;
    private readonly Func<string, bool> _runsHere;
    private readonly Func<string, Noted?, bool> _stop;
    private readonly Func<ServiceClient?> _service;
    private readonly Func<DateTimeOffset> _clock;

    // A say's two questions (MSG1e): whether this registry hears the session's words, a driven session's inbox among them,
    // and whether a process for it lives anywhere on this machine. The seams answer both by what runs here.
    private readonly Func<string, bool> _hears;
    private readonly Func<string, bool> _aliveHere;
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>
    /// The listing behind the change watch (FABRIC-EVENT2): the longest a request waits when the watcher missed it, a third of
    /// the asking door's ten seconds (<see cref="SessionsWorld.Wait"/>), so a missed one is still taken in time.
    /// </summary>
    public static readonly TimeSpan Listing = TimeSpan.FromSeconds(3);

    /// <summary>The folder's watch, or null on the test seams, which look only when <see cref="HonourAsync"/> is called.</summary>
    internal ChangeWatch? Changes { get; }

    /// <summary>Where a look the folder woke is written with its ms (<c>change.judged</c>); none by default.</summary>
    public MachineLog? Log
    {
        get => Changes?.Log;
        init
        {
            if (Changes is not null) Changes.Log = value;
        }
    }

    /// <summary>
    /// A loop's half of a terminal's words (MSG1e, D137 §5.2): what it does with a say it takes, answered beside the request.
    /// A loop on a registry has <see cref="LoopWords"/>'s by default; a shell hands its own screen's judge. Null takes no say.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<WordsHeld>>? Say { get; init; }

    /// <summary>
    /// A loop's half of a terminal's <c>sessions take-back</c> (SAIDBACK1c, D137 §2.4), answered beside the request in the say's
    /// shape: <see cref="WordsHeld.Sent"/> is that the word was taken back, a refusal's code is <see cref="WordsHeld.Why"/>. A
    /// loop on a registry has <see cref="LoopWords"/>'s by default, which keeps no note; the host hands one that does. Null takes none.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<WordsHeld>>? TakeBack { get; init; }

    /// <summary>
    /// What a loop does with a press a terminal told (STATEHUB1e, D169 point 8): the shell's publishes it to its windows as the
    /// window's press of that route is published. Null takes none: a headless loop has no window, so it leaves each for a loop that
    /// hears them, and drops only what nobody took in time (<see cref="PressesTold"/>).
    /// </summary>
    public Action<PressTold>? Told { get; init; }

    /// <summary>
    /// A loop's watch over its own registry, looking when the folder says a request arrived, and listing it every
    /// <paramref name="every"/> (<see cref="Listing"/>) behind that, until it is disposed.
    /// </summary>
    /// <param name="service">The loop's service once it answers; null before, when a request that needs the ledger waits.</param>
    /// <param name="clock">What a request's age is measured against; the real clock unless a test hands its own.</param>
    /// <param name="source">What watches the folder: the machine's watcher unless a test hands its fake.</param>
    public SessionRequestWatch(
        string home, SessionProcesses processes, Func<ServiceClient?> service, TimeSpan? every = null, Func<DateTimeOffset>? clock = null,
        IChangeSource? source = null)
        : this(
            home, id => processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase), (id, note) => processes.Stop(id, note: note),
            service, clock)
    {
        _hears = id => processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) || processes.InboxOf(id) is not null;
        _aliveHere = processes.AliveOnThisMachine;
        var words = new LoopWords(processes, service);
        Say = words.HoldAsync;
        TakeBack = words.TakeBackAsync;
        // FABRIC-EVENT2c: made before the watch starts, so its watcher opens at once and the first request after an install is
        // heard, not found at the listing. Both loops (the desktop's and the headless host's) build their watch here.
        Directory.CreateDirectory(_requests.Folder);
        Changes = new ChangeWatch(source);
        Changes.Topic(ChangeTopics.Requests, every ?? Listing, LookAsync);
        Changes.Watch(ChangeTopics.Requests, ChangeTopics.Requests, _requests.Folder, Hears);
        Changes.Start();
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
    /// stop writes on it as the person's, with their codes (LANG1a).
    /// </summary>
    public SessionRequestWatch(
        string home, Func<string, bool> runsHere, Func<string, Noted?, bool> stop, Func<ServiceClient?> service, Func<DateTimeOffset>? clock = null)
    {
        _requests = new SessionRequests(home);
        _told = new PressesTold(home);
        _runsHere = runsHere;
        _stop = stop;
        _service = service;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _hears = runsHere;
        _aliveHere = runsHere;
    }

    /// <summary>
    /// What the folder hears (FABRIC-EVENT2): a request (<c>.json</c>) or a press (<see cref="PressesTold.Suffix"/>) arriving under
    /// its final name. Never a deletion, a temporary, a take's or a withdrawal's name, or a say's answer: those are what a look
    /// and the asking door write themselves, and a look they woke would wake the next.
    /// </summary>
    internal static bool Hears(string name, ChangeKind kind) =>
        kind != ChangeKind.Deleted
        && !name.Contains('/')
        && (name.EndsWith(PressesTold.Suffix, StringComparison.Ordinal)
            || (name.EndsWith(".json", StringComparison.Ordinal) && !name.EndsWith(SessionRequests.Answered, StringComparison.Ordinal)));

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
                    if (await SayAsync(pending, Say, ct).ConfigureAwait(false) is { } said) honoured.Add(said);
                    continue;
                }

                if (pending.Move == SessionMove.TakeBack)
                {
                    if (await SayAsync(pending, TakeBack, ct).ConfigureAwait(false) is { } back) honoured.Add(back);
                    continue;
                }

                if (!_runsHere(pending.Session)) continue;

                var resolves = pending.Move != SessionMove.Stop || pending.Parked;
                var service = _service();
                if (resolves && service is null) continue;
                if (_requests.Take(pending.Session) is not { } request) continue;

                try
                {
                    // A stop's note is a pause's words for the record (PAUSE1b), or none for the plain stop: the driver's line,
                    // with its codes, or carried whole where an older door wrote none (LANG1a).
                    var line = request.Note is null ? null : Noted.From(request.Note, request.NoteParts);
                    if (!resolves) _stop(request.Session, line);
                    else if (request.Move == SessionMove.Stop && line is not null)
                    {
                        await SessionMoves.ResolveAsync(id => _stop(id, null), service!, request.Session, "stopped", line, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var state = request.Move switch
                        {
                            SessionMove.Finish => "completed",
                            SessionMove.Decline => "declined",
                            _ => "stopped",
                        };
                        // A finish's note and a decline's reason are the person's own words.
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
    /// One look at the presses a terminal told (STATEHUB1e): each taken once and handed to <see cref="Told"/>, oldest first, and what
    /// was heard comes back. A loop with no <see cref="Told"/> takes none, and drops only what nobody took in time.
    /// </summary>
    public IReadOnlyList<PressTold> Hear()
    {
        var now = _clock();
        if (Told is not { } told)
        {
            _told.Drop(now);
            return [];
        }

        var heard = _told.Take(now);
        foreach (var press in heard) told(press);
        return heard;
    }

    /// <summary>
    /// A say (MSG1e), taken where it is this loop's and handed to <see cref="Say"/>; what became of the words is written beside
    /// it for the door that asked, a failure included, so the asker is never left to wait out its time for an answer that will
    /// not come. Null where it is not this loop's, or another loop took it first.
    /// </summary>
    private async Task<SessionRequest?> SayAsync(
        SessionRequest pending, Func<SessionRequest, CancellationToken, Task<WordsHeld>>? say, CancellationToken ct)
    {
        if (say is null) return null;

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

    /// <summary>One look the change watch asked for: the requests honoured, and what a terminal pressed; how many of both.</summary>
    private async Task<int> LookAsync(ChangeWake wake, CancellationToken ct)
    {
        try
        {
            var honoured = await HonourAsync(ct).ConfigureAwait(false);
            // STATEHUB1e: and what a terminal pressed, in the same folder at the same look.
            return honoured.Count + Hear().Count;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder held for a moment: the listing behind the watch reads it again.
            return 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Changes is not null) await Changes.DisposeAsync().ConfigureAwait(false);
        _one.Dispose();
    }
}
