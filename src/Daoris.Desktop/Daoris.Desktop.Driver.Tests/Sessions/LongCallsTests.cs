using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1c (D192 point 5's fourth layer; the pipeline design §3.2, §7's proof): a stand-in session under light checks whose call
/// outlasts its bound is told once, at urgent level, as Daoris's word kept on its record, and never ended; the rail's call says it was told.
/// </summary>
/// <remarks>
/// Every moment is the test's own: each event carries its stamp, and each look is handed its <c>now</c>. The stand-in session is its
/// record's events, its inbox with a door that takes words at the next step, and its record's said door; nothing starts a process.
/// </remarks>
public sealed class LongCallsTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(LongCalls.CallMinutes);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-long-calls-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public LongCallsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Record() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static SessionEvent Call(string id, string? status, string? title = null, int at = 0) =>
        new() { Kind = SessionEventKind.Tool, Id = id, Title = title, Status = status, At = T0.AddMinutes(at) };

    /// <summary>The stand-in session's door: what it was sent at its next step, and whether anything ended it.</summary>
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

    [Fact]
    public async Task A_call_past_its_bound_is_told_once_at_urgent_level_as_Daoris_word_and_never_ended()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        var door = new Door();
        var said = new StandInSaid();
        var watch = new LongCalls("s1", Bound, calls, null, door.Inbox(), said);
        events.Append("s1", Call("c1", "in_progress", "dotnet test src/Daoris.Service", at: 1));

        // Inside its bound: nothing.
        Assert.Empty(await watch.LookAsync(T0.AddMinutes(20), _closing.Token));
        Assert.Empty(said.Said);

        var told = Assert.Single(await watch.LookAsync(T0.AddMinutes(21), _closing.Token));
        // Once: the next looks while it still runs tell nothing more.
        Assert.Empty(await watch.LookAsync(T0.AddMinutes(30), _closing.Token));
        Assert.Empty(await watch.LookAsync(T0.AddMinutes(90), _closing.Token));

        var kept = Assert.Single(said.Said);
        Assert.Equal((told, MessageLevel.Urgent, DaorisWords.From), (kept.Key, kept.Level, kept.From));
        Assert.StartsWith("long-call/s1/call/", told, StringComparison.Ordinal);
        Assert.Equal(LongCalls.Words("dotnet test src/Daoris.Service", TimeSpan.FromMinutes(20), Bound, background: false), kept.Text);
        Assert.Contains("`dotnet test src/Daoris.Service` has run for 20 minutes", kept.Text, StringComparison.Ordinal);
        Assert.Contains("verification pipeline's", kept.Text, StringComparison.Ordinal);
        Assert.Contains("Daoris does not end it", kept.Text, StringComparison.Ordinal);
        // Sent at its next step as Daoris's urgent word, under its record's id, and nothing ended or interrupted it.
        var sent = Assert.Single(door.Sent);
        Assert.Equal((kept.Id, MessageLevel.Urgent), (sent.Id, sent.Level));
        Assert.NotNull(sent.Daoris);
        Assert.Equal(0, door.Interrupts);
        // The rail's call says it was told, and still runs.
        Assert.Equal(new RunningCall("dotnet test src/Daoris.Service", T0.AddMinutes(1)) { Told = true }, calls.Calling("s1"));
    }

    [Fact]
    public async Task A_second_long_call_is_a_fact_of_its_own_and_the_told_mark_goes_with_its_call()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        var door = new Door();
        var said = new StandInSaid();
        var watch = new LongCalls("s1", Bound, calls, null, door.Inbox(), said);

        events.Append("s1", Call("c1", "in_progress", "npm test", at: 0));
        Assert.Single(await watch.LookAsync(T0.AddMinutes(25), _closing.Token));
        events.Append("s1", Call("c1", "completed", at: 26));
        Assert.Null(calls.Calling("s1"));

        events.Append("s1", Call("c2", "in_progress", "npm test", at: 27));
        Assert.False(calls.Calling("s1")!.Told);
        Assert.Single(await watch.LookAsync(T0.AddMinutes(48), _closing.Token));
        Assert.Equal(2, said.Said.Count);
        Assert.True(calls.Calling("s1")!.Told);
    }

    [Fact]
    public async Task Background_work_past_its_bound_is_told_once_by_its_id()
    {
        var events = RunningCalls.Watched(Record());
        var door = new Door();
        var said = new StandInSaid();
        IReadOnlyList<RunningTask> running = [new("t1", "dotnet test", T0), new("t2", "npm run dev", T0.AddMinutes(10))];
        var watch = new LongCalls("s1", Bound, RunningCalls.Of(events), () => running, door.Inbox(), said);

        Assert.Equal(["long-call/s1/task/t1"], await watch.LookAsync(T0.AddMinutes(20), _closing.Token));
        Assert.Equal(["long-call/s1/task/t2"], await watch.LookAsync(T0.AddMinutes(30), _closing.Token));
        Assert.Empty(await watch.LookAsync(T0.AddMinutes(60), _closing.Token));

        Assert.Equal(
            LongCalls.Words("dotnet test", TimeSpan.FromMinutes(20), Bound, background: true),
            said.Said[0].Text);
        Assert.Contains("stop that task", said.Said[0].Text, StringComparison.Ordinal);
        Assert.Equal(0, door.Interrupts);
    }

    /// <summary>A door that takes words only at its turn's end is told nothing: the call has ended before a word could reach it.</summary>
    [Fact]
    public async Task A_door_that_takes_words_only_at_its_turns_end_is_told_nothing()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        var said = new StandInSaid();
        events.Append("s1", Call("c1", "in_progress", "dotnet test", at: 0));

        var turnEnd = new LongCalls("s1", Bound, calls, null, new Door().Inbox(nextStep: false), said);
        Assert.Empty(await turnEnd.LookAsync(T0.AddMinutes(60), _closing.Token));
        Assert.Empty(said.Said);
        Assert.False(calls.Calling("s1")!.Told);
    }

    [Fact]
    public void Only_light_checks_give_a_bound_and_a_watch()
    {
        Assert.Equal(Bound, LongCalls.BoundOf(SessionChecks.Light));
        Assert.Null(LongCalls.BoundOf(SessionChecks.HeavyAtEnd));
        Assert.Null(LongCalls.BoundOf(null));

        var calls = RunningCalls.Of(Record());
        var inbox = new Door().Inbox();
        Assert.Null(LongCalls.Watch(null, "s1", calls, null, inbox, null));
        Assert.Null(LongCalls.Watch(SessionChecks.HeavyAtEnd, "s1", calls, null, inbox, null));
        Assert.Null(LongCalls.Watch(SessionChecks.Light, "s1", calls, null, null, null));
        using var watching = LongCalls.Watch(SessionChecks.Light, "s1", calls, null, inbox, null);
        Assert.NotNull(watching);
    }
}
