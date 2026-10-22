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
///
/// <para><b>Each session's on its own</b> (PARALLEL1h, D181 point 3). A look claims each session it has a request for and honours
/// that session's requests beside the others, in the order they were asked; a session already claimed keeps its claim, and the
/// requests a later look found for it are honoured by that claim after the ones it holds. So a say that takes its time (a
/// conversation's door, the ledger) holds only its own session's next request, never another session's nor a terminal's press.
/// The folder's look returns once it has begun them, and what it began is waited for, cancelled, when the watch closes.</para>
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

    // PARALLEL1h: the sessions being honoured now, each its claim's end, and those a look found more for while claimed. Held only to
    // read and change these, never across a say, a stop or the ledger.
    private readonly object _gate = new();
    private readonly Dictionary<string, Task> _claimed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _again = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the folder's looks begin on: cancelled as the watch closes, which then waits for what they began.</summary>
    private readonly CancellationTokenSource _closing = new();

    /// <summary>
    /// The listing behind the change watch (FABRIC-EVENT2): the longest a request waits when the watcher missed it, a third of
    /// the asking door's ten seconds (<see cref="SessionsWorld.Wait"/>), so a missed one is still taken in time.
    /// </summary>
    public static readonly TimeSpan Listing = TimeSpan.FromSeconds(3);

    /// <summary>The folder's watch, or null on the test seams, which look only when <see cref="HonourAsync"/> is called.</summary>
    internal ChangeWatch? Changes { get; }

    /// <summary>
    /// The chain on the folder's watch (FABRIC-CHAIN2, the design §C6), or null on the test seams: the folder is link 7's, its wake
    /// a <c>home</c> event and its listing the topic's <c>timer</c>, and its reader the only row bound here.
    /// </summary>
    internal Chain? Chain { get; }

    /// <summary>
    /// Where a look the folder woke is written with its ms (<c>change.judged</c>), and one that took something as the chain's
    /// (<c>chain.fired</c>); and each chosen setting's outcome (<c>help.setting</c>, HELPMODE1c3); none by default.
    /// </summary>
    public MachineLog? Log
    {
        get => _log;
        init
        {
            _log = value;
            if (Changes is not null) Changes.Log = value;
            if (Chain is not null) Chain.Log = value;
        }
    }

    private readonly MachineLog? _log;

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
    /// A loop's half of Ask Daoris's housekeeping tools (HELPMODE1c2a, D183's note): what it does with a housekeeping request for a
    /// conversation its registry runs, answered beside the request. A loop on a registry has <see cref="HelpHousekeeping"/>'s by
    /// default, which judges each call and calls the cleanup's own judges; null takes none.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<HousekeepingAnswer>>? Housekeep { get; init; }

    /// <summary>
    /// A loop's half of Ask Daoris's chosen settings (HELPMODE1c3, D183's note): what it does with a setting request for a conversation
    /// its registry runs, answered beside the request, and its outcome kept in <see cref="Log"/>. A loop on a registry has
    /// <see cref="HelpSettings"/>'s by default, which judges each call and changes the setting through its own door's edit; null takes none.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<SettingAnswer>>? Set { get; init; }

    /// <summary>
    /// A loop's half of Ask Daoris's checkout reads (HELPMODE1c4, D183's note): what it does with a read of a checkout for a conversation
    /// its registry runs, answered beside the request with what was read. A loop on a registry has <see cref="HelpCheckouts"/>'s by
    /// default, which judges each call; null takes none.
    /// </summary>
    public Func<SessionRequest, CancellationToken, Task<CheckoutAnswer>>? Checkout { get; init; }

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
        Housekeep = new HelpHousekeeping(home, service).AnswerAsync;
        Set = new HelpSettings(home, service).AnswerAsync;
        Checkout = new HelpCheckouts(home, service).AnswerAsync;
        // FABRIC-EVENT2c: made before the watch starts, so its watcher opens at once and the first request after an install is
        // heard, not found at the listing. Both loops (the desktop's and the headless host's) build their watch here.
        Directory.CreateDirectory(_requests.Folder);
        Changes = new ChangeWatch(source);
        Chain = new Chain(Changes).Bind(ChainRanks.Read, ReadAsync);
        Chain.Topic(ChangeTopics.Requests, ChainEvents.Home, every ?? Listing);
        Changes.Watch(ChangeTopics.Requests, ChangeTopics.Requests, _requests.Folder, Hears);
        Changes.Start();
    }

    /// <summary>The machine resumed, as its shell heard (FABRIC-EVENT2d): the folder's watcher is reopened and the requests judged now.</summary>
    public void Resumed() => Changes?.Resumed();

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
    /// One look: each request for a session this loop runs, taken and acted on, a session's in the order they were asked and each
    /// session beside the others (PARALLEL1h). What this look honoured comes back, in the order asked. A session another look is
    /// honouring is waited for, and what it honours is that look's. A move the ledger refused is not retried: the asking door sees
    /// the record unmoved and says so.
    /// </summary>
    public async Task<IReadOnlyList<SessionRequest>> HonourAsync(CancellationToken ct = default)
    {
        var (runs, held, _) = Begin(ct);
        var honoured = await Task.WhenAll(runs).ConfigureAwait(false);
        await Task.WhenAll(held).WaitAsync(ct).ConfigureAwait(false);
        return [.. honoured.SelectMany(each => each).OrderBy(request => request.At)];
    }

    /// <summary>
    /// Claim each session with a request this loop would act on, and begin honouring it beside the others (PARALLEL1h): the runs
    /// begun, each its session's honoured; the claims other looks hold that this one found more for, which honour those after
    /// their own; and how many requests the runs begun with.
    /// </summary>
    private (List<Task<IReadOnlyList<SessionRequest>>> Runs, List<Task> Held, int Begun) Begin(CancellationToken ct)
    {
        var runs = new List<Task<IReadOnlyList<SessionRequest>>>();
        var held = new List<Task>();
        var begun = 0;
        foreach (var session in _requests.Pending(_clock()).Where(Ours).GroupBy(request => request.Session, StringComparer.OrdinalIgnoreCase))
        {
            TaskCompletionSource claim;
            lock (_gate)
            {
                if (_claimed.TryGetValue(session.Key, out var other))
                {
                    _again.Add(session.Key);
                    held.Add(other);
                    continue;
                }

                claim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _claimed[session.Key] = claim.Task;
            }

            SessionRequest[] mine = [.. session];
            begun += mine.Length;
            runs.Add(Task.Run(() => HonourSessionAsync(session.Key, mine, claim, ct), CancellationToken.None));
        }

        return (runs, held, begun);
    }

    /// <summary>
    /// One session's claim (PARALLEL1h): its requests honoured in the order asked, then those a later look found for it meanwhile,
    /// read from the folder again, until a look has found none more; then the claim is let go.
    /// </summary>
    private async Task<IReadOnlyList<SessionRequest>> HonourSessionAsync(
        string session, IReadOnlyList<SessionRequest> mine, TaskCompletionSource claim, CancellationToken ct)
    {
        var honoured = new List<SessionRequest>();
        try
        {
            while (true)
            {
                foreach (var pending in mine)
                {
                    // A closing watch leaves what it has not begun in the folder, for the next loop that looks.
                    ct.ThrowIfCancellationRequested();
                    if (await HonourOneAsync(pending, ct).ConfigureAwait(false) is { } done) honoured.Add(done);
                }

                lock (_gate)
                {
                    if (!_again.Remove(session))
                    {
                        _claimed.Remove(session);
                        return honoured;
                    }
                }

                mine = [.. _requests.Pending(_clock()).Where(pending => Same(pending.Session, session) && Ours(pending))];
            }
        }
        catch
        {
            lock (_gate)
            {
                if (_claimed.TryGetValue(session, out var held) && held == claim.Task) _claimed.Remove(session);
                _again.Remove(session);
            }

            throw;
        }
        finally
        {
            claim.TrySetResult();
        }
    }

    private static bool Same(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a request is this loop's to act on now: what its honouring judges again just before it takes it, so a look claims no
    /// session whose requests are another loop's.
    /// </summary>
    private bool Ours(SessionRequest pending) => pending.Move switch
    {
        SessionMove.Say => Say is not null && SaysHere(pending.Session),
        SessionMove.TakeBack => TakeBack is not null && SaysHere(pending.Session),
        // HELPMODE1c2a: only the loop running the conversation, whose tool is waiting on it now.
        SessionMove.Housekeep => Housekeep is not null && _runsHere(pending.Session),
        // HELPMODE1c3: the same for a chosen setting.
        SessionMove.Set => Set is not null && _runsHere(pending.Session),
        // HELPMODE1c4: and for a checkout read.
        SessionMove.Read => Checkout is not null && _runsHere(pending.Session),
        _ => _runsHere(pending.Session) && (!Resolves(pending) || _service() is not null),
    };

    /// <summary>
    /// Whether a say for the session is this registry's (MSG1e): one it hears, or one nothing on this machine runs while this loop's
    /// service answers. Not where another process here runs it, which its own loop answers, nor where the record's half waits for a service.
    /// </summary>
    private bool SaysHere(string session) => _hears(session) || (!_aliveHere(session) && _service() is not null);

    /// <summary>Whether the move is the ledger's as well as the process's: anything but a live session's plain stop.</summary>
    private static bool Resolves(SessionRequest pending) => pending.Move != SessionMove.Stop || pending.Parked;

    /// <summary>One request taken and acted on, or null where it is not this loop's now, or another loop took it first.</summary>
    private async Task<SessionRequest?> HonourOneAsync(SessionRequest pending, CancellationToken ct)
    {
        if (pending.Move == SessionMove.Say) return await SayAsync(pending, Say, ct).ConfigureAwait(false);
        if (pending.Move == SessionMove.TakeBack) return await SayAsync(pending, TakeBack, ct).ConfigureAwait(false);
        if (pending.Move == SessionMove.Housekeep) return await HousekeepAsync(pending, ct).ConfigureAwait(false);
        if (pending.Move == SessionMove.Set) return await SetAsync(pending, ct).ConfigureAwait(false);
        if (pending.Move == SessionMove.Read) return await ReadCheckoutAsync(pending, ct).ConfigureAwait(false);
        if (!_runsHere(pending.Session)) return null;

        var resolves = Resolves(pending);
        var service = _service();
        if (resolves && service is null) return null;
        if (_requests.Take(pending.Session) is not { } request) return null;

        try
        {
            // A stop's note is a pause's words for the record (PAUSE1b), or none for the plain stop: the driver's line,
            // with its codes, or carried whole where an older door wrote none (LANG1a).
            var line = request.Note is null ? null : Noted.From(request.Note, request.NoteParts);
            // TERMTRUST1a (D201 point 7): a stop under the standing confirmation keeps its own line and says so after it.
            if (request.Standing && request.Move == SessionMove.Stop)
            {
                line = SessionMoves.Standing(line ?? (resolves ? SessionMoves.ByThePersonNoted("stopped") : Observation.Stopped));
            }

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

            return request;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException)
        {
            // The ledger refused, or did not answer: the record did not move, and the door that asked says so.
            return null;
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
        if (say is null || !SaysHere(pending.Session)) return null;
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

    /// <summary>
    /// A housekeeping request (HELPMODE1c2a), taken where this loop runs its conversation and handed to <see cref="Housekeep"/>; what
    /// became of it is written beside it, a failure included, so the tool is never left to wait out its time. Held in the session's
    /// claim, so one conversation's requests are judged one after another, never two clean-ups of one repository at once from it.
    /// Null where it is not this loop's, or another loop took it first.
    /// </summary>
    private async Task<SessionRequest?> HousekeepAsync(SessionRequest pending, CancellationToken ct)
    {
        if (Housekeep is not { } housekeep || !_runsHere(pending.Session)) return null;
        if (_requests.Take(pending) is not { } request) return null;

        var operation = request.Operation ?? "";
        HousekeepingAnswer answer;
        try
        {
            answer = await housekeep(request, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Never "nothing was done": a clean-up that failed on its third branch removed the first two.
            answer = new HousekeepingAnswer(operation, false, HousekeepingAnswer.Failed,
                $"Daoris could not finish it ({error.Message.TrimEnd('.')}). Look with `housekeeping_inspect` to see what stands now.");
        }

        try
        {
            _requests.Answer(request, answer);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unwritten: the tool waits out its time and says the act may still be under way.
        }

        return request;
    }

    /// <summary>
    /// A setting request (HELPMODE1c3), taken where this loop runs its conversation and handed to <see cref="Set"/>, as a housekeeping
    /// request is; what became of it is written beside it, a failure included, and kept as its outcome in the machine log: the setting,
    /// its value, where it was set and whether it changed, never the words. Null where it is not this loop's, or another loop took it.
    /// </summary>
    private async Task<SessionRequest?> SetAsync(SessionRequest pending, CancellationToken ct)
    {
        if (Set is not { } set || !_runsHere(pending.Session)) return null;
        if (_requests.Take(pending) is not { } request) return null;

        var setting = request.Operation ?? "";
        SettingAnswer answer;
        try
        {
            answer = await set(request, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Never "nothing was changed": a write that failed at its rename may or may not have landed.
            answer = new SettingAnswer(setting, false, SettingAnswer.Failed,
                $"Daoris could not finish it ({error.Message.TrimEnd('.')}). Ask the person to look in Settings to see what stands now.");
        }

        Log?.Info(
            "help.setting", ("session", request.Session), ("setting", setting), ("value", request.Value), ("repository", request.Repository),
            ("workspace", request.Workspace), ("done", answer.Done), ("code", answer.Code));
        try
        {
            _requests.Answer(request, answer);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unwritten: the tool waits out its time and says the change may still be under way.
        }

        return request;
    }

    /// <summary>
    /// A checkout read (HELPMODE1c4), taken where this loop runs its conversation and handed to <see cref="Checkout"/>, as a setting
    /// request is; what was read, or why not, is written beside it, a failure included, so the tool is never left to wait out its time.
    /// Null where it is not this loop's, or another loop took it.
    /// </summary>
    private async Task<SessionRequest?> ReadCheckoutAsync(SessionRequest pending, CancellationToken ct)
    {
        if (Checkout is not { } read || !_runsHere(pending.Session)) return null;
        if (_requests.Take(pending) is not { } request) return null;

        CheckoutAnswer answer;
        try
        {
            answer = await read(request, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            answer = CheckoutAnswer.Refusal(request.Operation ?? "", CheckoutAnswer.Failed, $"Daoris could not read it ({error.Message.TrimEnd('.')}).");
        }

        try
        {
            _requests.Answer(request, answer);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unwritten: the tool waits out its time and says it may still be under way.
        }

        return request;
    }

    /// <summary>Link 7 on the requests folder (FABRIC-CHAIN2): its wake or its deadline reads the folder, and emits nothing into the chain.</summary>
    private Task<ChainFired> ReadAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct) => Task.FromResult(new ChainFired(Look(), []));

    /// <summary>
    /// One look the change watch asked for: each session's requests begun beside the others (PARALLEL1h), and what a terminal pressed,
    /// heard now rather than after a slow say; how many of both. It waits for none of what it began, so the folder's next word is heard
    /// while a say takes its time.
    /// </summary>
    private int Look()
    {
        var begun = 0;
        try
        {
            var (runs, _, count) = Begin(_closing.Token);
            foreach (var run in runs) Observe(run);
            begun = count;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder held for a moment: the listing behind the watch reads it again.
        }

        try
        {
            // STATEHUB1e: and what a terminal pressed, in the same folder at the same look.
            return begun + Hear().Count;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return begun;
        }
    }

    /// <summary>
    /// A run begun beside the look, watched for what its own catches did not hold: a folder held for a moment is read again by the
    /// listing, and a closing watch's cancellation is its own; anything else is written to the machine log, never lost unobserved.
    /// </summary>
    private void Observe(Task run) =>
        _ = run.ContinueWith(
            failed =>
            {
                if (failed.Exception?.GetBaseException() is { } error and not (IOException or UnauthorizedAccessException)) Log?.Failed("a terminal's request", error);
            },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        if (Changes is not null) await Changes.DisposeAsync().ConfigureAwait(false);

        // PARALLEL1h: what the looks began is cancelled and waited for, so no say or stop outlives the watch.
        await _closing.CancelAsync().ConfigureAwait(false);
        Task[] begun;
        lock (_gate) begun = [.. _claimed.Values];
        await Task.WhenAll(begun).ConfigureAwait(false);
        _closing.Dispose();
    }
}
