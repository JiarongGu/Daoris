using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <see cref="TransportRun"/> over both doors, run as real model-free processes, observed as a start observes
/// them. Every executor's run holds it (through <c>Driver.HoldAsync</c>), so this is that run's unit: the downloads kept,
/// the process bracketed by its own lineage token, started through the agents' route and tracked, a reaper beside it, the capture reading its
/// streams into the transcript and the record, a native prompt on its input, the wait on its exit, the capture bounded past it, and the end
/// read the way the conclusion reads it, for its first process, its go-on's and its retry's alike. A native door's one program is a Node
/// script that speaks the stream the Claude Code mapper reads; a protocol door's is the stand-in's own.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class TransportRunProcessTests : IDisposable
{
    private const string Script = """
        const fs = require('fs');
        const [mode, ...rest] = process.argv.slice(2);
        const init = id => console.log(JSON.stringify({ type: 'system', subtype: 'init', session_id: id, cwd: 'x', tools: [], model: 'm' }));
        const said = text => console.log(JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text }] } }));
        const result = () => console.log(JSON.stringify({ type: 'result', subtype: 'success', is_error: false, result: 'ok' }));
        switch (mode) {
          case 'plain': console.log('hello out'); console.error('hello err'); process.exit(3); break;
          case 'claude': init('conv-1'); said('Logged the port.'); console.error('the harness says this'); result(); break;
          case 'again': {
            if (!fs.existsSync(rest[0])) { fs.writeFileSync(rest[0], '1'); console.log('not a frame'); process.exit(1); }
            init('conv-1'); said('second run'); result(); break;
          }
          case 'againhangs': {
            if (!fs.existsSync(rest[0])) { fs.writeFileSync(rest[0], '1'); process.exit(1); }
            setInterval(() => {}, 1000); break;
          }
          case 'resume': {
            let heard = '';
            process.stdin.setEncoding('utf8');
            process.stdin.on('data', chunk => { heard += chunk; });
            process.stdin.on('end', () => { init('conv-1'); said('args: ' + rest.join(' ')); said('heard: ' + heard); result(); });
            break;
          }
          case 'token': console.log('token ' + (process.env.DAORIS_LINEAGE_PROC ?? '')); break;
          case 'stdin': {
            let heard = '';
            process.stdin.setEncoding('utf8');
            process.stdin.on('data', chunk => { heard += chunk; });
            process.stdin.on('end', () => { init('conv-1'); said('heard: ' + heard); result(); });
            break;
          }
          case 'hangs': setInterval(() => {}, 1000); break;
          case 'talks': {
            init('conv-1'); said('ready'); result();
            process.stdin.on('data', () => {});
            process.stdin.on('end', () => process.exit(0));
            break;
          }
        }
        """;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-transport-run-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ServiceClient _service = new("http://127.0.0.1:9", null);

    private readonly SessionProcesses _processes;

    private readonly Daoris.Driver.Driver _driver;

    private readonly SessionEvents _events;

    private readonly List<SessionEvent> _heard = [];

    private readonly CancellationTokenSource _bound = new(Poll.Patience);

    public TransportRunProcessTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path.Combine(_home, "agent.cjs"), Script);
        Directory.CreateDirectory(Path.Combine(_home, "engine"));
        _processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        // The driver's registry holds the shipped doors alone: a go-on's process is prepared from the registry its run started from,
        // whose door it is, never from one the driver holds now.
        _driver = new Daoris.Driver.Driver(_service, DriverConfig.Empty, ProviderRegistry.Built(), _home, _processes);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _events.Evented += (_, e) =>
        {
            lock (_heard) _heard.Add(e);
        };
    }

    public void Dispose()
    {
        _bound.Dispose();
        _service.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private CancellationToken Bound => _bound.Token;

    private string Transcript => Path.Combine(_home, "s1.log");

    private SessionTarget Target() => new("q-1", "title", "body", "asker", "engine", Path.Combine(_home, "engine"), "http://127.0.0.1:9") { Home = _home };

    /// <summary>
    /// The run handed its start's own facts and the sink a driven executor composes (row l): the record and its console, the conversation
    /// its harness names kept under <paramref name="keepAs"/>, and the part's lineage under <paramref name="lineageHome"/>.
    /// </summary>
    private void Hand(
        TransportRun run, ISessionOutput? output = null, string? keepAs = null, HarnessWords? own = null, IReadOnlyList<SessionEvent>? opening = null,
        string? lineageHome = null)
    {
        run.HandStart(run.Start with { Session = "s1", Transcript = Transcript, Own = own, Opening = opening });
        run.HandSink(ReadingSinks.Of(
            new RecordReadings("s1", output, _events),
            keepAs is null ? null
                : ReadingSinks.On<WireReading.ConversationNamed>(named => _driver.Context.Conversations.Keep("s1", keepAs, named.Conversation)),
            lineageHome is null ? null : new LineageReadings(lineageHome, "s1")));
    }

    /// <summary>
    /// The scripted native harness: the Claude Code mapper over its stream, a resume that says its arguments and what its input carried, flags
    /// for its carriers.
    /// </summary>
    private static Provider Scripted(string home, bool structured) =>
        TestDoors.Door("scripted", resumes: true, spawn: new ScriptedNative(home), dialect: structured ? TestDoors.StreamJson : null);

    private sealed class ScriptedNative(string home) : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => Recipe("claude");

        public ProcessRecipe Chat(SpawnRequest request) => Recipe("claude");

        public ProcessRecipe? Resume(SpawnRequest request) => Recipe("resume", "--resume", request.Resume!) with { Input = true };

        public void HandSettings(ProcessStartInfo info, string settingsFile) => info.ArgumentList.Add("--settings:" + settingsFile);

        public void HandServers(ProcessStartInfo info, string configFile) => info.ArgumentList.Add("--mcp-config:" + configFile);

        private ProcessRecipe Recipe(params string[] arguments) => new("node", [Path.Combine(home, "agent.cjs"), .. arguments]);
    }

    private TransportRun Over(AgentDoor adapter, ProcessStartInfo info, Func<TransportStart, TransportStart>? shape = null) =>
        Over(ProviderRegistry.Built(), adapter, info, shape);

    private TransportRun Over(Provider adapter, ProcessStartInfo info, Func<TransportStart, TransportStart>? shape = null)
    {
        var registry = TestDoors.Registry(adapter);
        return Over(registry, registry.Door(adapter.Doors[0].Id.Value), info, shape);
    }

    private ProcessStartInfo Prepare(Provider adapter, IReadOnlyList<string>? command) =>
        TestDoors.Registry(adapter).Start(adapter.Doors[0], Target(), command);

    private TransportRun Over(ProviderRegistry registry, AgentDoor adapter, ProcessStartInfo info, Func<TransportStart, TransportStart>? shape)
    {
        var start = new TransportStart(info, StartClass.Person)
        {
            Target = Target(), Request = SpawnRequests.Of(Target(), null), Selection = new HarnessSelection(null),
        };
        return new TransportRun(new DriverTransportHost(_driver), registry, adapter, shape is null ? start : shape(start));
    }

    private IReadOnlyList<SessionEvent> Heard()
    {
        lock (_heard) return [.. _heard];
    }

    /// <summary>
    /// A native door that carries only text: the process tracked from its start and until the run is let go, its two streams in the
    /// transcript, the exit its end names and the harness's ending read from the transcript beside it, as a start's conclusion reads them.
    /// </summary>
    [Fact]
    public async Task A_native_start_is_tracked_captured_waited_for_and_ended_as_a_start_is()
    {
        var adapter = Scripted(_home, structured: false);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("plain"));
        Hand(run);

        await run.OpenAsync(Bound);

        Assert.True(run.Started);
        Assert.True(_processes.RunsHere("s1"));
        var end = await run.EndAsync(Bound);

        Assert.Equal(3, end.Exit);
        Assert.False(end.Named);
        Assert.Null(end.Usage);
        Assert.Equal(3, end.Ending.Exit);
        Assert.True(end.Ending.Failed);
        Assert.Contains("hello out", end.Ending.Lines);
        Assert.Contains("hello err", end.Ending.Lines);
        Assert.Contains("hello out", File.ReadAllText(Transcript));
        Assert.True(run.Process!.HasExited);

        await run.DisposeAsync();
        Assert.False(_processes.RunsHere("s1"));
    }

    /// <summary>
    /// A native door that speaks a structured stdout: the conversation its init line names kept under the adapter and told to the end, the
    /// harness's own words (its stderr) the ending's lines, the opening said once ahead of the stream, and what the agent said in the transcript.
    /// </summary>
    [Fact]
    public async Task A_structured_native_start_keeps_the_conversation_it_names_and_says_its_opening_once()
    {
        var adapter = Scripted(_home, structured: true);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("claude"));
        Hand(run, keepAs: "scripted", own: new HarnessWords(), opening: [new SessionEvent { Kind = SessionEventKind.Note, Text = "started on `scripted`" }]);

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.True(end.Named);
        Assert.False(end.Ending.Failed);
        Assert.Contains("the harness says this", end.Ending.Lines);
        Assert.Equal("conv-1", _driver.Context.Conversations.Read("s1")?.Conversation);
        var transcript = File.ReadAllText(Transcript);
        Assert.StartsWith("started on `scripted`", transcript, StringComparison.Ordinal);
        Assert.Contains("Logged the port.", transcript);
        Assert.Single(Heard(), e => e.Text == "started on `scripted`");
    }

    /// <summary>
    /// The go-on: the next process resumes the kept conversation with the held words as its one prompt on its input,
    /// under the settings and servers the start is handed, its output appended to the same transcript with its own opening;
    /// its executor told once that process is up and before anything reads it, where the words it carries are sent; a conversation
    /// it names kept, never told to the resume the run began with, whose own words it does not carry; both processes tracked until the run is
    /// let go.
    /// </summary>
    [Fact]
    public async Task A_go_on_resumes_the_kept_conversation_under_the_carriers_and_appends_to_the_transcript()
    {
        var adapter = Scripted(_home, structured: true);
        var named = 0;
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("claude"), start => start with { Named = () => named++ });
        Hand(run, keepAs: "scripted", own: new HarnessWords());
        run.HandCarriers(new RunCarriers("settings.json", "servers.json"));
        await run.OpenAsync(Bound);
        await run.EndAsync(Bound);
        var first = run.Process;
        Assert.Equal(1, named);

        var told = new List<Process?>();
        run.Input.Hold(new DoorWord(
            [], "Also log the port.", [new SessionEvent { Kind = SessionEventKind.Note, Text = "resumed on `scripted`" }],
            Written: () => told.Add(run.Process)));
        await run.ResumeAsync("conv-1", Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.True(end.Named);
        Assert.Equal(1, named);
        var up = Assert.Single(told);
        Assert.NotSame(first, up);
        Assert.Same(run.Process, up);
        var transcript = File.ReadAllText(Transcript);
        Assert.Contains("Logged the port.", transcript);
        Assert.Contains("resumed on `scripted`", transcript);
        Assert.Contains("args: --resume conv-1 --settings:settings.json --mcp-config:servers.json", transcript);
        Assert.Contains("heard: Also log the port.", transcript);
        Assert.True(transcript.IndexOf("Logged the port.", StringComparison.Ordinal) < transcript.IndexOf("resumed on `scripted`", StringComparison.Ordinal));
        Assert.True(_processes.RunsHere("s1"));

        await run.DisposeAsync();
        Assert.False(_processes.RunsHere("s1"));
    }

    /// <summary>
    /// The native resume's one retry: the same start once more after a failed exit before the conversation is named, its
    /// transcript appended, its opening not said again, said as the retry it is, and the resume told it is tried again.
    /// </summary>
    [Fact]
    public async Task A_resume_that_failed_before_naming_its_conversation_is_tried_once_more_by_the_run_with_its_opening_said_once()
    {
        var resume = new ResumeAsk(
            "conv-1", [new SaidWordView("w1", "Also log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true)],
            Continuations.Opening("scripted", null, null, answer: false, findings: false, persons: true));
        var opening = resume.Opening();
        var adapter = Scripted(_home, structured: true);
        var output = new SessionOutput();
        var onConsole = new List<string>();
        output.Lined += (_, line) =>
        {
            lock (onConsole) onConsole.Add(line.Text);
        };
        File.WriteAllText(Transcript, "the record's earlier lines\n");
        await using var run = Over(
            adapter, Prepare(adapter, null).WithArguments("again", Path.Combine(_home, "marker")));
        Hand(run, output, keepAs: "scripted", own: new HarnessWords(), opening: opening);

        // The first process exits 1 before its `init`; the run tries the same resume once more on its own, and its end is the retry's.
        // A word said before either process is up waits on the run's input, is handed to the first process and, as that one ended before it
        // named its conversation, to the retry's; once the conversation is named it is acknowledged and never written again.
        run.Input.Hold(new DoorWord("w1", "Also log the port."));
        await run.ResumeAsync("conv-1", Bound, resume);
        var again = await run.EndAsync(Bound);

        Assert.Empty(run.Input.Unacknowledged);
        Assert.Equal(0, again.Exit);
        Assert.True(again.Named);
        Assert.True(resume.Retried);
        Assert.True(resume.Named);
        Assert.True(resume.Prompted);
        var transcript = File.ReadAllText(Transcript);
        Assert.StartsWith("the record's earlier lines", transcript, StringComparison.Ordinal);
        Assert.Contains("not a frame", transcript);
        Assert.Contains("second run", transcript);
        // The retry's line is the console's: the transcript is the capture's, which says only the stream.
        lock (onConsole) Assert.Contains(onConsole, line => line.Contains("the same resume is tried once more", StringComparison.Ordinal));
        var said = opening.First(e => e.Kind == SessionEventKind.Note).Text!;
        Assert.Equal(1, transcript.Split(said).Length - 1);
        Assert.Equal(1, Heard().Count(e => e.Text == said));
        // Each process the run started its own lineage token, the retry's beside the first's.
        var started = Lineage().Where(line => line.GetProperty("by").GetString() == "start").Select(line => line.GetProperty("proc").GetString()).ToList();
        Assert.Equal(2, started.Distinct().Count());
    }

    private List<System.Text.Json.JsonElement> Lineage() =>
        [.. File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)];

    /// <summary>
    /// The lineage bracket: the run's process is issued its own token, its <c>start</c> written before the spawn, the
    /// token on the spawn's environment, its <c>end</c> written clean once the exit is read and the capture drained, and the token let go at the
    /// exit.
    /// </summary>
    [Fact]
    public async Task A_runs_process_is_bracketed_by_its_own_token_from_its_start_to_its_end()
    {
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        var adapter = Scripted(_home, structured: false);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("token"));
        Hand(run, lineageHome: _home);

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        var proc = run.Proc!;
        Assert.Equal(0, end.Exit);
        Assert.Matches("^[0-9a-f]{16}$", proc);
        Assert.Contains($"token {proc}", File.ReadAllText(Transcript));
        var lines = Lineage();
        Assert.Equal([("start", proc), ("end", proc)], lines.Select(line => (line.GetProperty("by").GetString(), line.GetProperty("proc").GetString())));
        Assert.True(lines[^1].GetProperty("clean").GetBoolean());
        Assert.Equal(TokenReading.Gone, tokens.Reading(proc));
    }

    /// <summary>A run its move issued a token for (a resume moved first) starts its first process under that token, its start not written again.</summary>
    [Fact]
    public async Task A_run_moved_first_starts_its_first_process_under_the_token_its_move_issued()
    {
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        var moved = ChildLineage.Opened(_home, "s1");
        var adapter = Scripted(_home, structured: false);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("token"), start => start with { Moved = moved });
        Hand(run, lineageHome: _home);

        await run.OpenAsync(Bound);
        await run.EndAsync(Bound);

        Assert.Equal(moved, run.Proc);
        Assert.Contains($"token {moved}", File.ReadAllText(Transcript));
        Assert.Single(Lineage(), line => line.GetProperty("by").GetString() == "start");
    }

    /// <summary>
    /// The native prompt: written on the process's input once its output is being read, the input closed, and
    /// the write's return told.
    /// </summary>
    [Fact]
    public async Task A_native_runs_prompt_goes_on_its_input_and_its_write_is_told()
    {
        var adapter = Scripted(_home, structured: true);
        var info = Prepare(adapter, null).WithArguments("stdin");
        info.RedirectStandardInput = true;
        var written = 0;
        await using var run = Over(adapter, info, start => start with { Prompt = "Log the port.", Written = () => written++ });
        Hand(run, own: new HarnessWords());

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Equal(1, written);
        Assert.Contains("heard: Log the port.", File.ReadAllText(Transcript));
    }

    /// <summary>
    /// An executor whose own steps come between its spawn and its capture (its actor kept, its watch and inbox opened) spawns
    /// first, the process up and tracked with nothing reading it, then hands the run its completed start and its records and opens over the
    /// same process.
    /// </summary>
    [Fact]
    public async Task An_executor_spawns_first_then_hands_its_start_and_records_and_opens_over_the_same_process()
    {
        var adapter = Scripted(_home, structured: true);
        var info = Prepare(adapter, null).WithArguments("stdin");
        info.RedirectStandardInput = true;
        var start = new TransportStart(info, StartClass.Person);
        await using var run = new TransportRun(new DriverTransportHost(_driver), TestDoors.Registry(adapter), adapter.Doors[0], start);

        await run.SpawnAsync("s1", Bound);

        Assert.True(run.Started);
        Assert.True(_processes.RunsHere("s1"));
        Assert.False(File.Exists(Transcript));
        var process = run.Process;
        run.HandStart(start with { Prompt = "Log the port." });
        Hand(run, own: new HarnessWords());
        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Same(process, run.Process);
        Assert.Contains("heard: Log the port.", File.ReadAllText(Transcript));
    }

    /// <summary>
    /// A run let go before its end: its process ended, its <c>end</c> written unclean once the exit is read and its token
    /// let go, and its capture observed.
    /// </summary>
    [Fact]
    public async Task A_run_let_go_before_its_end_ends_its_process_and_writes_its_end_unclean()
    {
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        var adapter = Scripted(_home, structured: false);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("hangs"));
        Hand(run);
        await run.OpenAsync(Bound);
        var proc = run.Proc!;

        await run.AbandonAsync();

        Assert.True(run.Process!.HasExited);
        var end = Lineage()[^1];
        Assert.Equal(("end", proc, false), (end.GetProperty("by").GetString(), end.GetProperty("proc").GetString(), end.GetProperty("clean").GetBoolean()));
        Assert.Equal(TokenReading.Gone, tokens.Reading(proc));
    }

    /// <summary>A run let go while its retry hangs lets go the retry's process and capture, its own lineage token ended unclean: the leg the run holds is the retry's.</summary>
    [Fact]
    public async Task A_run_let_go_while_its_retry_hangs_ends_the_retry_s_process_and_not_the_first_s()
    {
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        var adapter = Scripted(_home, structured: true);
        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("againhangs", Path.Combine(_home, "marker")));
        Hand(run, new SessionOutput(), keepAs: "scripted", own: new HarnessWords());

        await run.ResumeAsync("conv-1", Bound, new ResumeAsk(
            "conv-1", [], Continuations.Opening("scripted", null, null, answer: false, findings: false, persons: true)));
        var ending = run.EndAsync(Bound);
        await Poll.Until(() => File.Exists(ChildLineage.PathOf(_home, "s1"))
            && Lineage().Where(line => line.GetProperty("by").GetString() == "start").Select(line => line.GetProperty("proc").GetString()).ToList() is [_, var second]
            && run.Proc == second && !run.Input.MayRetry);

        var retried = run.Process!;
        var proc = run.Proc!;
        await run.AbandonAsync();
        await ending;

        Assert.True(retried.HasExited);
        var end = Lineage().Last(line => line.GetProperty("by").GetString() == "end");
        Assert.Equal(("end", proc, false), (end.GetProperty("by").GetString(), end.GetProperty("proc").GetString(), end.GetProperty("clean").GetBoolean()));
        Assert.Equal(TokenReading.Gone, tokens.Reading(proc));
    }

    private (TransportRun Run, string Seen) OverProtocol(string scenario, TransportProtocol protocol)
    {
        var seen = Path.Combine(_home, $"seen-{scenario}.txt");
        var command = ProviderFiles.Of("stub").Command("acp-stub", "--scenario", scenario, "--seen", seen);
        var adapter = ProviderRegistry.Built().Door("acp-stub");
        var run = Over(adapter, ProviderRegistry.Built().Start(adapter, Target(), command), start => start with { Protocol = protocol });
        Hand(run, keepAs: "acp-stub");
        return (run, seen);
    }

    private static IReadOnlyList<string> Methods(string seen) => File.Exists(seen) ? File.ReadAllLines(seen) : [];

    /// <summary>
    /// A protocol door's driven run: the session over the process's stdio opened, prompted and closed as <c>AcpSession.RunAsync</c> does, the
    /// process tracked, the stream's lines in the transcript, the end's exit read once its stdin closed, and the session reachable through the
    /// run's own accessor.
    /// </summary>
    [Fact]
    public async Task A_protocol_door_runs_its_session_over_the_process_and_ends_as_a_start_does()
    {
        var (run, seen) = OverProtocol("open", TransportProtocol.Driven(Path.Combine(_home, "engine"), "Go on."));
        await using var _ = run;

        await run.OpenAsync(Bound);

        Assert.NotNull(run.Session);
        Assert.True(_processes.RunsHere("s1"));
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Null(end.Ending.Failure);
        Assert.False(end.Ending.Failed);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], Methods(seen));
        // The transcript is the run's until it is let go, as a session's is open until its capture ends.
        await run.DisposeAsync();
        Assert.Contains("heard: Go on.", File.ReadAllText(Transcript));
    }

    /// <summary>
    /// The driven session's wider setup as the executor's closure: what it sets on the session the run made, its lines said
    /// through the run beside the session's, and its word at the session's end said after the turn's end and before the input closes.
    /// </summary>
    [Fact]
    public async Task A_protocol_runs_executor_sets_its_session_runs_it_and_says_its_last_word_after_the_turns_end()
    {
        TransportRun? held = null;
        var cwd = Path.Combine(_home, "engine");
        var protocol = new TransportProtocol(cwd)
        {
            Configure = session => session.BackgroundLook = TimeSpan.FromSeconds(7),
            Run = async (session, conversation, ct) =>
            {
                held!.Say($"— it looks again every {session.BackgroundLook.TotalSeconds:0} s");
                return await session.RunAsync(cwd, "Go on.", ct, resume: conversation);
            },
            Ended = () => held!.Say("— the executor's last word"),
        };
        var (run, _) = OverProtocol("open", protocol);
        held = run;
        await using var _ = run;

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);
        await run.DisposeAsync();

        Assert.Equal(0, end.Exit);
        var transcript = File.ReadAllText(Transcript);
        Assert.Contains("— it looks again every 7 s", transcript);
        var turnEnd = transcript.IndexOf("session record is concluded from the exit code", StringComparison.Ordinal);
        Assert.True(turnEnd >= 0, transcript);
        Assert.True(turnEnd < transcript.IndexOf("— the executor's last word", StringComparison.Ordinal), transcript);
    }

    /// <summary>The refusal a protocol turn ended on is the ending's, for a conclusion that reads its code.</summary>
    [Fact]
    public async Task A_protocol_refusal_is_the_endings_refusal()
    {
        var (run, _) = OverProtocol("limit", TransportProtocol.Driven(Path.Combine(_home, "engine"), "Go on."));
        await using var _ = run;

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.NotNull(end.Ending.Refusal);
        Assert.False(string.IsNullOrEmpty(end.Ending.Failure));
    }

    /// <summary>A protocol failure at the prompt is the ending's failure, as the conclusion reads it beside the exit.</summary>
    [Fact]
    public async Task A_protocol_failure_is_the_endings_failure()
    {
        var (run, _) = OverProtocol("fatal", TransportProtocol.Driven(Path.Combine(_home, "engine"), "Go on."));
        await using var _ = run;

        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.False(string.IsNullOrEmpty(end.Ending.Failure));
        Assert.True(end.Ending.Failed);
    }

    /// <summary>A resume the agent would not take is no turn that failed: nothing is prompted, and the run says why it is refused.</summary>
    [Fact]
    public async Task A_protocol_resume_the_agent_refuses_prompts_nothing_and_says_why()
    {
        var (run, seen) = OverProtocol("missing", TransportProtocol.Driven(Path.Combine(_home, "engine"), "Go on."));
        await using var _ = run;

        await run.ResumeAsync("s-gone", Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(ContinueWhy.Gone, run.ResumeRefused!.Why.Code);
        Assert.Null(end.Ending.Failure);
        Assert.DoesNotContain("session/prompt", Methods(seen));
    }

    /// <summary>
    /// The conversation's shape: a protocol door's session opened and held, its open on its way as the run's open returns and
    /// told once made, words and turns beside it on <see cref="TransportRun.Session"/>, closed by the run, the process gone with the close; its
    /// transcript closed once both its streams ended, before the run is let go, so a record concluded over it is read, and a line said later
    /// dropped.
    /// </summary>
    [Fact]
    public async Task A_protocol_session_held_open_is_told_opened_closed_by_the_run_and_its_transcript_closed_at_its_end()
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TransportRun? held = null;
        var (run, seen) = OverProtocol("open", new TransportProtocol(Path.Combine(_home, "engine"))
        {
            Opened = () =>
            {
                held!.Say("— the conversation opened");
                opened.TrySetResult();
            },
        });
        held = run;
        await using var _ = run;

        await run.OpenAsync(Bound);
        await opened.Task.WaitAsync(Bound);

        Assert.NotNull(run.Session);
        Assert.Equal(["initialize", "session/new"], Methods(seen));

        await run.CloseAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Equal(["initialize", "session/new", "session/close"], Methods(seen));
        Assert.Contains("— the conversation opened", File.ReadAllText(Transcript));
        run.Say("— a late line");
        Assert.DoesNotContain("— a late line", File.ReadAllText(Transcript));
    }

    /// <summary>
    /// A held open the agent refuses is told to its executor before the run ends its process, so what it says is the transcript's;
    /// the session that never opened is no session, and the run's end says it could not open, as the conversation's record concludes it.
    /// </summary>
    [Fact]
    public async Task A_held_open_the_agent_refuses_is_told_before_its_process_ends_and_its_end_says_it_could_not_open()
    {
        Exception? told = null;
        TransportRun? held = null;
        var (run, seen) = OverProtocol("missing", new TransportProtocol(Path.Combine(_home, "engine"))
        {
            Unopened = error =>
            {
                told = error;
                held!.Say("— the executor says why");
            },
        });
        held = run;
        await using var _ = run;

        await run.ResumeAsync("s-gone", Bound);
        var error = await Assert.ThrowsAsync<DriverException>(() => run.EndAsync(Bound));

        Assert.IsType<AcpResumeRefused>(told);
        Assert.StartsWith("the ACP session could not open: ", error.Message, StringComparison.Ordinal);
        Assert.Null(run.Session);
        Assert.True(run.Process!.HasExited);
        Assert.Contains("— the executor says why", File.ReadAllText(Transcript));
        Assert.DoesNotContain("session/prompt", Methods(seen));
    }

    /// <summary>
    /// The native conversation: each event its capture keeps told once the record holds it, which is how its turn's end lets its
    /// next words go; the conversation its stream names kept and told the moment it is named, while the harness still runs; and
    /// its close the end of its input, after which the harness winds up and exits on its own.
    /// </summary>
    [Fact]
    public async Task A_native_conversation_is_told_each_event_keeps_what_its_stream_names_at_once_and_closes_on_its_input()
    {
        var adapter = Scripted(_home, structured: true);
        var info = Prepare(adapter, null).WithArguments("talks");
        info.RedirectStandardInput = true;
        var turns = 0;
        var named = 0;
        await using var run = Over(adapter, info, start => start with
        {
            Observed = e =>
            {
                if (e.Kind == SessionEventKind.Turn) Interlocked.Increment(ref turns);
            },
            Named = () => Interlocked.Increment(ref named),
        });
        Hand(run, keepAs: "scripted");

        await run.OpenAsync(Bound);
        await Poll.Until(() => Volatile.Read(ref turns) == 1, () => "no turn's end was told", Poll.Patience);

        Assert.Equal("conv-1", _driver.Context.Conversations.Read("s1")?.Conversation);
        Assert.Equal(1, Volatile.Read(ref named));
        Assert.False(run.Process!.HasExited);

        await run.CloseAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.True(end.Named);
    }

    /// <summary>
    /// An executor that composes what names its process before the spawn (the conversation's connector) has the run
    /// bracket it first: the downloads kept and the token issued, the spawn under that token, and a run let go before its spawn lets it go.
    /// </summary>
    [Fact]
    public async Task A_run_bracketed_before_its_spawn_starts_under_that_token_and_one_let_go_unspawned_lets_it_go()
    {
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        var adapter = Scripted(_home, structured: false);
        var never = Over(adapter, Prepare(adapter, null).WithArguments("token"));
        var unspawned = await never.BracketAsync("s1", Bound);

        Assert.Equal(TokenReading.Unwatched, tokens.Reading(unspawned));
        await never.DisposeAsync();
        Assert.False(never.Started);
        Assert.Equal(TokenReading.Gone, tokens.Reading(unspawned));

        await using var run = Over(adapter, Prepare(adapter, null).WithArguments("token"));
        Hand(run, lineageHome: _home);
        var proc = await run.BracketAsync("s1", Bound);
        await run.SpawnAsync("s1", Bound);
        await run.OpenAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Equal(proc, run.Proc);
        Assert.Contains($"token {proc}", File.ReadAllText(Transcript));
        Assert.Equal(1, Lineage().Count(line => line.GetProperty("by").GetString() == "start" && line.GetProperty("proc").GetString() == proc));
    }
}

internal static class StartInfoExtensions
{
    /// <summary>The same start with its program's arguments after the script's path replaced: the first argument names the script's mode.</summary>
    public static ProcessStartInfo WithArguments(this ProcessStartInfo info, params string[] mode)
    {
        var script = info.ArgumentList[0];
        info.ArgumentList.Clear();
        info.ArgumentList.Add(script);
        foreach (var argument in mode) info.ArgumentList.Add(argument);
        return info;
    }
}
