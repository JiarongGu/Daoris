using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END2 over real git (the files-only half is <see cref="HeldFactsTests"/>): <c>Held</c> written through the end judgement
/// by the process holding the home's lease alone, lapsing on a standing branch when its tip, its line's commit, its cycle's line or
/// its repository changes (canary 7), and a trace that survives every removal path and is never judged again.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class HeldFactsGitTests : LandedFixture
{
    private SessionBranches Grown => new(Home);

    private GrownBranch Entry(string branch) => Grown.Entries().Last(each => each.Branch == branch);

    private static IReadOnlySet<string> NoneInUse { get; } = new HashSet<string>();

    private static Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult(NoneInUse);

    private static Task<int> KeepAsync(SessionTrees trees, string root, string line = "main") =>
        trees.Ends.KeepAsync(root, "engine", "aurora", line, CancellationToken.None);

    /// <summary>A session's tree with one commit: its tree, its branch and its tip.</summary>
    private static async Task<(string Tree, string Branch, string Tip)> WorkAsync(SessionTrees trees, string root, string file)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, file, $"{file}\n", $"work on {file}");
        return (opened.Path, opened.Branch, (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim());
    }

    private static async Task<string> MergedAsync(string root, string branch)
    {
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {branch}", branch);
        return (await GitAsync(root, "rev-parse", "main")).Trim();
    }

    /// <summary>🔴 Canary 9's other half: no lease, no write; then the lease's holder writes the end and its evidence.</summary>
    [Fact]
    public async Task Only_the_leases_holder_writes_held_with_its_evidence()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, branch, tip) = await WorkAsync(trees, root, "a.txt");
        var line = await MergedAsync(root, branch);
        Assert.StartsWith("unit-", Entry(branch).Unit, StringComparison.Ordinal);

        Assert.Equal(0, await KeepAsync(trees, root));
        Assert.Null(Entry(branch).Held);

        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(1, await KeepAsync(trees, root));

        var held = Entry(branch).Held!;
        Assert.Equal((EndBy.Merged, tip, "main", "main", line), (held.By, held.Tip, held.Line, held.On, held.OnAt));
        Assert.Equal(FolderIdentity.Of(Path.Combine(root, ".git")), held.Repository);
        Assert.Equal(held.At, held.Since);
        // A judgement that finds what it found writes nothing.
        Assert.Equal(0, await KeepAsync(trees, root));
        Assert.Equal(held, Entry(branch).Held);
    }

    /// <summary>🔴 Canary 7: the line moving on keeps the fact; the line rewound past it, or the tip moving, lapses it.</summary>
    [Fact]
    public async Task A_fact_lapses_when_the_line_is_rewound_or_the_tip_moves_and_holds_while_the_line_moves_on()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch, _) = await WorkAsync(trees, root, "a.txt");
        var (_, rewound, _) = await WorkAsync(trees, root, "b.txt");
        await MergedAsync(root, branch);
        await MergedAsync(root, rewound);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(2, await KeepAsync(trees, root));
        var first = Entry(branch).Held!;

        await CommitAsync(root, "c.txt", "c\n", "the line moves on");
        Assert.Equal(0, await KeepAsync(trees, root));
        Assert.Equal(first, Entry(branch).Held);

        await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~2");
        await CommitAsync(tree, "d.txt", "d\n", "the tip moves");
        Assert.Equal(2, await KeepAsync(trees, root));

        Assert.Null(Entry(branch).Held);
        Assert.Null(Entry(rewound).Held);
    }

    /// <summary>🔴 Canary 7: a cycle naming another line lapses the fact.</summary>
    [Fact]
    public async Task A_fact_lapses_when_the_cycle_names_another_line()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "other");
        var trees = new SessionTrees(Home);
        var (_, branch, _) = await WorkAsync(trees, root, "a.txt");
        await MergedAsync(root, branch);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(1, await KeepAsync(trees, root));

        Assert.Equal(1, await KeepAsync(trees, root, line: "other"));

        Assert.Null(Entry(branch).Held);
    }

    /// <summary>🔴 Canary 7: a repository replaced at its path is never read as the one judged; judged again, the fact is a new one.</summary>
    [Fact]
    public async Task A_fact_lapses_when_the_repository_is_replaced_at_its_path()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, branch, _) = await WorkAsync(trees, root, "a.txt");
        await MergedAsync(root, branch);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(1, await KeepAsync(trees, root));
        var before = Entry(branch).Held!;

        var aside = root + "-aside";
        Directory.Move(root, aside);
        Copy(aside, root);
        Assert.NotEqual(before.Repository, FolderIdentity.Of(Path.Combine(root, ".git")));

        Assert.Equal(1, await KeepAsync(trees, root));

        var after = Entry(branch).Held!;
        Assert.Equal(FolderIdentity.Of(Path.Combine(root, ".git")), after.Repository);
        Assert.Equal(after.At, after.Since);
    }

    /// <summary>
    /// 🔴 A trace survives the person's removal (TreeRemovals' own forget) and the look's tidy (the replay's forget), and is never judged
    /// again: a line rewound under it leaves its fact as it was.
    /// </summary>
    [Fact]
    public async Task A_trace_survives_the_removal_and_the_looks_tidy_and_is_never_judged_again()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (removedTree, removed, _) = await WorkAsync(trees, root, "a.txt");
        var (_, tidied, _) = await WorkAsync(trees, root, "b.txt");
        await MergedAsync(root, removed);
        await MergedAsync(root, tidied);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(2, await KeepAsync(trees, root));

        Assert.True((await trees.RemoveAsync(removedTree)).Removed);
        var pass = await trees.TidyEmptyAsync(root, "engine", "aurora", InUseAsync);
        Assert.Contains(pass.Results, result => result.Removed && result.Item.Branch == tidied);

        var traces = Grown.Entries();
        Assert.Empty(Grown.All());
        Assert.All(traces, each => Assert.NotNull(each.GoneAt));
        Assert.All(traces, each => Assert.Equal(EndBy.Merged, each.Held?.By));
        await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~2");
        Assert.Equal(0, await KeepAsync(trees, root));
        Assert.Equal(traces, Grown.Entries());
    }

    /// <summary>
    /// 🔴 <i>Accept</i>'s tidy under the merge form: the end is judged before the tree goes, so its entry is kept ended. Under the branch
    /// form the session branch goes inside a landing branch the line does not hold yet: its unit stays open, named in the landing's
    /// <c>Units</c>, until the landing's own end retires it.
    /// </summary>
    [Fact]
    public async Task Accepts_tidy_keeps_an_ended_trace_under_the_merge_form_and_an_open_unit_under_the_branch_form()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, Tidy: true)).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var (mergedTree, merged, _) = await WorkAsync(trees, root, "a.txt");
        Assert.True((await trees.LandAsync(mergedTree, new LandingSubject("s1", "17", "merged work"))).Landed);
        Assert.Equal(EndBy.Merged, Entry(merged).Held?.By);
        Assert.NotNull(Entry(merged).GoneAt);

        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var (handedTree, handed, _) = await WorkAsync(trees, root, "b.txt");
        var landed = await trees.LandAsync(handedTree, new LandingSubject("s2", "18", "handed work"));
        Assert.True(landed.Landed, landed.Message);

        var trace = Entry(handed);
        var landing = new LandedBranches(Home).Of("engine", landed.Branch!)!;
        Assert.NotNull(trace.GoneAt);
        Assert.Null(trace.Held);
        Assert.Equal([trace.Unit!], landing.Units);
        Assert.True(SessionBranches.Open(trace, SessionBranches.OpenUnits(Home)));

        await MergedAsync(root, landed.Branch!);
        Assert.Equal(1, await KeepAsync(trees, root));
        Assert.Equal(EndBy.Merged, new LandedBranches(Home).Of("engine", landed.Branch!)!.Held?.By);
        Assert.False(SessionBranches.Open(trace, SessionBranches.OpenUnits(Home)));
    }

    /// <summary>
    /// FABRIC-END2d (a), a guard green before and after: a session branch recorded before END2, with no unit, whose squashing landing's branch
    /// the line holds, is kept ended with no unit naming its landing. The line holds the squash commit, whose tree is the session's own, so
    /// the content proof ends it on the line, as the clean-up's press proves it there (<c>SquashedHoldAsync</c>, then the content).
    /// </summary>
    [Fact]
    public async Task A_session_branch_from_before_units_whose_squash_landing_the_line_holds_is_kept_ended()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, branch, landing) = await OlderSquashAsync(trees, root);
        Assert.Null(Entry(branch).Unit);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        await KeepAsync(trees, root);

        var held = Entry(branch).Held;
        Assert.NotNull(held);
        Assert.Equal((EndBy.OnLine, "main"), (held.By, held.On));
        Assert.Equal(EndBy.Merged, new LandedBranches(Home).Of("engine", landing)!.Held?.By);
    }

    /// <summary>
    /// 🔴 FABRIC-END2d (b): a landing inside another whose own proof ended it is kept ended inside that one (D102's group, as the clean-up's
    /// <c>Inside</c> proves it), its fact resting on that branch at the commit read; and the session branch it carries ends with it. With no
    /// units (an install from before END2), the session branch ends through the landing whose branch holds its work.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_landing_inside_another_the_line_holds_is_kept_ended_inside_it(bool units)
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (a, b, _, first) = await InsideLandingAsync(trees, root);
        if (!units) ForgetUnits();
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        await KeepAsync(trees, root);

        var landings = new LandedBranches(Home);
        Assert.Equal(EndBy.OnLine, landings.Of("engine", b.Branch!)!.Held?.By);
        var inside = landings.Of("engine", a.Branch!)!.Held;
        Assert.NotNull(inside);
        Assert.Equal((EndBy.Inside, b.Branch, (await GitAsync(root, "rev-parse", b.Branch!)).Trim()), (inside.By, inside.On, inside.OnAt));
        Assert.Equal((EndBy.Landed, units ? a.Branch : b.Branch), (Entry(first).Held?.By, Entry(first).Held?.On));
        // A judgement that finds what it found writes nothing.
        Assert.Equal(0, await KeepAsync(trees, root));
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }
}
