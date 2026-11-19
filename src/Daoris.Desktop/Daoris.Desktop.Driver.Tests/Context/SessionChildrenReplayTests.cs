using System.Text.Json;
using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// Each door's captures replayed into a session's record: every subagent's and background task's start and end kept as a
/// <c>child</c> or <c>task</c> event under its own id, its start linked to the call that started it where the wire names one, an end
/// only the session's close implies said as inferred, and a subagent's own words and calls in its own record beside the session's,
/// never in it.
/// </summary>
public sealed class SessionChildrenReplayTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-children-replay-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionEvents _events;
    private readonly ChildRecords _children;

    public SessionChildrenReplayTests()
    {
        Directory.CreateDirectory(_home);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _children = new ChildRecords(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static string[] Capture(params string[] path) =>
        File.ReadAllLines(Path.Combine([WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", .. path]))
            .Where(line => line.Length > 0).ToArray();

    private RecordReadings Sink(string session) => new(session, new SessionOutput(), _events, _children);

    private IReadOnlyList<SessionEvent> Record(string session) => _events.After(session, 0).Events;

    private static IReadOnlyList<SessionEvent> Of(IReadOnlyList<SessionEvent> record, string kind) => [.. record.Where(e => e.Kind == kind)];

    /// <summary>Every start with a call names one this record holds as a call.</summary>
    private static void Linked(IReadOnlyList<SessionEvent> record, params string[] kinds)
    {
        var calls = record.Where(e => e.Kind == SessionEventKind.Tool && e.Id is not null).Select(e => e.Id!).ToHashSet();
        var started = record.Where(e => kinds.Contains(e.Kind) && e.End is null && e.Call is not null).ToList();
        Assert.NotEmpty(started);
        Assert.All(started, start => Assert.Contains(start.Call!, calls));
    }

    private async Task NativeAsync(string session, string[] lines) => await HarnessCapture.CaptureStructuredAsync(
        new StringReader(string.Join('\n', lines)), new StringReader(""), Path.Combine(_home, session + ".log"), Sink(session),
        new ClaudeStreamJson(), prompt: null, CancellationToken.None);

    [Fact]
    public async Task The_native_door_s_family_keeps_each_child_with_its_call_parent_and_depth_and_its_reported_end()
    {
        await NativeAsync("s1", Capture("lineage", "claude-family.stream.jsonl"));

        var children = Of(Record("s1"), SessionEventKind.Child);
        Assert.Equal(
            [
                ("a9b1456decf181940", "toolu_01Ve1SdfhbHQzgFDSCu3ruVT", null, 1, null),
                ("af7e45b4d6f2967a9", "toolu_01VWHzpGu43DRBXQwDoj6f7Z", "a9b1456decf181940", 2, null),
                ("af7e45b4d6f2967a9", null, null, null, ChildEnds.Ok),
                ("a9b1456decf181940", null, null, null, ChildEnds.Ok),
                ("a24ebe27d77bec19f", "toolu_01S7UGDsG67Pe7izoronZZ2E", null, 1, null),
                ("a24ebe27d77bec19f", null, null, null, ChildEnds.Ok),
            ],
            children.Select(e => (e.Id, e.Call, e.Parent, e.Depth, e.End)));
        Assert.All(children, e => Assert.Equal("reported", e.By));
    }

    [Fact]
    public async Task The_native_door_s_background_work_keeps_each_task_with_its_call_and_the_word_it_ended_in()
    {
        await NativeAsync("s1", Capture("native", "claude-background-cut.stream.jsonl"));

        var record = Record("s1");
        Assert.Equal(
            [("b0fetch", "toolu_fetch", "git fetch", null), ("b0fetch", null, null, ChildEnds.Ok),
             ("b1gates", "toolu_gates", "npm run gates", null), ("b1gates", null, null, ChildEnds.Stopped)],
            Of(record, SessionEventKind.Task).Select(e => (e.Id, e.Call, e.Name, e.End)));
        Linked(record, SessionEventKind.Task);
    }

    [Fact]
    public async Task The_native_door_s_subagent_keeps_its_words_and_calls_in_its_own_record_and_its_lifetime_in_the_session_s()
    {
        // The shapes the console3 probe recorded of a subagent's lines (docs/archive/2026-09-30-console3-native-streams-evidence.md).
        await NativeAsync("s1", Capture("native", "subagent-words.shapes.jsonl"));

        var record = Record("s1");
        var child = Assert.Single(Of(record, SessionEventKind.Child), e => e.End is null);
        Assert.Equal(("a1c2e3f4a5b6c7d8e", "toolu_ag", "Read README first line"), (child.Id, child.Call, child.Task));
        Linked(record, SessionEventKind.Child);
        Assert.Equal(ChildEnds.Ok, Assert.Single(Of(record, SessionEventKind.Child), e => e.End is not null).End);

        var own = _children.Read("s1", "a1c2e3f4a5b6c7d8e")!;
        Assert.Contains(own, e => e.Kind == SessionEventKind.Tool && e.Id == "toolu_child_read");
        Assert.Contains(own, e => e.Kind == SessionEventKind.Message && e.Text!.Contains("CHILDWORDS", StringComparison.Ordinal));
        Assert.Equal(Enumerable.Range(1, own.Count).Select(n => (long)n), own.Select(e => e.Seq));
        Assert.DoesNotContain(record, e => e.Id == "toolu_child_read" || (e.Text ?? "").Contains("CHILDWORDS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_claude_protocol_door_keeps_its_subagent_by_its_card_and_each_task_by_the_call_its_progress_names()
    {
        // The updates the console2 probe recorded of claude-agent-acp (docs/archive/2026-09-28-console2-streams-evidence.md).
        await ProtocolAsync("s1", "s-root", Capture("protocol", "claude-acp-beside.shapes.jsonl"));

        var record = Record("s1");
        var child = Assert.Single(Of(record, SessionEventKind.Child), e => e.End is null);
        Assert.Equal(
            ("a8c41f3e0b2d97e15", "a8c41f3e0b2d97e15", 1, "Read README first line", "Read the first line of README.md and say it."),
            (child.Id, child.Call, child.Depth, child.Name, child.Task));
        Assert.Equal(("ok", "reported"), Ended(record, SessionEventKind.Child, "a8c41f3e0b2d97e15"));
        Linked(record, SessionEventKind.Child, SessionEventKind.Task);

        Assert.Equal(
            [("bq7tick", "toolu_tick"), ("bq8serve", "toolu_serve")],
            Of(record, SessionEventKind.Task).Where(e => e.End is null).Select(e => (e.Id, e.Call)));
        // Stopped then completed in one breath: the last word is its end. The server outlived the turn: its end is the close's.
        Assert.Equal(("ok", "reported"), Ended(record, SessionEventKind.Task, "bq7tick"));
        Assert.Equal(("lost", "inferred"), Ended(record, SessionEventKind.Task, "bq8serve"));

        var own = _children.Read("s1", "a8c41f3e0b2d97e15")!;
        Assert.Contains(own, e => e.Kind == SessionEventKind.Tool && e.Id == "toolu_child_read");
        Assert.Contains(own, e => e.Kind == SessionEventKind.Message && e.Text!.Contains("CHILDWORDS", StringComparison.Ordinal));
        Assert.DoesNotContain(record, e => e.Id == "toolu_child_read" || (e.Text ?? "").Contains("CHILDWORDS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Codex_s_door_keeps_each_task_by_the_call_its_announcement_names_and_says_no_subagent()
    {
        var frames = Capture("providers", "codex", "corpus.jsonl")
            .Select(line => JsonSerializer.Serialize(new { from = "s-root", update = JsonDocument.Parse(line).RootElement.GetProperty("frame") }))
            .ToArray();

        await ProtocolAsync("s1", "s-root", frames);

        var record = Record("s1");
        Assert.Empty(Of(record, SessionEventKind.Child));
        Assert.Equal(("call_m1", "dotnet test --filter Census"), Of(record, SessionEventKind.Task).Where(e => e.End is null).Select(e => (e.Call!, e.Name!)).Single());
        Linked(record, SessionEventKind.Task);
        Assert.Equal(("lost", "inferred"), Ended(record, SessionEventKind.Task, "call_m1"));
    }

    [Fact]
    public void Each_door_states_what_it_says_of_its_children_and_tasks_and_what_it_can_stop()
    {
        var built = ProviderRegistry.Built();

        Assert.Equal(new BesideAbilities(true, true, false, false), DoorAbilities.Of(built.Door("claude-code")).Beside);
        Assert.Equal(new BesideAbilities(true, true, true, false), DoorAbilities.Of(built.Door("claude-code-acp")).Beside);
        Assert.Equal(new BesideAbilities(false, true, true, false), DoorAbilities.Of(built.Door("codex-acp")).Beside);
    }

    [Fact]
    public void A_replayed_reading_an_end_with_no_start_and_a_second_end_keep_nothing()
    {
        var lifetimes = new ChildLifetimes();

        Assert.Null(lifetimes.Read(new WireReading.ChildSeen("c1", ChildSignal.Stream) { Replayed = true }));
        Assert.Null(lifetimes.Read(new WireReading.TaskEnded("t0", "completed")));
        Assert.NotNull(lifetimes.Read(new WireReading.TaskStarted("t1")));
        Assert.Equal(ChildEnds.Lost, lifetimes.Read(new WireReading.StreamEnded("task/t1", SessionStream.SessionEnded))!.End);
        // The word its door says after the close's is how it ended; nothing after that.
        Assert.Equal(("stopped", "reported"), lifetimes.Read(new WireReading.TaskEnded("t1", "killed")) is { } said ? (said.End, said.By) : default);
        Assert.Null(lifetimes.Read(new WireReading.TaskEnded("t1", "completed")));
        Assert.Null(lifetimes.Read(new WireReading.StreamEnded("task/t1", SessionStream.SessionEnded)));
    }

    /// <summary>A task the person stopped keeps who stopped it on the end its door says; one its agent ended keeps no one.</summary>
    [Fact]
    public void A_task_the_person_stopped_keeps_who_on_its_end()
    {
        var lifetimes = new ChildLifetimes(task => task == "t1");
        lifetimes.Read(new WireReading.TaskStarted("t1"));
        lifetimes.Read(new WireReading.TaskStarted("t2"));

        Assert.Equal(("stopped", "person"), lifetimes.Read(new WireReading.TaskEnded("t1", "killed")) is { } mine ? (mine.End, mine.Origin) : default);
        Assert.Equal(("stopped", null), lifetimes.Read(new WireReading.TaskEnded("t2", "killed")) is { } its ? (its.End, its.Origin) : default);
    }

    private static (string?, string?) Ended(IReadOnlyList<SessionEvent> record, string kind, string id) =>
        record.Last(e => e.Kind == kind && e.Id == id && e.End is not null) is var end ? (end.End, end.By) : default;

    /// <summary>The protocol door's updates as its session reads them: what its streams take, and the session's own mapped into its record.</summary>
    private async Task ProtocolAsync(string session, string root, string[] lines)
    {
        var sink = Sink(session);
        var streams = new AcpStreams(
            ReadingStreams.Of(sink)!, new AcpConsole(_ => { }, AcpConsole.Quiet), e => sink.Read(new WireReading.Said(e)), AcpConsole.Quiet,
            children: sink);
        foreach (var line in lines)
        {
            using var parsed = JsonDocument.Parse(line);
            var from = parsed.RootElement.GetProperty("from").GetString();
            var update = parsed.RootElement.GetProperty("update").Clone();
            if (!streams.Take(from, root, update) && AcpRender.Map(update) is { } own) sink.Read(new WireReading.Said(own));
        }

        await streams.EndAllAsync();
    }
}
