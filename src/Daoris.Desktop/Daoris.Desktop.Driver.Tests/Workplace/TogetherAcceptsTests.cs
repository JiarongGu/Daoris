using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>The combined Accept</i>, <i>Inclusion, in every form</i>): three branches (the head, a
/// part beside it, another session), the members replayed onto the head, the line moved after the head's turn, then one Accept. It lands
/// by branch, squash and merge; the members are carried in the record, read <i>brought into</i>, and never land a second time. A member
/// held by its own gate lands nothing, and a head moved since it was checked is refused under the hold before anything is made.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherAcceptsTests : LandedFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private static async Task<string> TipAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    /// <summary>The head with both members replayed onto it, the line one commit on, and the operation checked and ready.</summary>
    private async Task<(SessionTrees Trees, string Root, TreeOpened Head, TreeOpened Beside, TogetherOperation Operation)> ReadyAsync()
    {
        var root = await RepositoryAsync("engine");
        var line = await TipAsync(root);
        var trees = new SessionTrees(Home);
        var head = await trees.OpenAsync(root, "engine", "aurora");
        var beside = await trees.OpenAsync(root, "engine", "aurora");
        var other = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(head.Path, "head.txt", "head\n", "the head's work");
        await CommitAsync(beside.Path, "beside.txt", "beside\n", "the work beside it");
        await CommitAsync(other.Path, "other.txt", "other\n", "another session's work");
        var press = await TipAsync(head.Path);
        var (opened, refused) = new TogetherOperations(Home).Open("engine", new TogetherHead("s-head", "s-head", head.Path, head.Branch, press),
            [
                new TogetherMember("s-beside", beside.Path, beside.Branch, line, await TipAsync(beside.Path)) { Quest = "q2" },
                new TogetherMember("s-other", other.Path, other.Branch, line, await TipAsync(other.Path)) { Quest = "q3" },
            ], At);
        Assert.Null(refused);
        foreach (var member in opened!.Members) await GitAsync(head.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", member.Tip);
        await CommitAsync(root, "line.txt", "the line moved\n", "the line moves after the head's turn");

        new TogetherOperations(Home).TurnEnded(opened.Id, At);
        var ready = await new TogetherLook(Home, () => At).StepAsync(
            root, opened.Id, new TogetherFacts("s-head", false, null, false, "main"), null, CancellationToken.None);
        Assert.True(ready!.Phase == TogetherPhase.Ready, $"{ready.Phase}: {ready.Why}");
        return (trees, root, head, beside, ready);
    }

    private static Task<TreeLanding> AcceptAsync(SessionTrees trees, TogetherOperation operation, IReadOnlyDictionary<string, IWorkplaceGate?>? gates = null) =>
        trees.LandTogetherAsync(operation.Id, new LandingSubject("s-head", "q1", "Bring the work together"), gates ?? new Dictionary<string, IWorkplaceGate?>());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task One_Accept_on_a_moved_line_lands_the_branch_or_its_squash_carrying_the_members_who_never_land_again(bool squash)
    {
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: squash));
        var (trees, _, _, beside, operation) = await ReadyAsync();

        var landed = await AcceptAsync(trees, operation);

        Assert.True(landed.Landed, landed.Message);
        var after = new TogetherOperations(Home).Of(operation.Id)!;
        Assert.Equal(TogetherPhase.Landed, after.Phase);
        Assert.Equal(squash ? TogetherForms.Squash : TogetherForms.Branch, after.Attempt!.Form);
        var record = new LandedBranches(Home).All().Single(each => each.Branch == landed.Branch);
        Assert.Equal(["s-head", "s-beside", "s-other"], record.Work().Select(each => each.Session));
        Assert.Equal(operation.Members.Select(each => each.Tip), record.Brought.Select(each => each.Tip));
        Assert.Contains("s-beside", landed.Message);

        var again = await trees.LandAsync(beside.Path, new LandingSubject("s-beside", "q2", "The work beside it"));
        Assert.False(again.Landed);
        Assert.Equal(AutoLandingCode.Already, again.Refusal);
        Assert.Contains("brought into", again.Message);
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task One_Accept_under_a_merge_rule_merges_the_head_into_the_line_and_the_operation_is_its_inclusion_record()
    {
        Rule(new LandingRule(LandingForm.Merge, "feature/{quest}-{slug}"));
        var (trees, root, _, _, operation) = await ReadyAsync();

        var landed = await AcceptAsync(trees, operation);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(File.Exists(Path.Combine(root, "beside.txt")) && File.Exists(Path.Combine(root, "other.txt")));
        var after = new TogetherOperations(Home).Of(operation.Id)!;
        Assert.Equal((TogetherPhase.Landed, TogetherForms.Merge, "main"), (after.Phase, after.Attempt!.Form, after.Attempt.Target));
        Assert.Equal(await TipAsync(root), after.Attempt.Resulting);
        Assert.Equal(("main", true), TogetherInclusion.Of(Home, "s-beside") is { } brought ? (brought.Into, brought.Line) : default);
    }

    [Fact]
    public async Task A_member_held_by_its_own_gate_lands_nothing_and_the_operation_stays_ready()
    {
        var (trees, root, _, _, operation) = await ReadyAsync();
        var held = new HeldGate(new TreeLanding(false, "its review has not passed") { Refusal = AutoLandingCode.Unreviewed });

        var landed = await AcceptAsync(trees, operation, new Dictionary<string, IWorkplaceGate?> { ["s-other"] = held });

        Assert.False(landed.Landed);
        Assert.Contains("its review has not passed", landed.Message);
        Assert.Contains("s-other", landed.Message);
        Assert.Equal(TogetherPhase.Ready, new TogetherOperations(Home).Of(operation.Id)!.Phase);
        Assert.Empty(new LandedBranches(Home).All());
        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
    }

    [Fact]
    public async Task A_head_moved_since_it_was_checked_is_refused_under_the_hold_before_anything_is_made()
    {
        var (trees, root, head, _, operation) = await ReadyAsync();
        await CommitAsync(head.Path, "late.txt", "late\n", "a commit after the check");

        var landed = await AcceptAsync(trees, operation);

        Assert.False(landed.Landed);
        Assert.Contains("moved since it was checked", landed.Message);
        var after = new TogetherOperations(Home).Of(operation.Id)!;
        Assert.Equal(TogetherPhase.Ready, after.Phase);
        Assert.Null(after.Attempt);
        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
    }

    [Fact]
    public void An_entrys_part_bound_as_a_member_is_brought_into_its_head_and_lands_nothing_itself()
    {
        var steps = EntryLandings.Sequence(
            [new EntryLandingPart("s-head", "trees/a"), new EntryLandingPart("s-beside", "trees/b")], new HashSet<string>(), new HashSet<string> { "s-beside" });

        Assert.Equal([true, false], steps.Select(each => each.Lands));
        Assert.True(steps[1].BroughtInto);
    }

    /// <summary>A gate whose first part holds, as a member's review, opinion or workflow would.</summary>
    private sealed record HeldGate(TreeLanding Holds) : IWorkplaceGate
    {
        public string? Tip { get; init; }
        public IWorkplaceGate WithTip(string? tip) => this with { Tip = tip };
        public TreeLanding? Refusal => Holds;
        public IWorkplaceProcess? Process => null;
        public LandingReview? ReviewLanding => null;
        public LandingOpinion? OpinionLanding => null;
        public bool GraphAsksPass => false;
        public IReadOnlyList<string> PlanLines() => [];
        public LandingLine? Held(string session, (string Workspace, string Repository) owner, TreeLanding landed, string door) => null;
    }
}
