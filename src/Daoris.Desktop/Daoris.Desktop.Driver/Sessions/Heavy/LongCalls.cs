namespace Daoris.Driver;

/// <summary>A piece of background work a session's harness says it runs, with when this process heard it announced (FABRIC-PIPELINE1c).</summary>
public sealed record RunningTask(string Id, string Name, DateTimeOffset Since);

/// <summary>
/// A long call said (FABRIC-PIPELINE1c, D192 point 5's fourth layer; the pipeline design §3.2): a running call or a background task of a
/// driven session whose run's checks are <c>light</c>, past its pipeline's <c>callMinutes</c>, is told to the session once, at urgent level,
/// as Daoris's own word (D188 point 2: a fact that makes the work in flight wrong): the call's title and time, and that a suite run whole is
/// the verification pipeline's. Daoris never ends the call: a turn is the agent's (DONEEND1), and the person's <i>Stop</i> stays the door.
/// </summary>
/// <remarks>
/// <para><b>Heard where the calls already are.</b> The call is <see cref="RunningCalls"/>' (WAITSAY1b), the one the rail says with its time;
/// the background work is the protocol door's (<see cref="AcpSession.BackgroundRunning"/>, BGWAIT1). A call held for the person's answer to
/// its permission request is their wait, never the agent's, and is told nothing.</para>
///
/// <para><b>Once a call.</b> Each call is told once, by its start, and each task once, by its id: a second long call is a fact of its
/// own. A word the inbox no longer takes (the run winding up) is not tried again; the next look of a run that goes on finds nothing new.</para>
///
/// <para><b>Only where it reaches the work in flight.</b> Told only where the door takes an urgent word at the next step. Where it takes
/// words only at its turn's end (the native door's run, a protocol door that takes no prompt during a turn) the call has ended before the
/// word could reach it, and a word there would make a turn of its own for a fact no longer in flight: the rail's time says it instead.</para>
///
/// <para><b>On the conversation and the rail.</b> The word is kept on the record as Daoris's (COWORK1c) and shown in the conversation as
/// the inbox tells it; the call it names is marked <see cref="RunningCall.Told"/>, which the rail says beside its time.</para>
///
/// <para><b>The bound.</b> <c>callMinutes</c> is the development pipeline's option beside <c>checks</c> (design §3.1), 20 by default. Under
/// Current no rule sets it, and a steps version's <c>work</c> takes no such field, so their light sessions' bound is the default; a graph
/// version's <c>claimed</c> option is read from the version its run bound, with its <c>checks</c> (FABRIC-PIPELINE1b3, <see cref="MinutesOf"/>),
/// and kept beside them by the start, so every run of the session reads the same (<see cref="SessionChecks.HeldMinutes"/>).</para>
/// </remarks>
public sealed class LongCalls
{
    /// <summary><c>callMinutes</c>' default (the pipeline design §3.1): a judgement the install measures (D192's checks).</summary>
    public const int CallMinutes = 20;

    /// <summary>The option's name, on a graph version's <c>claimed</c> node and beside a session's kept checks.</summary>
    public const string Option = "callMinutes";

    /// <summary>
    /// The <c>callMinutes</c> a run's bound graph version sets on its <c>claimed</c> node (FABRIC-PIPELINE1b3), as written: a whole number of
    /// minutes from one; null where it sets none or none that reads, under Current, and under a steps version, whose bound is the default.
    /// </summary>
    public static int? MinutesOf(WorkflowProcess? process)
    {
        if (process is not { Named: true, Claimed: { } claimed } || !claimed.TryGetValue(Option, out var text)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            return Minutes(document.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>A bound as written: a JSON number that is a whole number of minutes from one, else null, the default.</summary>
    internal static int? Minutes(System.Text.Json.JsonElement value) =>
        value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out var minutes) && minutes >= 1 ? minutes : null;

    /// <summary>How often a light session's calls are looked at: a long call is said within this of its bound.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    /// <summary>The kind a long call's key names (<see cref="DaorisWord.Key"/>): <c>long-call/&lt;session&gt;/…</c>.</summary>
    public const string Kind = "long-call";

    private readonly string _session;
    private readonly TimeSpan _bound;
    private readonly RunningCalls _calls;
    private readonly Func<IReadOnlyList<RunningTask>> _background;
    private readonly DrivenInbox _inbox;
    private readonly ISaidDoor? _door;
    private readonly HashSet<string> _told = new(StringComparer.Ordinal);

    /// <param name="session">The session watched.</param>
    /// <param name="bound">Its pipeline's <c>callMinutes</c>, as a span.</param>
    /// <param name="calls">The record's running calls.</param>
    /// <param name="background">Its background work still running; none where its door says none.</param>
    /// <param name="inbox">Its inbox, which takes Daoris's word as the person's words are taken.</param>
    /// <param name="door">Its record's said door (COWORK1c), or null where the inbox holds the word in this process alone.</param>
    public LongCalls(
        string session, TimeSpan bound, RunningCalls calls, Func<IReadOnlyList<RunningTask>>? background, DrivenInbox inbox, ISaidDoor? door)
    {
        _session = session;
        _bound = bound;
        _calls = calls;
        _background = background ?? (() => []);
        _inbox = inbox;
        _door = door;
    }

    /// <summary>
    /// The bound a session's run takes: its <c>callMinutes</c> under <c>light</c>, <paramref name="minutes"/> where its version set them and
    /// the default where it did not, and none under any other checks.
    /// </summary>
    public static TimeSpan? BoundOf(string? checks, int? minutes = null) =>
        checks == SessionChecks.Light ? TimeSpan.FromMinutes(minutes ?? CallMinutes) : null;

    /// <summary>
    /// One look at <paramref name="now"/>: each call and task past the bound and not yet told, told now at urgent level as Daoris's word.
    /// Answers the keys told.
    /// </summary>
    /// <exception cref="OperationCanceledException">The run is closing.</exception>
    public async Task<IReadOnlyList<string>> LookAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_inbox.ReachAt(MessageLevel.Urgent) != DrivenReach.NextStep) return [];

        var told = new List<string>();
        if (_calls.Calling(_session) is { Waiting: false, Title.Length: > 0 } call && now - call.Since >= _bound)
        {
            var key = $"{Kind}/{_session}/call/{call.Since.UtcTicks}";
            if (_told.Add(key) && await TellAsync(key, Words(call.Title, now - call.Since, _bound, background: false), ct).ConfigureAwait(false))
            {
                _calls.MarkTold(_session, call);
                told.Add(key);
            }
        }

        foreach (var task in _background())
        {
            if (now - task.Since < _bound) continue;
            var key = $"{Kind}/{_session}/task/{task.Id}";
            if (_told.Add(key) && await TellAsync(key, Words(task.Name, now - task.Since, _bound, background: true), ct).ConfigureAwait(false))
            {
                told.Add(key);
            }
        }

        return told;
    }

    /// <summary>Look every <paramref name="every"/> by <paramref name="clock"/> until <paramref name="ct"/> ends: the run's watch.</summary>
    public async Task WatchAsync(TimeProvider clock, TimeSpan every, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(every, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await LookAsync(clock.GetUtcNow(), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The run ended: its watch with it.
        }
    }

    /// <summary>
    /// Watch a session's calls while its run lasts, where its run's checks give a bound; null where they give none. Disposing ends it.
    /// </summary>
    /// <param name="minutes">The <c>callMinutes</c> its start kept beside its checks; null for the default.</param>
    public static IDisposable? Watch(
        string? checks, string session, RunningCalls calls, Func<IReadOnlyList<RunningTask>>? background, DrivenInbox? inbox, ISaidDoor? door,
        Action<string>? failed = null, int? minutes = null)
    {
        if (inbox is null || BoundOf(checks, minutes) is not { } bound) return null;

        var watch = new LongCalls(session, bound, calls, background, inbox, door);
        var stop = new CancellationTokenSource();
        var running = Task.Run(async () =>
        {
            try
            {
                await watch.WatchAsync(TimeProvider.System, Every, stop.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A watch that fails is said, never the run's failure: the call goes on, and only the word is lost.
                failed?.Invoke(error.Message);
            }
        });
        return new Stopping(stop, running);
    }

    private async Task<bool> TellAsync(string key, string text, CancellationToken ct)
    {
        var word = new ChatMessage(text, [])
        {
            Level = MessageLevel.Urgent,
            // No judge: that the call ran past its bound stays true once it ends, and the word still says what to do next.
            Daoris = new DaorisWord(key),
        };
        var kept = await _inbox.KeepOrHoldAsync(_door, word, ct).ConfigureAwait(false);
        return kept.Outcome != InboxKeep.Closed;
    }

    /// <summary>
    /// What the session is told (the pipeline design §3.2's fourth layer): the call's title and time, that a suite run whole is the
    /// verification pipeline's, and that Daoris ends nothing.
    /// </summary>
    public static string Words(string title, TimeSpan ran, TimeSpan bound, bool background)
    {
        var what = background ? $"Your background task `{title}`" : $"Your call `{title}`";
        var stop = background ? "stop that task, " : "do not run it again, ";
        return $"Daoris: {what} has run for {(int)ran.TotalMinutes} minutes, past this pipeline's bound of {(int)bound.TotalMinutes} "
               + "minutes for one call. Your "
               + "checks are light: a test suite or a project run whole is the verification pipeline's, which runs it after your work lands. "
               + $"If this is one, {stop}and run only the checks your change reaches, filtered (a test class, a test file). Daoris does not "
               + "end it.";
    }

    private sealed class Stopping(CancellationTokenSource stop, Task running) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            stop.Cancel();
            // Let go without waiting on it: a look on its way ends at its next await, and owns nothing the run needs.
            _ = running.ContinueWith(_ => stop.Dispose(), TaskScheduler.Default);
        }
    }
}
