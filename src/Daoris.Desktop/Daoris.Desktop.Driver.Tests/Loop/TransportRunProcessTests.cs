using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1m-i part 1: <see cref="TransportRun"/> over both of today's doors, run as real model-free processes, observed as today's
/// start observes them (<c>Driver.HoldAsync</c>'s unit and <c>DriverContinuations.GoOnAsync</c>, <c>AgainAsync</c>): the process started
/// through the agents' route and tracked, a reaper beside it, the capture reading its streams into the transcript and the record, the wait
/// on its exit, the capture bounded past it, and the end read the way the conclusion reads it. A native door's one program is a Node script
/// that speaks the stream the Claude Code mapper reads; a protocol door's is the stand-in's own. No executor holds the run yet.
/// </summary>
[Trait(Category.Name, Category.Process)]
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
          case 'resume': init('conv-1'); said('args: ' + rest.join(' ')); result(); break;
        }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-transport-run-" + Guid.NewGuid().ToString("N")[..8]);

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
        _driver = new Daoris.Driver.Driver(_service, DriverConfig.Empty, AdapterSet.Built(), _home, _processes);
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

    /// <summary>The scripted native harness: the Claude Code mapper over its stream, a resume that says its arguments, flags for its carriers.</summary>
    private sealed class Scripted(string home, bool structured) : ISessionAdapter
    {
        public string Name => "scripted";

        public bool Resumes => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => Start(target, "claude");

        public ProcessStartInfo? PrepareResume(SessionTarget target, IReadOnlyList<string>? command, string conversation, string prompt) =>
            Start(target, "resume", "--resume", conversation, "-p", prompt);

        public void HandSettings(ProcessStartInfo info, string settingsFile) => info.ArgumentList.Add("--settings:" + settingsFile);

        public void HandServers(ProcessStartInfo info, string configFile) => info.ArgumentList.Add("--mcp-config:" + configFile);

        public IStreamMapper? StructuredOutput() => structured ? new ClaudeStreamJson() : null;

        private ProcessStartInfo Start(SessionTarget target, params string[] arguments) =>
            Spawning.InRoot(target, "node", [Path.Combine(home, "agent.cjs"), .. arguments]);
    }

    private TransportRun Over(ISessionAdapter adapter, ProcessStartInfo info, Func<TransportStart, TransportStart>? shape = null)
    {
        var start = new TransportStart(info, StartClass.Person) { Target = Target(), Selection = new HarnessSelection(null) };
        return new TransportRun(new DriverTransportHost(_driver), adapter, shape is null ? start : shape(start));
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
        var adapter = new Scripted(_home, structured: false);
        await using var run = Over(adapter, adapter.Prepare(Target(), null).WithArguments("plain"));
        run.HandRecords(new RunRecords("s1", Transcript, Events: _events));

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
        var adapter = new Scripted(_home, structured: true);
        await using var run = Over(adapter, adapter.Prepare(Target(), null).WithArguments("claude"));
        run.HandRecords(new RunRecords(
            "s1", Transcript, Events: _events, KeepAs: "scripted", Own: new HarnessWords(),
            Opening: [new SessionEvent { Kind = SessionEventKind.Note, Text = "started on `scripted`" }]));

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
    /// The go-on (MSG1b, D137 §2.1): the next process resumes the kept conversation with the held words as its one prompt, under the settings
    /// and servers the start was handed, its output appended to the same transcript with its own opening; both runs tracked until the run is let go.
    /// </summary>
    [Fact]
    public async Task A_go_on_resumes_the_kept_conversation_under_the_carriers_and_appends_to_the_transcript()
    {
        var adapter = new Scripted(_home, structured: true);
        await using var run = Over(adapter, adapter.Prepare(Target(), null).WithArguments("claude"));
        run.HandRecords(new RunRecords("s1", Transcript, Events: _events, KeepAs: "scripted", Own: new HarnessWords()));
        run.HandCarriers(new RunCarriers("settings.json", "servers.json"));
        await run.OpenAsync(Bound);
        await run.EndAsync(Bound);

        var end = await run.GoOnAsync(
            "conv-1", "Also log the port.", [new SessionEvent { Kind = SessionEventKind.Note, Text = "resumed on `scripted`" }], Bound);

        Assert.Equal(0, end.Exit);
        Assert.True(end.Named);
        var transcript = File.ReadAllText(Transcript);
        Assert.Contains("Logged the port.", transcript);
        Assert.Contains("resumed on `scripted`", transcript);
        Assert.Contains("args: --resume conv-1 -p Also log the port. --settings:settings.json --mcp-config:servers.json", transcript);
        Assert.True(transcript.IndexOf("Logged the port.", StringComparison.Ordinal) < transcript.IndexOf("resumed on `scripted`", StringComparison.Ordinal));
        Assert.True(_processes.RunsHere("s1"));

        await run.DisposeAsync();
        Assert.False(_processes.RunsHere("s1"));
    }

    /// <summary>
    /// The native resume's one retry (SESSCTX1c): the same start once more after a failed exit before the conversation was named, its
    /// transcript appended, its opening not said again, said as the retry it is, and the resume told it was tried again.
    /// </summary>
    [Fact]
    public async Task A_restart_is_the_same_start_once_more_appended_with_its_opening_said_once()
    {
        var resume = new ResumeAsk(
            "conv-1", [new SaidWordView("w1", "Also log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true)],
            Continuations.Opening("scripted", null, null, answer: false, findings: false, persons: true));
        var opening = resume.Opening();
        var adapter = new Scripted(_home, structured: true);
        var output = new SessionOutput();
        var onConsole = new List<string>();
        output.Lined += (_, line) =>
        {
            lock (onConsole) onConsole.Add(line.Text);
        };
        File.WriteAllText(Transcript, "the record's earlier lines\n");
        await using var run = Over(
            adapter, adapter.Prepare(Target(), null).WithArguments("again", Path.Combine(_home, "marker")),
            start => start with { Named = () => resume.Named = resume.Prompted = true, Again = resume.Again });
        run.HandRecords(new RunRecords("s1", Transcript, Output: output, Events: _events, KeepAs: "scripted", Own: new HarnessWords(), Opening: opening));

        await run.ResumeAsync("conv-1", Bound);
        var first = await run.EndAsync(Bound);

        Assert.Equal(1, first.Exit);
        Assert.False(first.Named);
        Assert.False(resume.Retried);

        var again = await run.RestartAsync(Bound);

        Assert.Equal(0, again.Exit);
        Assert.True(again.Named);
        Assert.True(resume.Retried);
        Assert.True(resume.Named);
        var transcript = File.ReadAllText(Transcript);
        Assert.StartsWith("the record's earlier lines", transcript, StringComparison.Ordinal);
        Assert.Contains("not a frame", transcript);
        Assert.Contains("second run", transcript);
        // The retry's line is the console's, as AgainAsync says it: the transcript is the capture's, which says only the stream.
        lock (onConsole) Assert.Contains(onConsole, line => line.Contains("the same resume is tried once more", StringComparison.Ordinal));
        var said = opening.First(e => e.Kind == SessionEventKind.Note).Text!;
        Assert.Equal(1, transcript.Split(said).Length - 1);
        Assert.Equal(1, Heard().Count(e => e.Text == said));
    }

    private (TransportRun Run, string Seen) OverProtocol(string scenario, TransportProtocol protocol)
    {
        var seen = Path.Combine(_home, $"seen-{scenario}.txt");
        var command = ProviderFiles.Of("stub").Command("acp-stub", "--scenario", scenario, "--seen", seen);
        var adapter = AdapterSet.Built().Resolve("acp-stub");
        var run = Over(adapter, adapter.Prepare(Target(), command), start => start with { Protocol = protocol });
        run.HandRecords(new RunRecords("s1", Transcript, Events: _events, KeepAs: "acp-stub"));
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

    /// <summary>A protocol failure at the prompt is the ending's failure, as the conclusion reads it beside the exit (ACPEND1).</summary>
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

    /// <summary>A resume the agent would not take is no turn that failed: nothing was prompted, and the run says why it was refused.</summary>
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
    /// The conversation's shape: a protocol door's session opened and held, words and turns beside it on <see cref="TransportRun.Session"/>,
    /// closed by the run, the process gone with the close.
    /// </summary>
    [Fact]
    public async Task A_protocol_session_can_be_held_open_and_closed_by_the_run()
    {
        var (run, seen) = OverProtocol("open", new TransportProtocol(Path.Combine(_home, "engine")));
        await using var _ = run;

        await run.OpenAsync(Bound);

        Assert.NotNull(run.Session);
        Assert.Equal(["initialize", "session/new"], Methods(seen));

        await run.CloseAsync(Bound);
        var end = await run.EndAsync(Bound);

        Assert.Equal(0, end.Exit);
        Assert.Equal(["initialize", "session/new", "session/close"], Methods(seen));
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
