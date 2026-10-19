using System.Diagnostics;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPSTUCK1: Ask Daoris's start waits for the machine's reading for its room only so long. On the owner's loaded machine the
/// reading walked git across every tree and repository and did not answer, and the conversation placed for the person's words
/// sat at <c>queued</c>: nothing said, no agent started, the words held for it. Since PARALLEL1a (D181 point 1) the room is
/// written from the facts the judges keep and the start waits on nothing, the bound only a backstop for a home with no facts
/// judged and no room written.
/// </summary>
/// <remarks>No process: the start gate's stand-in answers the agent's start (PROCSTART2), and the service is a stand-in.</remarks>
public sealed class HelpStartReadTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-read-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpStartReadTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachine Machine = new() { Adapter = "acp-stub", Helper = "acp-stub", Cap = 1 };

    /// <summary>How long a test waits for a start that should be over well within it; the start waits a fifth of a second.</summary>
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    /// <summary>A reading of the machine that never answers, as the owner's did.</summary>
    private static Task<HelpMachine?> Never(CancellationToken ct) => new TaskCompletionSource<HelpMachine?>().Task;

    private (ChatRunner Runner, SessionEvents Events, ServiceClient Client, DriverConfig Config, HelpPlaced Placed) Placed(
        StandInService service, string id)
    {
        service.Seed(id, "queued");
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            HelpReadBound = TimeSpan.FromMilliseconds(200),
        };
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", "never-started.mjs"] },
        };
        var placed = new HelpPlaced(id, $"Ask Daoris `{id}` opened.")
        {
            Adapter = adapters.Resolve("acp-stub"),
            Selection = new HarnessSelection(Refusal: null),
        };
        return (runner, events, client, config, placed);
    }

    /// <summary>
    /// PARALLEL1a (D181 point 1): a start before the machine's facts were ever judged, with a room written before, starts at once
    /// on that room, never waiting the bound: the agent is started in it, and its conversation says so first, by a code the page
    /// words. The reading left behind writes the room for the next conversation when it answers.
    /// </summary>
    [Fact]
    public async Task A_start_before_the_facts_were_judged_starts_at_once_on_the_room_as_last_written_and_says_so()
    {
        HelpRoom.Prepare(_home, Machine);
        await using var service = StandInService.Start(_home);
        var (runner, events, client, config, placed) = Placed(service, "help0001");
        using var _c = client;
        using var _r = runner;
        runner.HelpReadBound = TimeSpan.FromMinutes(5);
        var late = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProcessStartInfo? spawned = null;

        using (ProcessStarts.Within(info =>
               {
                   spawned = info;
                   return null;
               }))
        {
            await runner.RunHelpAsync(placed, config, _ => late.Task).WaitAsync(Within);
        }

        // At once, though the bound is minutes: the agent's start was made, in the room.
        Assert.NotNull(spawned);
        Assert.Equal(Path.GetFullPath(HelpRoom.PathOf(_home)), Path.GetFullPath(spawned!.WorkingDirectory));
        var said = Assert.Single(events.Page("help0001").Events, e => e.Kind == SessionEventKind.Note && e.Parts is { Count: > 0 });
        Assert.Equal("help.room-not-read-yet", Assert.Single(said.Parts!).Code);
        Assert.Contains("not read yet", said.Text);

        // The reading answers later: the room is written from it, for the next conversation.
        late.SetResult(Machine with { Repositories = [new("written-late", "default") { Checkout = true }] });
        var agents = Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md");
        await Poll.Until(() => File.ReadAllText(agents).Contains("`written-late`", StringComparison.Ordinal), within: Within);
    }

    /// <summary>
    /// PARALLEL1a's proof: one repository's judge never answers, and a start does not wait on it. The room is written at once from
    /// the facts as they stand, saying that repository not read yet and the other's line, and the conversation says first that some
    /// of the machine is not read yet; the start's own path starts no git.
    /// </summary>
    [Fact]
    public async Task A_start_with_a_repository_whose_judge_never_answers_starts_at_once_and_its_room_says_it_not_read_yet()
    {
        await using var service = StandInService.Start(_home);
        var (runner, events, client, config, placed) = Placed(service, "help0005");
        using var _c = client;
        using var _r = runner;
        runner.HelpReadBound = TimeSpan.FromMinutes(5);
        using var facts = new MachineFacts();
        var engine = new RepoView("engine", Adopted: true, Root: "checkouts/engine");
        var site = new RepoView("site", Adopted: true, Root: "checkouts/site");
        HelpFacts.Keep(facts, new HelpFactJudges(
            Machine: _ => Task.FromResult<HelpMachineWhole?>(new(new Snapshot([], [engine, site], []), [], [])),
            Sessions: _ => Task.FromResult<SessionLook?>(new SessionLook([], [], [])),
            History: _ => Task.FromResult<IReadOnlyList<HistoryReading>?>([]),
            Work: (_, _) => Task.FromResult<HelpWork?>(new HelpWork([], [], [])))
        {
            Guess = (root, _) => root == site.Root ? new TaskCompletionSource<string?>().Task : Task.FromResult<string?>("main"),
        });
        facts.Start();
        await facts.FirstAsync(HelpFacts.Lines, "engine", CancellationToken.None).WaitAsync(Within);
        var gits = 0;
        var took = Stopwatch.StartNew();

        using (ProcessStarts.Within(_ => null))
        {
            WorkingTree.Started.Value = _ => Interlocked.Increment(ref gits);
            try
            {
                await runner.RunHelpAsync(
                        placed, config, async ct => HelpFactsTests.Describe((await HelpFacts.ReadAsync(facts, config, ct))!, config))
                    .WaitAsync(Within);
            }
            finally
            {
                WorkingTree.Started.Value = null;
            }
        }

        Assert.True(took.Elapsed < TimeSpan.FromSeconds(5), $"the start took {took.Elapsed}");
        Assert.Equal(0, gits);
        var agents = File.ReadAllText(Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md"));
        Assert.Contains("| `site` | not driven | not read yet |", agents);
        Assert.Contains("| `engine` | not driven | `main` (the checkout's own) |", agents);
        var said = Assert.Single(events.Page("help0005").Events, e => e.Kind == SessionEventKind.Note && e.Parts is { Count: > 0 });
        Assert.Equal("help.room-not-read-yet", Assert.Single(said.Parts!).Code);
    }

    /// <summary>
    /// ASKSCOPE1b (D158's ASKSCOPE1 note point 4): a named workspace's start is the same start, no git on its press (D181): its own
    /// room folder written at once from the facts projected to its workspace, its agent started there, and the machine's room
    /// not written. Another workspace's line not judged yet is no part of its room, so its conversation is told nothing is
    /// missing.
    /// </summary>
    [Fact]
    public async Task A_named_workspace_s_start_writes_its_own_room_from_the_facts_and_starts_no_git()
    {
        await using var service = StandInService.Start(_home);
        var (runner, events, client, config, placed) = Placed(service, "help0006");
        using var _c = client;
        using var _r = runner;
        runner.HelpReadBound = TimeSpan.FromMinutes(5);
        using var facts = new MachineFacts();
        var engine = new RepoView("engine", Adopted: true, Root: "checkouts/engine", Workspace: "alpha");
        var site = new RepoView("site", Adopted: true, Root: "checkouts/site", Workspace: "beta");
        HelpFacts.Keep(facts, new HelpFactJudges(
            Machine: _ => Task.FromResult<HelpMachineWhole?>(new(new Snapshot([], [engine, site], []), [], [])),
            Sessions: _ => Task.FromResult<SessionLook?>(new SessionLook([], [], [])),
            History: _ => Task.FromResult<IReadOnlyList<HistoryReading>?>([]),
            Work: (_, _) => Task.FromResult<HelpWork?>(new HelpWork([], [], [])))
        {
            Guess = (root, _) => root == site.Root ? new TaskCompletionSource<string?>().Task : Task.FromResult<string?>("main"),
        });
        facts.Start();
        await facts.FirstAsync(HelpFacts.Lines, "engine", CancellationToken.None).WaitAsync(Within);
        var gits = 0;
        ProcessStartInfo? spawned = null;

        using (ProcessStarts.Within(info =>
               {
                   spawned = info;
                   return null;
               }))
        {
            WorkingTree.Started.Value = _ => Interlocked.Increment(ref gits);
            try
            {
                await runner.RunHelpAsync(
                        placed with { Workspace = "alpha" }, config,
                        async ct => HelpFactsTests.Describe((await HelpFacts.ReadAsync(facts, config, ct))!, config))
                    .WaitAsync(Within);
            }
            finally
            {
                WorkingTree.Started.Value = null;
            }
        }

        Assert.Equal(0, gits);
        var alpha = HelpRoom.PathOf(_home, "alpha");
        Assert.Equal(Path.GetFullPath(alpha), Path.GetFullPath(spawned!.WorkingDirectory));
        var agents = File.ReadAllText(Path.Combine(alpha, "AGENTS.md"));
        Assert.Contains("This conversation is workspace `alpha`'s", agents);
        Assert.Contains("| `engine` | not driven | `main` (the checkout's own) |", agents);
        Assert.DoesNotContain("| `site` |", agents);
        Assert.DoesNotContain("- Not read yet:", agents);
        Assert.False(File.Exists(Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md")));
        Assert.DoesNotContain(events.Page("help0006").Events, e => e.Parts is { Count: > 0 } parts && parts[0].Code == "help.room-not-read-yet");
    }

    /// <summary>
    /// A start that never spawned and left its record <c>queued</c> (the owner's `dc46ef27`, its driver gone) holds the room: every
    /// later Ask Daoris start is refused naming it. The person's stop on that one record ends it, as it ends one left
    /// <c>starting</c>; the loop's own sweep still takes neither.
    /// </summary>
    [Fact]
    public async Task The_person_s_stop_ends_a_record_left_queued_that_nothing_here_runs()
    {
        await using var service = StandInService.Start(_home);
        service.Seed("help0003", "queued");
        service.Seed("help0004", "queued");
        using var client = new ServiceClient(service.Url, null);
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));

        var swept = await Orphans.EndAsync(client, processes);
        var stopped = await SessionMoves.StopAsync(processes, client, "help0003");

        Assert.Empty(swept);
        Assert.True(stopped.Stopped);
        Assert.Equal(("stopped", "queued"), (service.State("help0003"), service.State("help0004")));
    }

    /// <summary>No room was ever written: nothing can serve, so the start ends failed saying why, by a code, never queued.</summary>
    [Fact]
    public async Task A_reading_that_does_not_answer_in_time_with_no_room_written_ends_failed_saying_why()
    {
        await using var service = StandInService.Start(_home);
        var (runner, _, client, config, placed) = Placed(service, "help0002");
        using var _c = client;
        using var _r = runner;
        var ended = new List<(string, string)>();

        var start = await runner.RunHelpAsync(
            placed, config, Never, onEnded: (session, state) =>
            {
                ended.Add((session, state));
                return Task.CompletedTask;
            }).WaitAsync(Within);

        Assert.Null(start.SessionId);
        Assert.Contains("was not read within", start.Message);
        Assert.Equal("failed", service.State("help0002"));
        Assert.Contains("was not read within", service.Note("help0002"));
        Assert.Equal([("help0002", "failed")], ended);
    }
}
