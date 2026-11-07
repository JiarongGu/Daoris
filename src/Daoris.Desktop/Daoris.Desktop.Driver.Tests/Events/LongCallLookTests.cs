using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The long call as the session watcher's one look finds it:
/// a light session whose call outlasts its bound is found once, and told once, at urgent level, as Daoris's word kept on its
/// record, and never ended; the rail's call says it is told.
/// </summary>
/// <remarks>
/// Every moment is the test's own: each event carries its stamp, and the watcher's wall clock is handed. The stand-in session is its
/// record's events, its inbox with a door that takes words at the next step, and its record's said door; nothing starts a process.
/// </remarks>
public sealed class LongCallLookTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(CallBound.Default);

    private static readonly TimeSpan Pace = TimeSpan.FromSeconds(15);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-long-calls-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    private DateTimeOffset _wall = T0;

    public LongCallLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionEvent Call(string id, string? status, string? title = null, int at = 0) =>
        new() { Kind = SessionEventKind.Tool, Id = id, Title = title, Status = status, At = T0.AddMinutes(at) };

    /// <summary>The stand-in session's door: what it is sent at its next step, and whether anything ended it.</summary>
    private sealed class Door
    {
        public List<ChatMessage> Sent { get; } = [];

        public int Interrupts { get; private set; }

        public DrivenInbox Inbox(bool nextStep = true)
        {
            var inbox = new DrivenInbox(_ => { });
            inbox.Attach(
                interrupt: () =>
                {
                    Interrupts++;
                    return Task.CompletedTask;
                },
                deliver: nextStep
                    ? message =>
                    {
                        Sent.Add(message);
                        return Task.FromResult("end_turn");
                    }
                    : null);
            inbox.Flow();
            return inbox;
        }
    }

    /// <summary>A running light session: its watcher on a wall clock the test moves, its word, and the looks' long calls told in order.</summary>
    private sealed class Session : IDisposable
    {
        private readonly LongCallLookTests _owner;
        private readonly SessionWatch.WatchHandle _handle;

        public Session(LongCallLookTests owner, SessionEvents record, DrivenInbox inbox, StandInSaid said, Func<IReadOnlyList<RunningTask>>? background = null, TimeSpan? bound = null)
        {
            _owner = owner;
            Events = RunningCalls.Watched(record);
            Calls = RunningCalls.Of(Events);
            Watch = SessionWatch.Of(Events);
            Watch.Wall = () => owner._wall;
            _handle = Watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "claude-code"));
            Watch.LongCalls("s1", bound ?? Bound, background, () => inbox.ReachAt(MessageLevel.Urgent) == DrivenReach.NextStep);
            Word = QuietWord.Tell(Watch, "s1", inbox, said, Calls)!;
            Watch.LongCalled += step => Found.Add(step);
        }

        public SessionEvents Events { get; }

        public RunningCalls Calls { get; }

        public SessionWatch Watch { get; }

        public QuietWord Word { get; }

        public List<LongCallStep> Found { get; } = [];

        /// <summary>One look at <paramref name="minutes"/> after the start: the keys newly found, each told as the run's word tells it.</summary>
        public async Task<IReadOnlyList<string>> LookAtAsync(int minutes)
        {
            _owner._wall = T0.AddMinutes(minutes);
            var before = Found.Count;
            Watch.Look(_ => null, Pace);
            var found = Found.Skip(before).ToList();
            foreach (var step in found) await Word.TellLongAsync(step, _owner._closing.Token);
            return [.. found.Select(step => step.Key)];
        }

        public void Dispose()
        {
            Word.Dispose();
            _handle.Dispose();
        }
    }

    private SessionEvents Record() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    [Fact]
    public async Task A_call_past_its_bound_is_told_once_at_urgent_level_as_Daoris_word_and_never_ended()
    {
        var door = new Door();
        var said = new StandInSaid();
        using var session = new Session(this, Record(), door.Inbox(), said);
        session.Events.Append("s1", Call("c1", "in_progress", "dotnet test src/Daoris.Service", at: 1));

        // Inside its bound: nothing.
        Assert.Empty(await session.LookAtAsync(20));
        Assert.Empty(said.Said);

        var told = Assert.Single(await session.LookAtAsync(21));
        // Once: the next looks while it still runs find nothing more.
        Assert.Empty(await session.LookAtAsync(30));
        Assert.Empty(await session.LookAtAsync(90));

        var kept = Assert.Single(said.Said);
        Assert.Equal((told, MessageLevel.Urgent, DaorisWords.From), (kept.Key, kept.Level, kept.From));
        Assert.StartsWith("long-call/s1/call/", told, StringComparison.Ordinal);
        Assert.Equal(QuietWord.LongWords(session.Found[0]), kept.Text);
        Assert.Contains("`dotnet test src/Daoris.Service` has run for 20 minutes", kept.Text, StringComparison.Ordinal);
        Assert.Contains("verification pipeline's", kept.Text, StringComparison.Ordinal);
        Assert.Contains("Daoris does not end it", kept.Text, StringComparison.Ordinal);
        // Sent at its next step as Daoris's urgent word, under its record's id, and nothing ended or interrupted it.
        var sent = Assert.Single(door.Sent);
        Assert.Equal((kept.Id, MessageLevel.Urgent), (sent.Id, sent.Level));
        Assert.NotNull(sent.Daoris);
        Assert.Equal(0, door.Interrupts);
        // The rail's call says it is told, and still runs.
        Assert.Equal(new RunningCall("dotnet test src/Daoris.Service", T0.AddMinutes(1)) { Told = true }, session.Calls.Calling("s1"));
    }

    [Fact]
    public async Task A_second_long_call_is_a_fact_of_its_own_and_the_told_mark_goes_with_its_call()
    {
        var said = new StandInSaid();
        using var session = new Session(this, Record(), new Door().Inbox(), said);

        session.Events.Append("s1", Call("c1", "in_progress", "npm test", at: 0));
        Assert.Single(await session.LookAtAsync(25));
        session.Events.Append("s1", Call("c1", "completed", at: 26));
        Assert.Null(session.Calls.Calling("s1"));

        session.Events.Append("s1", Call("c2", "in_progress", "npm test", at: 27));
        Assert.False(session.Calls.Calling("s1")!.Told);
        Assert.Single(await session.LookAtAsync(48));
        Assert.Equal(2, said.Said.Count);
        Assert.True(session.Calls.Calling("s1")!.Told);
    }

    [Fact]
    public async Task Background_work_past_its_bound_is_told_once_by_its_id()
    {
        var door = new Door();
        var said = new StandInSaid();
        IReadOnlyList<RunningTask> running = [new("t1", "dotnet test", T0), new("t2", "npm run dev", T0.AddMinutes(10))];
        using var session = new Session(this, Record(), door.Inbox(), said, () => running);

        Assert.Equal(["long-call/s1/task/t1"], await session.LookAtAsync(20));
        Assert.Equal(["long-call/s1/task/t2"], await session.LookAtAsync(30));
        Assert.Empty(await session.LookAtAsync(60));

        Assert.Equal(QuietWord.LongWords(session.Found[0]), said.Said[0].Text);
        Assert.Contains("Your background task `dotnet test` has run for 20 minutes", said.Said[0].Text, StringComparison.Ordinal);
        Assert.Contains("stop that task", said.Said[0].Text, StringComparison.Ordinal);
        Assert.Equal(0, door.Interrupts);
    }

    /// <summary>A door that takes words only at its turn's end is told nothing: the call has ended before a word could reach it.</summary>
    [Fact]
    public async Task A_door_that_takes_words_only_at_its_turns_end_is_told_nothing()
    {
        var said = new StandInSaid();
        using var session = new Session(this, Record(), new Door().Inbox(nextStep: false), said);
        session.Events.Append("s1", Call("c1", "in_progress", "dotnet test", at: 0));

        Assert.Empty(await session.LookAtAsync(60));
        Assert.Empty(said.Said);
        Assert.False(session.Calls.Calling("s1")!.Told);
    }

    /// <summary>The run's word tells what the look finds on its own, with no test to carry it between.</summary>
    [Fact]
    public async Task The_watchers_look_is_told_by_the_run_word_on_its_own()
    {
        var said = new StandInSaid();
        using var session = new Session(this, Record(), new Door().Inbox(), said);
        session.Events.Append("s1", Call("c1", "in_progress", "dotnet test", at: 0));

        _wall = T0.AddMinutes(25);
        session.Watch.Look(_ => null, Pace);

        await Poll.Until(() => said.Said.Count == 1, () => "the long call's word was not kept");
        await Poll.Until(() => session.Calls.Calling("s1")!.Told, () => "the call was not marked told");
    }

    [Fact]
    public void Only_light_checks_give_a_bound()
    {
        Assert.Equal(Bound, CallBound.Of(SessionChecks.Light));
        Assert.Null(CallBound.Of(SessionChecks.HeavyAtEnd));
        Assert.Null(CallBound.Of(null));
        // FABRIC-PIPELINE1b3: a graph version's `callMinutes` is the bound where its checks are light, and nothing under any other.
        Assert.Equal(TimeSpan.FromMinutes(30), CallBound.Of(SessionChecks.Light, 30));
        Assert.Equal(Bound, CallBound.Of(SessionChecks.Light, null));
        Assert.Null(CallBound.Of(SessionChecks.HeavyAtEnd, 30));
    }
}
