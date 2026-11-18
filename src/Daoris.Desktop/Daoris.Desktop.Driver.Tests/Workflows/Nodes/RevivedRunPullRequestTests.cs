using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run revived after it ended failed at its <c>push</c> (origin then held its branch at the landed tip, so the push reads reached with no
/// push of Daoris's on the landing) goes on to its pull-request step, and the pull request that step opened is asked about at the moments
/// as any run's is, so its <c>monitor</c> reaches <c>merged</c> at the next look once the plugin says it completed.
/// </summary>
public sealed class RevivedRunPullRequestTests : NodeRunFixture
{
    private const string Address = "https://example.test/org/engine/pull/7";

    private const string Delivered = """
        {"schemaVersion":2,"entries":["claimed"],"end":"monitor",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"push","kind":"push","version":1,"reach":"performed"},{"id":"pr","kind":"acme.deploy/pull-request","version":1,"reach":"performed"},
                  {"id":"monitor","kind":"monitor","version":1,"reach":"observed","outcomes":["merged","closed"]}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>p","kind":"after","from":"handed","to":"push"},
                  {"id":"p>r","kind":"after","from":"push","to":"pr"},{"id":"r>m","kind":"after","from":"pr","to":"monitor"}]}
        """;

    /// <summary>The plugin's pull-request step as its manifest declares it.</summary>
    private static readonly NodeDeclaration Opens = new(Plugin + "/pull-request", Plugin + "/pull-request", 1, NodeReach.Performed);

    /// <summary>What origin's remote-tracking refs hold, by branch, as the look reads them.</summary>
    private readonly Dictionary<string, string> _origin = new(StringComparer.Ordinal);

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, bool whole = true) => runtime.PassAsync(RunLook(whole: whole) with
    {
        Steps = () => [Step, Build, Opens],
        Remotes = (_, branches, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
            branches.Where(_origin.ContainsKey).ToDictionary(branch => branch, branch => _origin[branch], StringComparer.Ordinal)),
    }, CancellationToken.None);

    /// <summary>The run's units as the look reads them: the quest, and its branch carrying the landing as the record holds it now.</summary>
    private void Read() =>
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], new LandedBranches(_home).Of(RunHome.Repository, "feature/q7")!)];

    [Fact]
    public async Task A_run_revived_at_its_push_has_its_pull_request_asked_and_its_monitor_reaches_merged()
    {
        BoundTo(Delivered);
        _branchTips["daoris/s1"] = Tip1;
        new LandedBranches(_home).Record(RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        Read();
        var runtime = Runtime();
        runtime.PullRequests = new PullRequestRecords(_home);
        await PassAsync(runtime);
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("handed").State, RunStatus("push").State));

        // The push door's refusal, written on the record off the pass: the run ends failed-held there.
        Files.Edit(RunUnit, record => (record with
        {
            Statuses = new Dictionary<string, NodeStatus>(record.Statuses) { ["push"] = record.Statuses["push"] with { State = NodeStates.Failed, Code = PushDoor.Refused, Unreconciled = null } },
        }, true));
        await PassAsync(runtime);
        Assert.Equal(RunOutcomes.Failed, Files.Read(RunUnit)!.Ended?.Outcome);

        // Origin holds the branch at the landed tip: the push reads reached, the run is governed again, and its plugin opens the pull request.
        _origin["feature/q7"] = Tip1;
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("push").State, RunStatus("pr").State));
        Assert.Null(Files.Read(RunUnit)!.Ended);
        var invocation = Assert.Single(_port.Sent).Invocation;
        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("pull-request",
            new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, Address, null, null)), _now, "e1", Invocation: invocation)));
        await PassAsync(runtime, whole: false);
        await PassAsync(runtime);
        var landing = new LandedBranches(_home).Of(RunHome.Repository, "feature/q7")!;
        Assert.Equal((Address, Plugin, false), (landing.PullRequest, landing.Plugin, landing.Pushed));
        Read();
        await PassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("monitor").State);

        // The pull request completes on the platform; the window's focus is a moment, and its plugin answers completed.
        var world = new AnsweringWorld(_home, () => _now);
        await using (var moments = new PullRequestMoments(_home, world, new FakeChangeSource(), () => _now))
        {
            moments.Open();
            moments.Focused();
            moments.Changes.Pump();
            await moments.Changes.Settled();
        }

        Assert.Equal(["feature/q7"], world.Asked);
        Read();
        await PassAsync(runtime);
        var monitor = RunStatus("monitor");
        Assert.Equal((NodeStates.Reached, PullRequestReading.Merged), (monitor.State, monitor.Acceptance?.Outcome));
    }

    /// <summary>
    /// The address a run's pull-request step keeps on the landing is a moment of its own: the pull request it opened is asked about at once,
    /// with no focus and no move of origin's line.
    /// </summary>
    [Fact]
    public async Task The_pull_request_a_step_opened_is_asked_about_once_its_address_is_kept()
    {
        new LandedBranches(_home).Record(RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        var world = new AnsweringWorld(_home, () => _now);
        await using var moments = new PullRequestMoments(_home, world, new FakeChangeSource(), () => _now);
        moments.Open();

        Assert.True(new PullRequestRecords(_home).Keep(RunHome.Repository, "feature/q7", Plugin, Address));
        moments.Changes.Pump();
        await moments.Changes.Settled();

        Assert.Equal(["feature/q7"], world.Asked);
        Assert.Equal(PullRequestStates.Completed, new LandedBranches(_home).Of(RunHome.Repository, "feature/q7")!.PullRequestState?.State);
    }

    /// <summary>The moments' world over the home's real landing record: no fetch due, and the plugin answers each pull request completed.</summary>
    private sealed class AnsweringWorld(string home, Func<DateTimeOffset> clock) : IPullRequestMomentWorld
    {
        public List<string> Asked { get; } = [];

        public IReadOnlyList<LandedBranch> Open() => [.. new LandedBranches(home).All().Where(PullRequestMoments.Concerns)];

        public Task<IReadOnlyList<RefRepository>> RepositoriesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RefRepository>>([new RefRepository(RunHome.Repository, "default", "checkout")]);

        public Task<string?> LineAsync(RefRepository repository, CancellationToken ct) => Task.FromResult<string?>("main");

        public string? OriginTip(RefRepository repository, string line) => null;

        public DateTimeOffset? Fetched(RefRepository repository, string line) => clock();

        public void HeardFetched(RefRepository repository, string line, DateTimeOffset at)
        {
        }

        public Task<string?> FetchAsync(RefRepository repository, string line, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<int> AskAsync(RefRepository repository, IReadOnlyList<LandedBranch> entries, CancellationToken ct)
        {
            foreach (var entry in entries)
            {
                Asked.Add(entry.Branch);
                // A completed answer is kept only with the commits it merged, as the plugin's frame must give them.
                new LandedBranches(home).Answered(entry, new PullRequestState(PullRequestStates.Completed)
                {
                    AskedAt = clock(), MergeCommit = "f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9f9", SourceCommit = entry.Tip, Target = entry.Line,
                });
            }

            return Task.FromResult(entries.Count);
        }

        public void Judge(RefRepository repository)
        {
        }
    }
}
