namespace Daoris.Driver;

/// <summary>A line the transport is asked to run: the repository, its workspace and the line by name.</summary>
public sealed record IntegrationLineSend(string Repository, string Workspace, string Line);

/// <summary>Where a unit's <c>integrate</c> node hands the line its request waits in (INTEGRATE1g): the transport, or a test's stand-in.</summary>
public interface IIntegrationLines
{
    /// <summary>Take the line to run: it returns at once, and the run goes on off the caller's task. A line already waiting is taken once.</summary>
    void Send(IntegrationLineSend send);
}

/// <summary>What the transport runs with: the person's choices, each repository's registered checkout, the say door, and who it tells.</summary>
public sealed record IntegrationTransportWorld
{
    public Func<DriverConfig> Config { get; init; } = () => DriverConfig.Empty;

    /// <summary>A repository's registered checkout in its workspace; null where this machine has none, and its line is not run.</summary>
    public Func<string, string, CancellationToken, Task<string?>> Root { get; init; } = (_, _, _) => Task.FromResult<string?>(null);

    /// <summary>The say door a member's session is told its conflict through (§5.4's <c>session</c>); null holds every such member.</summary>
    public RebaseTelling.Say? Say { get; init; }

    /// <summary>The landing's own clean-up the tidy step hands a settled landed branch to; null removes nothing.</summary>
    internal IntegrationTidy? Tidy { get; init; }

    /// <summary>The reading of the start's confirmation the store is opened with; null reads this install's.</summary>
    public Func<StoreSettled>? Settled { get; init; }

    /// <summary>Each event of a line's run (§5.2), as its step ends.</summary>
    public Action<IntegrationEvent>? Told { get; init; }

    /// <summary>What a run said, a line each.</summary>
    public Action<string>? Said { get; init; }

    /// <summary>A run closed a request: the runtime's pass is asked, so each <c>integrate</c> node reads its request's outcome.</summary>
    public Action? Answered { get; init; }

    /// <summary>Whether a line may run now (the home's lease held here, no update draining); one that may not keeps its place and is tried again later.</summary>
    public Func<bool> Runs { get; init; } = () => true;

    /// <summary>How long a line whose candidate is held or waits, or whose gates wait for the slots, is let be before it is tried again.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// The integration transport (INTEGRATE1g; the integration design §5.1, §5.6, §17.4): each line's run, one per line, on the transport's own
/// task and never the pass's, as FABRIC-PIPELINE1d's heavy executor runs a check. A unit's <c>integrate</c> node sends the line its
/// admitted request waits in; the come-up sends every line a standing candidate or an open request is in. Its answer asks the pass, which reads
/// each request's outcome onto its node.
/// </summary>
/// <remarks>
/// <para><b>Shared with the heavy executor.</b> A line whose rule declares its gates runs holding the home's verification slot and then the
/// repository's heavy-run slot, each tried without waiting, as a check does, so a candidate's gates and a check never run in one repository at
/// once; a line with no gates takes neither. A line whose slots are held waits, said, and is tried again after <see cref="IntegrationTransportWorld.Poll"/>
/// or at the next send.</para>
/// </remarks>
public sealed class IntegrationTransport(string home, IntegrationTransportWorld? world = null) : IIntegrationLines, IAsyncDisposable
{
    private readonly IntegrationTransportWorld _world = world ?? new IntegrationTransportWorld();
    private readonly Lock _gate = new();
    private readonly List<IntegrationLineSend> _queue = [];
    private readonly Dictionary<IntegrationLineSend, DateTimeOffset> _notBefore = [];
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _ends = new();
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _loop;

    private IntegrationStore Store() => new(home, _world.Settled);

    /// <inheritdoc/>
    /// <remarks>A line whose last run left its candidate held or waiting, or whose slots were held, keeps its wait: a pass sends it at every look.</remarks>
    public void Send(IntegrationLineSend send)
    {
        lock (_gate)
        {
            if (!_queue.Contains(send)) _queue.Add(send);
            _loop ??= Task.Run(LoopAsync);
        }

        Interlocked.Exchange(ref _wake, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    /// <summary>Start the transport: the come-up's lines, those a standing candidate or an open request is in, are sent.</summary>
    public void Start()
    {
        lock (_gate) _loop ??= Task.Run(LoopAsync);
    }

    public async ValueTask DisposeAsync()
    {
        await _ends.CancelAsync().ConfigureAwait(false);
        _wake.TrySetResult();
        if (_loop is { } loop)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _ends.Dispose();
    }

    private async Task LoopAsync()
    {
        ComeUp();
        while (!_ends.IsCancellationRequested)
        {
            var woken = Volatile.Read(ref _wake).Task;
            IntegrationLineSend? next;
            DateTimeOffset? soonest;
            var now = DateTimeOffset.UtcNow;
            lock (_gate)
            {
                next = _queue.FirstOrDefault(send => !_notBefore.TryGetValue(send, out var at) || at <= now);
                if (next is not null) _queue.Remove(next);
                soonest = _queue.Select(send => _notBefore.TryGetValue(send, out var at) ? at : (DateTimeOffset?)null).Min();
            }

            if (next is null)
            {
                List<Task> waits = [woken];
                if (soonest is { } at && at - now is var until && until > TimeSpan.Zero) waits.Add(Task.Delay(until, _ends.Token));
                await Task.WhenAny(waits).ConfigureAwait(false);
                continue;
            }

            try
            {
                await RunAsync(next, _ends.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_ends.IsCancellationRequested)
            {
                return; // The driver ends: a candidate cut here is the next come-up's.
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or DriverException)
            {
                _world.Said?.Invoke($"integration: `{next.Repository}`'s line `{next.Line}` was not run ({error.Message}); it is tried again at the next send.");
            }
        }
    }

    /// <summary>The come-up (§5.6): every line a standing candidate or an open request is in, sent once.</summary>
    private void ComeUp()
    {
        try
        {
            var store = Store();
            var lines = store.Standing().Select(candidate => new IntegrationLineSend(candidate.Repository, candidate.Workspace, candidate.Line))
                .Concat(store.Open().Select(request => new IntegrationLineSend(request.Repository, request.Workspace, request.Line)));
            lock (_gate)
            {
                foreach (var line in lines.Where(line => !_queue.Contains(line))) _queue.Add(line);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            _world.Said?.Invoke($"integration: the standing candidates were not read at the come-up ({error.Message}); each line runs at its next send.");
        }
    }

    /// <summary>One line's run to rest, now, on the caller's task: what the loop runs, and what a test asks.</summary>
    internal async Task<IntegrationLineRun?> RunAsync(IntegrationLineSend send, CancellationToken ct)
    {
        if (!_world.Runs())
        {
            lock (_gate)
            {
                _notBefore[send] = DateTimeOffset.UtcNow + _world.Poll;
                if (!_queue.Contains(send)) _queue.Add(send);
            }

            return null;
        }

        if (await _world.Root(send.Repository, send.Workspace, ct).ConfigureAwait(false) is not { } root)
        {
            _world.Said?.Invoke($"integration: `{send.Repository}` has no checkout here, so its line `{send.Line}` is not run.");
            return null;
        }

        var store = Store();
        Tell(store, send);
        var config = _world.Config();
        var asks = new IntegrationSweepAsks(
            new IntegrationReplayAsk(send.Repository, send.Workspace, root, send.Line) { Say = _world.Say, Config = config },
            new IntegrationPromoteAsk(send.Repository, send.Workspace, root, send.Line, config, home) { Tidy = _world.Tidy });

        VerifySlot? verifying = null;
        HeavySlot? heavy = null;
        if (Gated(store, send))
        {
            verifying = VerifySlot.TryTake(home);
            heavy = verifying is null || HeavySlot.KeyOf(root) is not { } key ? null : HeavySlot.TryTake(home, key);
            if (heavy is null)
            {
                verifying?.Dispose();
                Later(send, $"integration: `{send.Repository}`'s line `{send.Line}` waits for {(verifying is null ? "the verification running in this home" : "the repository's heavy run")}.");
                return null;
            }
        }

        IntegrationLineRun run;
        try
        {
            run = await new IntegrationLines(store, DaorisHome.Entry(home, HomeEntry.Trees)).RunAsync(asks, ct).ConfigureAwait(false);
        }
        finally
        {
            heavy?.Dispose();
            verifying?.Dispose();
        }

        // A candidate left standing is held or waits for its verdict: tried again once the wait is over, never at every pass.
        var waits = store.StandingFor(send.Repository, send.Line) is not null;
        lock (_gate)
        {
            if (waits) _notBefore[send] = DateTimeOffset.UtcNow + _world.Poll;
            else _notBefore.Remove(send);
        }

        foreach (var told in run.Events) _world.Told?.Invoke(told);
        foreach (var line in run.Said) _world.Said?.Invoke($"integration: {line}");
        if (run.Closed) _world.Answered?.Invoke();
        return run;
    }

    /// <summary>Each request newly open in the line, told queued once (§5.2 step 1).</summary>
    private void Tell(IntegrationStore store, IntegrationLineSend send)
    {
        foreach (var request in store.Queued(send.Repository, send.Line))
        {
            bool fresh;
            lock (_gate) fresh = _queued.Add(request.Id);
            if (fresh) _world.Told?.Invoke(new IntegrationEvent(IntegrationEventCodes.Queued, send.Repository, send.Line) { Request = request.Id });
        }
    }

    /// <summary>Whether the line's next candidate runs gates: its standing candidate's first member's rule, else its first queued request's.</summary>
    private static bool Gated(IntegrationStore store, IntegrationLineSend send)
    {
        var first = store.StandingFor(send.Repository, send.Line) is { } standing
            ? store.Members(standing.Id).OrderBy(member => member.Position).Select(member => store.Request(member.Request)).FirstOrDefault()
            : store.Queued(send.Repository, send.Line).FirstOrDefault();
        return first?.Policy.Gates == LandingIntegrate.Declared;
    }

    private void Later(IntegrationLineSend send, string why)
    {
        lock (_gate)
        {
            _notBefore[send] = DateTimeOffset.UtcNow + _world.Poll;
            if (!_queue.Contains(send)) _queue.Add(send);
        }

        _world.Said?.Invoke(why);
    }
}
