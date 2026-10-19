using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WAITSAY1b (D152's UX7c amendment): the call each live session's agent is inside, kept in memory from the record's events as they
/// are written, so the rail can say *running `npm test` for 8 min* of a session whose events it does not hold. Set at the call's
/// start, cleared at its end and at the turn's end; never written, and never read back from the record.
/// </summary>
/// <remarks>Every moment is the test's own: each event carries its stamp. Nothing here starts a process.</remarks>
public sealed class RunningCallsTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-calls-" + Guid.NewGuid().ToString("N")[..8]);

    public RunningCallsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Record() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static SessionEvent Call(string id, string? status, string? title = null, int at = 0) =>
        new() { Kind = SessionEventKind.Tool, Id = id, Title = title, Status = status, At = T0.AddMinutes(at) };

    [Fact]
    public void A_call_is_running_from_its_start_until_its_end()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);

        events.Append("s1", Call("c1", "in_progress", "npm test", at: 1));
        Assert.Equal(new RunningCall("npm test", T0.AddMinutes(1)), calls.Calling("s1"));

        // Its output streamed as updates with no status keeps it running, from when it began.
        events.Append("s1", Call("c1", null, at: 5) with { Output = "PASS 12 of 40" });
        Assert.Equal(new RunningCall("npm test", T0.AddMinutes(1)), calls.Calling("s1"));

        events.Append("s1", Call("c1", "completed", at: 9));
        Assert.Null(calls.Calling("s1"));
        Assert.Empty(calls.Now());
    }

    [Fact]
    public void A_turns_end_clears_every_call_it_left_open()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        events.Append("s1", Call("c1", "pending", "Read README.md"));
        events.Append("s2", Call("c9", "in_progress", "dotnet build", at: 2));

        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled", At = T0.AddMinutes(3) });

        Assert.Null(calls.Calling("s1"));
        // Another session's call is its own.
        Assert.Equal(new RunningCall("dotnet build", T0.AddMinutes(2)), calls.Calling("s2"));
        Assert.Equal(["s2"], calls.Now().Keys);
    }

    [Fact]
    public void The_newest_open_call_is_the_one_running_and_any_end_ends_it_a_refusal_among_them()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        events.Append("s1", Call("c1", "in_progress", "npm test", at: 1));
        events.Append("s1", Call("c2", "in_progress", "git push", at: 2));
        Assert.Equal("git push", calls.Calling("s1")?.Title);

        // A call the rules refused (D52) waits on nobody: it ended, and the one still open beside it is the one running.
        events.Append("s1", Call("c2", "refused", at: 3));
        Assert.Equal(new RunningCall("npm test", T0.AddMinutes(1)), calls.Calling("s1"));

        events.Append("s1", Call("c1", "failed", at: 4));
        Assert.Null(calls.Calling("s1"));
    }

    [Fact]
    public void A_call_this_process_saw_only_updates_of_is_named_once_an_update_names_it()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);

        events.Append("s1", Call("c1", null, at: 1) with { Output = "…" });
        Assert.Null(calls.Calling("s1"));

        // ACP may name a call in a later update: it runs from when this process first heard of it.
        events.Append("s1", Call("c1", "in_progress", "Run the suite", at: 2));
        Assert.Equal(new RunningCall("Run the suite", T0.AddMinutes(1)), calls.Calling("s1"));
    }

    [Fact]
    public void A_new_ask_begins_a_turn_with_nothing_running_and_words_shown_waiting_change_nothing()
    {
        var events = RunningCalls.Watched(Record());
        var calls = RunningCalls.Of(events);
        events.Append("s1", Call("c1", "in_progress", "npm test", at: 1));

        // Words shown the moment they are said (STEER1) are no ask: the call goes on.
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "also the readme", Reaches = "next-step", Id = "w1" });
        Assert.Equal("npm test", calls.Calling("s1")?.Title);

        // The ask a session took is a new turn's: a call the last one left open (a process that died mid-call) is not running.
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "also the readme", Id = "w1" });
        Assert.Null(calls.Calling("s1"));
    }

    [Fact]
    public void It_is_held_in_memory_alone_so_another_reader_of_the_same_record_knows_no_call()
    {
        var events = RunningCalls.Watched(Record());
        events.Append("s1", Call("c1", "in_progress", "npm test"));

        // The record holds the event it was handed and nothing more; a second reader of it was told of no call.
        Assert.Single(events.After("s1", 0).Events);
        Assert.Null(RunningCalls.Of(Record()).Calling("s1"));
        Assert.Same(RunningCalls.Of(events), RunningCalls.Of(events));
    }
}
