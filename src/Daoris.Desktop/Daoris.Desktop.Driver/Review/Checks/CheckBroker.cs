namespace Daoris.Driver;

/// <summary>How a check is run: the heavy run's own door, <see cref="HeavyRun.RunAsync"/>, which a test stands in for.</summary>
public delegate Task<int> BrokeredRun(HeavyRunAsk ask, HeavyRunWorld world, TextWriter output, CancellationToken ct);

/// <summary>
/// The check broker: runs the asks of the sessions' <c>check_run</c> as heavy runs in their sessions' trees under the machine's one
/// admission (<see cref="CheckAdmission"/>), as many at once as it admits, and writes each verdict (<see cref="CheckHanding"/> carries it back).
/// </summary>
/// <remarks>
/// <para>The broker has no queue and no limit of its own. Every due ask starts at once and waits its turn (<see cref="AdmissionOrder"/>) in
/// the one order, its quest's class then its arrival; how many run is the admission's alone. A waiting ask's state says what it waits for.
/// A command of the asker's own takes no repository slot and waits first come for the machine's places, as a terminal's does.</para>
/// <para>One broker seat per home (<c>checks/broker.lock</c>) keeps a second Daoris process from starting a check the first holds. A run
/// the state calls running that this broker does not hold ended with a restart and is queued again; one cut off by the driver's end before
/// it had its slots is queued, never a verdict.</para>
/// </remarks>
public sealed class CheckBroker(string home, Func<string, CancellationToken, Task<string?>> folderOf, BrokeredRun? run = null, TimeProvider? clock = null)
{
    /// <summary>The most of a log's end a verdict keeps, in characters.</summary>
    public const int TailBound = 4000;

    /// <summary>How long a check Daoris could not run waits before it is asked again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly CheckFiles _files = new(home);
    private readonly BrokeredRun _run = run ?? HeavyRun.RunAsync;
    private readonly AdmissionOrder _order = new();
    private readonly Lock _gate = new();
    private readonly Lock _classesGate = new();
    private readonly Dictionary<string, Task> _active = new(StringComparer.Ordinal);
    private IReadOnlyList<PriorityEvent> _classes = [];
    private DateTimeOffset _classesAt = DateTimeOffset.MinValue;

    /// <summary>The quest a session serves, whose class orders its checks; null for a session serving none.</summary>
    public Func<string, string?> QuestOf { get; init; } = _ => null;

    public Func<CheckRules>? Rules { get; init; }

    public HeavyWaiting Waiting { get; init; } = HeavyWaiting.Default;

    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(2);

    public Action<CheckState>? Settled { get; init; }

    public int Holding
    {
        get
        {
            lock (_gate) return _active.Count;
        }
    }

    /// <summary>The asks due at <paramref name="now"/>: none answered yet, queued, or errored with its delay past, in the order asked.</summary>
    public static IReadOnlyList<CheckAsk> Pending(IReadOnlyList<CheckAsk> asks, Func<string, CheckState?> state, DateTimeOffset now) =>
        [.. asks.Where(ask => state(ask.Id) is null or { State: CheckStates.Queued }
                || (state(ask.Id) is { State: CheckStates.Errored } errored && errored.At + RetryAfter <= now))
            .OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal)];

    /// <summary>
    /// Look over the asks until none waits or runs: each due one starts at once and waits its turn. Returns at once where another Daoris
    /// process on this home holds the seat.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        using var seat = _files.TakeSeat();
        if (seat is null) return;

        var recovered = false;
        while (!ct.IsCancellationRequested)
        {
            var asks = _files.Asks();
            if (!recovered)
            {
                recovered = true;
                foreach (var ask in asks.Where(each => _files.ReadState(each.Id) is { State: CheckStates.Running }))
                {
                    _files.WriteState(new CheckState(ask.Id, CheckStates.Queued, _clock.GetUtcNow()));
                }
            }

            Task[] running;
            lock (_gate)
            {
                foreach (var ask in Pending(asks, _files.ReadState, _clock.GetUtcNow()).Where(each => !_active.ContainsKey(each.Id)))
                {
                    // Joined before any of the pass starts, so the order holds however long each one's tree takes to read.
                    var turn = ask.Command is null ? _order.Join(ask.Id, () => PlaceOf(ask)) : null;
                    _active[ask.Id] = Task.Run(() => OneAsync(ask, turn, ct), CancellationToken.None);
                }

                running = [.. _active.Values];
            }

            if (running.Length == 0) return;
            await Task.WhenAny([.. running, Task.Delay(Poll, _clock, CancellationToken.None)]).ConfigureAwait(false);
        }

        Task[] ending;
        lock (_gate) ending = [.. _active.Values];
        await Task.WhenAll(ending).ConfigureAwait(false);
    }

    private async Task OneAsync(CheckAsk ask, AdmissionTurn? turn, CancellationToken ct)
    {
        try
        {
            if (await RunOneAsync(ask, turn, ct).ConfigureAwait(false) is { } state) Settled?.Invoke(state);
        }
        finally
        {
            turn?.Dispose();
            lock (_gate) _active.Remove(ask.Id);
        }
    }

    private async Task<CheckState?> RunOneAsync(CheckAsk ask, AdmissionTurn? turn, CancellationToken ct)
    {
        if (await folderOf(ask.Session, ct).ConfigureAwait(false) is not { Length: > 0 } folder || !Directory.Exists(folder))
        {
            return Keep(new CheckState(ask.Id, CheckStates.Errored, _clock.GetUtcNow()) { Error = "its session has no tree on this machine to run in." });
        }

        string? tip = null;
        var admitted = false;
        try
        {
            tip = await TipAsync(folder, ct).ConfigureAwait(false);
            void Running()
            {
                admitted = true;
                _files.WriteState(new CheckState(ask.Id, CheckStates.Running, _clock.GetUtcNow()) { Tip = tip });
            }

            if (turn is not null)
            {
                _files.WriteState(new CheckState(ask.Id, CheckStates.Queued, _clock.GetUtcNow()) { Tip = tip });
                turn.WhenRefused = wait => Waits(ask, tip, wait);
                turn.WhenAdmitted = Running;
            }
            else
            {
                Running();
            }

            int exit;
            await using (var log = _files.OpenLogWriter(ask.Id))
            {
                exit = await _run(new HeavyRunAsk(ask.Gate) { Command = ask.Command }, WorldOf(folder, ask, turn), log, ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested && (!admitted || exit != 0)) return Cut(ask, tip, admitted);
            return Keep(new CheckState(ask.Id, exit == 0 ? CheckStates.Reached : exit == 2 ? CheckStates.Errored : CheckStates.Failed, _clock.GetUtcNow())
            {
                Exit = exit, Tail = TailOf(ask.Id), Tip = tip, Error = exit == 2 ? TailOf(ask.Id) : null,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Cut(ask, tip, admitted);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return Keep(new CheckState(ask.Id, CheckStates.Errored, _clock.GetUtcNow()) { Error = error.Message, Tip = tip });
        }
    }

    private HeavyRunWorld WorldOf(string folder, CheckAsk ask, AdmissionTurn? turn)
    {
        var world = new HeavyRunWorld(home, folder, System.Environment.GetEnvironmentVariable);
        return world with
        {
            Log = _files.PathOf(ask.Id, "gate.log"), Turn = turn, Waiting = Waiting,
            Checks = Rules ?? (() => world.Rules() with { World = RoomWorld.Machine }),
        };
    }

    /// <summary>An ask's place in the order, read again at each look: its quest's class (<see cref="PriorityBook.OfRun"/>), then its arrival.</summary>
    private QueuePlace PlaceOf(CheckAsk ask)
    {
        var now = _clock.GetUtcNow();
        IReadOnlyList<PriorityEvent> events;
        lock (_classesGate)
        {
            if (now - _classesAt >= TimeSpan.FromSeconds(1) || now < _classesAt)
            {
                _classes = PriorityBook.Read(home);
                _classesAt = now;
            }

            events = _classes;
        }

        var @class = QuestOf(ask.Session) is { Length: > 0 } quest ? PriorityBook.OfRun(events, quest, null, now).Class : PriorityClasses.Normal;
        return new QueuePlace(@class, QueueStage.FirstChecks, ask.At);
    }

    private void Waits(CheckAsk ask, string? tip, CheckWait wait)
    {
        if (_files.ReadState(ask.Id) is { State: CheckStates.Queued, Wait: { } before } && before.Sentence == wait.Sentence) return;
        _files.WriteState(new CheckState(ask.Id, CheckStates.Queued, _clock.GetUtcNow()) { Tip = tip, Wait = wait });
    }

    private CheckState? Cut(CheckAsk ask, string? tip, bool admitted)
    {
        if (admitted) return Keep(new CheckState(ask.Id, CheckStates.Stopped, _clock.GetUtcNow()) { Tail = TailOf(ask.Id), Tip = tip });
        _files.WriteState(new CheckState(ask.Id, CheckStates.Queued, _clock.GetUtcNow()) { Tip = tip });
        return null;
    }

    private CheckState Keep(CheckState state)
    {
        _files.WriteState(state);
        return state;
    }

    private string? TailOf(string id)
    {
        if (_files.ReadLog(id) is not { } text) return null;
        if (text.Length <= TailBound) return text;
        var cut = text[^TailBound..];
        var line = cut.IndexOf('\n');
        return line >= 0 && line + 1 < cut.Length ? cut[(line + 1)..] : cut;
    }

    private static async Task<string?> TipAsync(string folder, CancellationToken ct)
    {
        var (code, tip, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", "rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 && tip.Trim() is { Length: > 0 } read ? read : null;
    }
}
