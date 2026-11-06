using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1m-i: <see cref="TransportRun"/>, Daoris's <see cref="AgentRun"/> over today's two transports, admits an operation by the
/// run's own evidence (its door's wire and its resume) and refuses one it does not, before anything is written (design §2.4, §4.1); its
/// spawn asks its host in order. No process: every refusal here comes before a start, so the run holds nothing. The unit of a start itself,
/// over both doors, is <see cref="TransportRunProcessTests"/>'s.
/// </summary>
public sealed class TransportRunTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-transport-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ServiceClient _service = new("http://127.0.0.1:9", null);

    private readonly SessionProcesses _processes;

    public TransportRunTests()
    {
        Directory.CreateDirectory(_home);
        _processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose()
    {
        _service.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A run over a door's adapter that was never started: a program that does not exist, which nothing here reaches.</summary>
    private TransportRun Over(string door)
    {
        var adapters = ProviderRegistry.Built();
        var driver = new Daoris.Driver.Driver(_service, DriverConfig.Empty, adapters, _home, _processes);
        return new TransportRun(new DriverTransportHost(driver), adapters, adapters.Door(door), new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person));
    }

    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Fix the flaky gate",
        Body: "It fails one run in five; the log is attached.",
        Asker: "Platform",
        Repository: "Game",
        Root: "D:/trees/s-1",
        ServiceUrl: "http://localhost:5177");

    /// <summary>A run that goes on as its executor handed it: the target and the selection its resume is prepared from, or neither.</summary>
    private TransportRun GoingOn(ITransportHost host, AgentDoor adapter, bool handed = true) =>
        new(host, ProviderRegistry.Built(), adapter, new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person)
        {
            Target = handed ? Target() : null,
            Request = handed ? SpawnRequests.Of(Target(), null) : null,
            Selection = handed ? new HarnessSelection(null) : null,
        });

    private static async Task Refused(Task operation, string ability)
    {
        var error = await Assert.ThrowsAsync<DriverException>(() => operation);

        Assert.Equal($"this door does not support {ability} — nothing was written.", error.Message);
    }

    /// <summary>
    /// A run's words are two named ports, one per transport (D246): a native door's lines go through its <c>Input</c> and it has no
    /// <c>Words</c>; a protocol door's input refuses a line before anything is written, its words being requests on its session.
    /// </summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public async Task Each_door_carries_its_words_on_its_own_port(string door)
    {
        AgentRun run = Over(door);

        Assert.Null(run.Words);
        if (door == "acp-stub")
        {
            await Refused(run.Input.SayAsync(new DoorWord("w1", "go on"), CancellationToken.None), TransportRun.LinesAbility);
            await Refused(run.Input.ControlAsync("interrupt", CancellationToken.None), TransportRun.LinesAbility);
        }
        else
        {
            Assert.Null(run.Input.Refuses);
        }

        Assert.False(((TransportRun)run).Started);
    }

    /// <summary>A native run is one process to its end: no turn to stop with the session kept, no task to stop, no choice to carry on a session.</summary>
    [Fact]
    public async Task A_native_run_has_no_turn_or_task_to_stop_and_no_choice_to_carry()
    {
        var run = Over("stub");

        await Refused(run.StopTurnAsync(CancellationToken.None), "a stop that keeps the conversation");
        await Refused(run.StopTaskAsync("t-1", CancellationToken.None), "one task stopped");
        var choice = new ActorAsked("stub", new ActorValue("entry", "m-2", true), new ActorValue("entry", null, false));
        await Refused(run.ApplyChoiceAsync(choice, CancellationToken.None), TransportRun.ChoiceAbility);

        Assert.False(run.Started);
    }

    /// <summary>A door that resumes nothing has no conversation to go on in, so a resume and a go-on are refused for it.</summary>
    [Fact]
    public async Task A_door_that_cannot_resume_refuses_a_resume_and_a_go_on()
    {
        var run = Over("stub");

        await Refused(run.ResumeAsync("c-1", CancellationToken.None), "a resume");

        Assert.False(run.Started);
    }

    /// <summary>A protocol door resumes on its wire and goes on in its own session, so the native go-on is refused for it.</summary>
    [Fact]
    public async Task A_protocol_door_refuses_the_native_go_on()
    {
        var run = Over("acp-stub");

        Assert.False(run.GoesOn);

        Assert.False(run.Started);
    }

    /// <summary>
    /// The conversation's runner reaches its <see cref="AcpSession"/> through <see cref="TransportRun.Session"/> alone (design §4.1, round
    /// 5), which is null until the run opened a protocol door's session and always on a native one.
    /// </summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public void The_conversations_session_is_the_runs_own_accessor_and_nothing_before_the_open(string door)
    {
        Assert.Null(Over(door).Session);
        Assert.Null(Over(door).Words);
        Assert.Contains(
            typeof(TransportRun).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
            property => property.PropertyType == typeof(AcpSession));
        Assert.DoesNotContain(typeof(AgentRun).GetProperties(), property => property.PropertyType == typeof(AcpSession));
    }

    /// <summary>
    /// ONEWAY1h-i: the run's spawn keeps the downloads its start is handed (KEEP1b), brackets its process's lineage (COWORKVIEW1a) and starts
    /// it, in that order, each through the host; a start the gate refuses refuses the spawn.
    /// </summary>
    [Fact]
    public async Task The_runs_spawn_keeps_what_its_start_is_handed_then_brackets_and_starts_it_through_its_host()
    {
        var host = new RecordingTransportHost(_home, _processes);
        var run = new TransportRun(host, ProviderRegistry.Built(), ProviderRegistry.Built().Door("stub"), new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person));
        using var refusing = ProcessStarts.Within(_ => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified."));

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => run.SpawnAsync("s1", CancellationToken.None));

        Assert.Equal(["keep s1", "bracket s1", "spawn p1"], host.Asked);
        Assert.False(run.Started);
    }

    /// <summary>
    /// ONEWAY1h-iii: a run bracketed before its spawn (the conversation's, whose connector names its process) keeps its start's downloads and
    /// issues its token through its host once, and its spawn starts under that token without either again.
    /// </summary>
    [Fact]
    public async Task A_run_bracketed_first_keeps_and_brackets_once_and_its_spawn_starts_under_that_token()
    {
        var host = new RecordingTransportHost(_home, _processes);
        var run = new TransportRun(host, ProviderRegistry.Built(), ProviderRegistry.Built().Door("stub"), new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person));
        using var refusing = ProcessStarts.Within(_ => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified."));

        Assert.Equal("p1", await run.BracketAsync("s1", CancellationToken.None));
        Assert.Equal(["keep s1", "bracket s1"], host.Asked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.BracketAsync("s1", CancellationToken.None));
        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => run.SpawnAsync("s1", CancellationToken.None));

        Assert.Equal(["keep s1", "bracket s1", "spawn p1"], host.Asked);
        Assert.False(run.Started);
    }

    /// <summary>A start handed before the open, once the process is up or before its spawn, is the same process's.</summary>
    [Fact]
    public void A_start_handed_later_must_start_the_same_process()
    {
        var run = Over("stub");

        var error = Assert.Throws<InvalidOperationException>(() => run.HandStart(new TransportStart(new ProcessStartInfo("another"), StartClass.Person)));

        Assert.Contains("the same process", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ONEWAY1h-ii: the run admits the native go-on with held words (MSG1b) by its own evidence, a native door that resumes, and only where its
    /// executor handed it the target and the selection its resume is prepared from. Where it admits none, its executor holds no words.
    /// </summary>
    [Fact]
    public void A_go_on_is_admitted_on_a_native_door_that_resumes_and_was_handed_what_its_resume_is_prepared_from()
    {
        var host = new RecordingTransportHost(_home, _processes);
        var adapters = ProviderRegistry.Built();

        Assert.True(GoingOn(host, BuiltDoors.Claude).GoesOn);
        Assert.False(GoingOn(host, BuiltDoors.Claude, handed: false).GoesOn);
        Assert.False(GoingOn(host, adapters.Door("stub")).GoesOn);
        Assert.False(GoingOn(host, adapters.Door("acp-stub")).GoesOn);
    }

    /// <summary>
    /// 🔴 Words held while a native run worked go on in its own conversation before the record concludes (MSG1b, D137 §2.1): the run's go-on
    /// prepares <c>claude -p --resume &lt;kept id&gt;</c> under the rules and servers the run was handed, the words never among its arguments
    /// (AGENTLYNTAI1s-i: they go on its input), the target not sent again, and keeps no downloads. Its executor is told nothing until its
    /// process is up.
    /// </summary>
    [Fact]
    public async Task A_go_on_resumes_its_own_conversation_under_the_same_rules_with_the_words_off_its_arguments()
    {
        var host = new RecordingTransportHost(_home, _processes) { Commands = ["claude"] };
        var run = GoingOn(host, BuiltDoors.Claude);
        run.HandStart(run.Start with { Session = "s1", Transcript = Path.Combine(_home, "s1.log") });
        run.HandSink(ReadingSinks.None);
        run.HandCarriers(new RunCarriers("D:/daoris-data/sessions/s1.settings.json", "D:/daoris-data/sessions/s1.mcp.json"));
        var told = 0;
        using var refusing = ProcessStarts.Within(_ => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified."));

        // The words wait on the run's input and the next process is its resume (D243); a spawn that fails writes none of them.
        run.Input.Hold(new DoorWord([], "Also log the port.", Written: () => told++));
        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => run.NextProcessAsync("0b5e7c1a", CancellationToken.None));

        var info = Assert.Single(host.Bracketing);
        var arguments = info.ArgumentList.ToList();
        Assert.Equal("--resume", arguments[arguments.IndexOf("-p") + 1]);
        Assert.Equal("0b5e7c1a", arguments[arguments.IndexOf("--resume") + 1]);
        Assert.DoesNotContain("Also log the port.", arguments);
        Assert.True(info.RedirectStandardInput);
        Assert.Equal("D:/daoris-data/sessions/s1.settings.json", arguments[arguments.IndexOf("--settings") + 1]);
        Assert.Equal("D:/daoris-data/sessions/s1.mcp.json", arguments[arguments.IndexOf("--mcp-config") + 1]);
        Assert.DoesNotContain(arguments, argument => argument.Contains("Fix the flaky gate"));
        Assert.Equal(["prepare claude-code", "bracket s1", "spawn p1"], host.Asked);
        Assert.Equal(0, told);
        Assert.False(run.Started);
    }

    /// <summary>
    /// The contract holds the four members every executor uses (design §4.1, round 5): the sink its readings go to (row l), the carriers,
    /// the end and the go-on, the native retry being the run's own (row m-ii), and no word operation beyond the two it already had.
    /// </summary>
    [Fact]
    public void The_contract_gains_the_four_members_and_no_word_operation()
    {
        var methods = typeof(AgentRun).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.IsAbstract)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "ApplyChoiceAsync", "CloseAsync", "EndAsync", "HandCarriers", "HandSink", "OpenAsync",
                "ResumeAsync", "StopTaskAsync", "StopTurnAsync",
            ],
            methods);
        // The words are two named ports, one per transport, and no operation of their own (D246).
        var ports = typeof(AgentRun).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => (property.Name, property.PropertyType))
            .ToArray();
        Assert.Equal([("Input", typeof(DoorInput)), ("Words", typeof(ProtocolWords))], ports);
        Assert.True(typeof(TransportRun).IsSubclassOf(typeof(AgentRun)));
    }
}

/// <summary>
/// A host that records what a <see cref="TransportRun"/> asks of it, in order, and the process each bracket is written for; it prepares a
/// resume as the door made it and answers nothing that would wait on a process.
/// </summary>
internal sealed class RecordingTransportHost(string home, SessionProcesses processes) : ITransportHost
{
    public List<string> Asked { get; } = [];

    /// <summary>The process each bracket was written for, the one the spawn after it starts.</summary>
    public List<ProcessStartInfo> Bracketing { get; } = [];

    /// <summary>The command every door is configured with, or null for its own.</summary>
    public IReadOnlyList<string>? Commands { get; init; }

    public string Home => home;

    public SessionProcesses Processes => processes;

    public MachineLog? Log => null;

    public TimeSpan LingerBound => TimeSpan.Zero;

    public TextReader Hearing(TextReader reader, string sessionId) => reader;

    public Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct)
    {
        Asked.Add($"keep {sessionId}");
        return Task.CompletedTask;
    }

    public string Bracketed(string sessionId, ProcessStartInfo info, string? moved)
    {
        Asked.Add($"bracket {sessionId}");
        Bracketing.Add(info);
        return "p1";
    }

    public Task<Process?> SpawnedAsync(string proc, Func<Task<Process?>> spawn)
    {
        Asked.Add($"spawn {proc}");
        return spawn();
    }

    public Task<int?> WaitAsync(
        string sessionId, string proc, Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet,
        Action? quietEnding) => throw new NotSupportedException();

    public Task<bool> DrainedAsync(string sessionId, string proc, Task capture) => throw new NotSupportedException();

    public void Ended(string sessionId, string proc, int? exit) => throw new NotSupportedException();

    public Task AbandonedAsync(string sessionId, string proc, Process process, Task capture) => throw new NotSupportedException();

    public void KeepConversation(string sessionId, string adapter, string conversation) => throw new NotSupportedException();

    public IReadOnlyList<string>? Command(string adapter) => Commands;

    public ProcessStartInfo Prepared(AgentDoor adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next)
    {
        Asked.Add($"prepare {adapter.Id.Value}");
        return next;
    }

    public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => throw new NotSupportedException();
}
