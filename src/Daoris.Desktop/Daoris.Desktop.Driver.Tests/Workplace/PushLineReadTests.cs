using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, a bare origin and a platform that squash-merges and deletes the branch: a <c>push</c> whose branch no longer stands is not
/// retried at every look. The line is fetched once, and where it holds the landing's work the proof is written on the landing; where it
/// does not, or origin cannot be reached, the node fails once with a sentence the person can act on.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PushLineReadTests : LandedFixture
{
    private const string Branch = "feature/x-0001";
    private const string Unit = "run:r1";
    private static readonly NodeDeclaration Push = new(NodeKinds.Push, NodeKinds.Push, 1, NodeReach.Performed);
    private readonly List<string> _said = [];

    private PushStart Start(string root, string tip) =>
        new(Unit, Push, "engine", "aurora", root, Branch, tip) { Authority = StepAuthority.OfDoor(Unit, Push.Id, 1) };

    private void Active(string tip) =>
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Push.Id] = new(Unit, Push.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Unreconciled = 1, Binding = new NodeAcceptance(Unit, tip, "graph-1", Push.Id, 1, 1) },
            },
        });

    private NodeStatus Status() => new NodeRecordFiles(Home, () => true).Read(Unit)!.Statuses[Push.Id];

    /// <summary>A landing pushed to origin whose pull request the platform then squash merged, deleting the branch there and here; this checkout's origin refs not fetched since.</summary>
    private async Task<(string Root, string Origin, string Tip)> MergedByPlatformAsync()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await GitAsync(root, "checkout", "--quiet", "-b", Branch);
        await CommitAsync(root, "a.txt", "a\n", "the landed work");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "push", "--quiet", "origin", Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        var entry = new LandedBranch("engine", "aurora", Branch, "main", tip, "s-0001", "q1", "Work", new MovedClock().GetUtcNow()) { Units = [Unit] };
        new LandedBranches(Home).Record(entry);
        new LandedBranches(Home).Pushed("engine", Branch, tip);
        await SquashOnPlatformAsync(origin, Branch);
        await GitAsync(root, "branch", "-D", "--quiet", Branch);
        return (root, origin, tip);
    }

    [Fact]
    public async Task A_branch_the_platform_squash_merged_and_deleted_is_read_from_the_line_and_its_proof_kept()
    {
        var (root, _, tip) = await MergedByPlatformAsync();
        Active(tip);
        Assert.Null(new LandedBranches(Home).All().Single().Held);

        await new PushDoor(Home, _said.Add, holdsLease: () => true).PerformAsync(Start(root, tip), CancellationToken.None);

        var held = Assert.IsType<HeldFact>(new LandedBranches(Home).All().Single().OnTheLine);
        Assert.Equal("origin/main", held.On);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Contains(_said, line => line.Contains("holds its work", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_branch_gone_with_the_line_not_holding_it_fails_the_push_once_with_a_sentence()
    {
        var (root, origin, tip) = await MergedByPlatformAsync();
        await GitAsync(origin, "update-ref", "refs/heads/main", (await GitAsync(origin, "rev-list", "--max-parents=0", "main")).Trim());
        Active(tip);

        await new PushDoor(Home, _said.Add, holdsLease: () => true).PerformAsync(Start(root, tip), CancellationToken.None);

        var status = Status();
        Assert.Equal((NodeStates.Failed, PushLineRead.BranchGone), (status.State, status.Code));
        Assert.Contains(_said, line => line.Contains("does not hold its work", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_branch_gone_with_origin_unreadable_fails_the_push_once_with_a_sentence()
    {
        var (root, origin, tip) = await MergedByPlatformAsync();
        await GitAsync(root, "remote", "set-url", "origin", Path.Combine(Scratch, "no-such-origin.git"));
        Active(tip);

        await new PushDoor(Home, _said.Add, holdsLease: () => true).PerformAsync(Start(root, tip), CancellationToken.None);

        var status = Status();
        Assert.Equal((NodeStates.Failed, PushLineRead.LineUnread), (status.State, status.Code));
        Assert.Contains(_said, line => line.Contains("could not be read", StringComparison.Ordinal));
        Assert.NotNull(origin);
    }
}
