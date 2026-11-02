using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1m-i part 1: <see cref="TransportRun"/>, Daoris's <see cref="AgentRun"/> over today's two transports, admits an operation by
/// the run's own evidence (its door's wire and its resume) and refuses one it does not, before anything is written (design §2.4, §4.1). No
/// process: every refusal here comes before a start, so the run holds nothing. The unit of a start itself, over both doors, is
/// <see cref="TransportRunProcessTests"/>'s.
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
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(_service, DriverConfig.Empty, adapters, _home, _processes);
        return new TransportRun(new DriverTransportHost(driver), adapters.Resolve(door), new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person));
    }

    private static async Task Refused(Task operation, string ability)
    {
        var error = await Assert.ThrowsAsync<DriverException>(() => operation);

        Assert.Equal($"this door does not support {ability} — nothing was written.", error.Message);
    }

    /// <summary>
    /// The words stay off the contract until m-ii (design §4.1): <c>SendAsync</c> carries no id, while today's retry marks a word attempted
    /// before the wire and takes it off by its id once written, which is <c>DoorInput</c>'s. Both doors refuse them, and no executor calls them.
    /// </summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public async Task Words_are_refused_on_both_doors_until_the_door_input_moves_them(string door)
    {
        var run = Over(door);

        await Refused(run.SendAsync("go on", CancellationToken.None), TransportRun.WordsAbility);
        await Refused(run.SteerAsync("go on", CancellationToken.None), TransportRun.WordsAbility);

        Assert.False(run.Started);
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

    /// <summary>A door that resumes nothing has no conversation to go on in, so a resume, a go-on and a restart are refused for it.</summary>
    [Fact]
    public async Task A_door_that_cannot_resume_refuses_a_resume_a_go_on_and_a_restart()
    {
        var run = Over("stub");

        await Refused(run.ResumeAsync("c-1", CancellationToken.None), "a resume");
        await Refused(run.GoOnAsync("c-1", "go on", [], CancellationToken.None), "a go-on");
        await Refused(run.RestartAsync(CancellationToken.None), "a restart");

        Assert.False(run.Started);
    }

    /// <summary>A protocol door resumes on its wire and goes on in its own session, so the native go-on and the native retry are refused for it.</summary>
    [Fact]
    public async Task A_protocol_door_refuses_the_native_go_on_and_the_native_retry()
    {
        var run = Over("acp-stub");

        await Refused(run.GoOnAsync("c-1", "go on", [], CancellationToken.None), "a go-on");
        await Refused(run.RestartAsync(CancellationToken.None), "a restart");

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
        var host = new RecordingHost(_home, _processes);
        var run = new TransportRun(host, AdapterSet.Built().Resolve("stub"), new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person));
        using var refusing = ProcessStarts.Within(_ => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified."));

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => run.SpawnAsync("s1", CancellationToken.None));

        Assert.Equal(["keep s1", "bracket s1", "spawn p1"], host.Asked);
        Assert.False(run.Started);
    }

    /// <summary>A start handed once the process is up is the same process's, and comes before the open.</summary>
    [Fact]
    public void A_start_handed_later_must_start_the_same_process()
    {
        var run = Over("stub");

        var error = Assert.Throws<InvalidOperationException>(() => run.HandStart(new TransportStart(new ProcessStartInfo("another"), StartClass.Person)));

        Assert.Contains("the same process", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A host that records what the run asks of it, in order, and answers nothing else.</summary>
    private sealed class RecordingHost(string home, SessionProcesses processes) : ITransportHost
    {
        public List<string> Asked { get; } = [];

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

        public IReadOnlyList<string>? Command(string adapter) => throw new NotSupportedException();

        public ProcessStartInfo Prepared(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next) =>
            throw new NotSupportedException();

        public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => throw new NotSupportedException();
    }

    /// <summary>
    /// The contract holds the five members every executor uses (design §4.1, round 5): the records, the carriers, the end, the go-on and
    /// the restart, and no word operation beyond the two it already had.
    /// </summary>
    [Fact]
    public void The_contract_gains_the_five_members_and_no_word_operation()
    {
        var methods = typeof(AgentRun).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.IsAbstract)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "ApplyChoiceAsync", "CloseAsync", "EndAsync", "GoOnAsync", "HandCarriers", "HandRecords", "OpenAsync", "RestartAsync",
                "ResumeAsync", "SendAsync", "SteerAsync", "StopTaskAsync", "StopTurnAsync",
            ],
            methods);
        Assert.True(typeof(TransportRun).IsSubclassOf(typeof(AgentRun)));
    }
}
