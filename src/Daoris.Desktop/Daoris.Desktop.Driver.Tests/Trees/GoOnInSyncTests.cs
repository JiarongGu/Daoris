using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1c5 (the fabric events design §E5, as CLOSE1c amends it): the person went on with a step whose source they reopened,
/// and before its carried-on session starts, the step's tree is brought up to date with the source's new submission. Its own commits
/// are replayed onto the submission's commit, as the tree sync replays a branch onto the line (WSR6, D88), its branch moved last and
/// its record cut there; then the line, as any start's (LANDSYNC2). A conflict leaves the tree exactly as it was, tells the session what
/// conflicted and that the person must resolve it, and changes nothing else. A host that names no commit has the line alone synced.
/// </summary>
/// <remarks>🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GoOnInSyncTests : TreeSyncFixture
{
    /// <summary>
    /// The source's session submitted at `p1`; the step grew from its branch there and committed `s1`. The person reopened the source,
    /// its session went on (`p2`) and submitted again; the person went on with the step. Its carry-on starts in a tree holding `p2`.
    /// </summary>
    [Fact]
    public async Task The_go_ons_carry_on_starts_in_a_tree_holding_the_new_submissions_commit()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        await CommitAsync(source.Path, "p2.txt", "the source's new work\n", "p2");
        var submitted = await RevAsync(source.Path, "HEAD");

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", submitted);

        Assert.Equal(0, await ExitAsync(step.Path, "merge-base", "--is-ancestor", submitted, "HEAD"));
        Assert.Equal(["s1"], await SubjectsAsync(root, $"{submitted}..{step.Branch}"));
        Assert.Equal(step.Branch, (await GitAsync(step.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(step.Path, "status", "--porcelain")).Trim());
        Assert.Equal(submitted, trees.Grown.Of("engine", step.Branch)?.From);
        Assert.Equal(source.Branch, trees.Grown.Of("engine", step.Branch)?.GrewFrom);
        Assert.Equal(
            $"Before this turn Daoris brought this tree up to date with `#q1`'s new submission (`{submitted[..8]}`): its 1 commit(s) of its "
            + "own were replayed onto that commit, as a tree is brought up to date with the line. Read the log before you go on: the commits "
            + "you made have new ids.",
            started.Told);
    }

    /// <summary>A tree that already holds the new submission's commit is left as it was, and the session is told nothing of it.</summary>
    [Fact]
    public async Task A_tree_already_holding_the_new_submission_is_left_and_told_nothing()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        var head = await RevAsync(step.Path, "HEAD");

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", await RevAsync(source.Path, "HEAD"));

        Assert.Equal(TreeStartSync.Nothing, started);
        Assert.Equal(head, await RevAsync(step.Path, "HEAD"));
    }

    /// <summary>
    /// A conflict: the step changed the line its source's new work changed too. The tree is exactly as it was, on its branch, clean; the
    /// session is told what conflicted and that the person must resolve it; and nothing else is changed, the line's sync included.
    /// </summary>
    [Fact]
    public async Task A_conflict_leaves_the_tree_exactly_as_it_was_and_the_session_is_told_the_person_must_resolve_it()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        await CommitAsync(step.Path, "shared.txt", "one\nthe step's\n", "s2");
        await CommitAsync(source.Path, "shared.txt", "one\nthe source's\n", "p2");
        var submitted = await RevAsync(source.Path, "HEAD");
        // The line came to hold the step's first commit too: a line sync would move the tree, and must not.
        await PlatformCommitAsync(origin, "s.txt", "s\n", "s1 (#7)");
        var head = await RevAsync(step.Path, "HEAD");
        var recorded = trees.Grown.Of("engine", step.Branch);

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", submitted);

        Assert.Null(started.Moved);
        Assert.StartsWith(
            $"Daoris could not bring this tree up to date with `#q1`'s new submission (`{submitted[..8]}`) before this turn: CONFLICT",
            started.Told);
        Assert.Contains("shared.txt", started.Told);
        Assert.Contains("The tree is exactly as it was, and nothing else was brought up to date. The person must resolve this conflict", started.Told);
        Assert.Equal(head, await RevAsync(step.Path, "HEAD"));
        Assert.Equal(step.Branch, (await GitAsync(step.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(step.Path, "status", "--porcelain")).Trim());
        Assert.Equal(head, await RevAsync(root, step.Branch));
        Assert.Equal(recorded, trees.Grown.Of("engine", step.Branch));
    }

    /// <summary>A host before the field names no commit: only the line is synced, as CLOSE1c2 does, and that is said once.</summary>
    [Fact]
    public async Task With_no_commit_only_the_line_is_synced_and_that_is_said_once()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        await CommitAsync(source.Path, "p2.txt", "the source's new work\n", "p2");
        var head = await RevAsync(step.Path, "HEAD");

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", commit: null);

        Assert.Null(started.Moved);
        Assert.Equal(
            "Daoris could not bring this tree up to date with `#q1`'s new submission: the service names no commit for it (a host before "
            + "it, or a submission no evidence was read for), so only the line was brought up to date. Read what `#q1` now holds before "
            + "you build on its work.",
            started.Told);
        Assert.Equal(head, await RevAsync(step.Path, "HEAD"));
    }

    /// <summary>A commit this repository does not hold (the source's work is in another, or not fetched here) is said, and the line synced.</summary>
    [Fact]
    public async Task A_commit_this_repository_does_not_hold_is_said_and_the_tree_left()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, step) = await SourceAndStepAsync(trees, root);
        var head = await RevAsync(step.Path, "HEAD");
        const string Elsewhere = "0123456789abcdef0123456789abcdef01234567";

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", Elsewhere);

        Assert.Null(started.Moved);
        Assert.StartsWith("Daoris could not bring this tree up to date with `#q1`'s new submission (`01234567`): this repository does not hold that commit", started.Told);
        Assert.Equal(head, await RevAsync(step.Path, "HEAD"));
    }

    /// <summary>
    /// SYNCMERGE1b (D178 point 3): the step's take lost to the person's reopen of its source and its session stood down; the source submitted
    /// again (`p2`), and the person brought the step forward, its fresh take stamped with that submission. The planner brings the carry-on to
    /// that source, naming the source's current commit, and the stood-down session's tree holds it before the carry-on starts.
    /// </summary>
    [Fact]
    public async Task A_brought_forward_steps_carry_on_starts_in_a_tree_holding_its_current_source()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        await CommitAsync(source.Path, "p2.txt", "the source's new work\n", "p2");
        var submitted = await RevAsync(source.Path, "HEAD");
        var (stepView, sourceView, start) = BroughtForward(step.Path, submitted);

        Assert.True(Planner.BringsForwardToSource(stepView, start));
        var started = await trees.GoOnInSyncAsync(step.Path, $"#{stepView.Parent}", Planner.SourceCommit(stepView, sourceView));

        Assert.Equal(0, await ExitAsync(step.Path, "merge-base", "--is-ancestor", submitted, "HEAD"));
        Assert.Equal(["s1"], await SubjectsAsync(root, $"{submitted}..{step.Branch}"));
        Assert.Equal(step.Branch, (await GitAsync(step.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.StartsWith($"Before this turn Daoris brought this tree up to date with `#q1`'s new submission (`{submitted[..8]}`)", started.Told);
    }

    /// <summary>
    /// A take that lost is stopped mid-turn (D68 §5), so its tree may hold work it never committed: the bring-forward's carry-on leaves that
    /// tree exactly as it was, uncommitted work and all, and the session is told why and to build nothing on the source's new work yet.
    /// </summary>
    [Fact]
    public async Task A_brought_forward_tree_with_uncommitted_work_is_left_as_it_was_and_the_session_told()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (source, step) = await SourceAndStepAsync(trees, root);
        await CommitAsync(source.Path, "p2.txt", "the source's new work\n", "p2");
        var submitted = await RevAsync(source.Path, "HEAD");
        await File.WriteAllTextAsync(Path.Combine(step.Path, "s.txt"), "s, half done\n");
        var head = await RevAsync(step.Path, "HEAD");
        var (stepView, sourceView, _) = BroughtForward(step.Path, submitted);

        var started = await trees.GoOnInSyncAsync(step.Path, "#q1", Planner.SourceCommit(stepView, sourceView));

        Assert.Null(started.Moved);
        Assert.StartsWith(
            $"Daoris could not bring this tree up to date with `#q1`'s new submission (`{submitted[..8]}`) before this turn: its tree has "
            + "uncommitted work (1 path(s)), which a replay does not carry. The tree is exactly as it was",
            started.Told);
        Assert.Equal(head, await RevAsync(step.Path, "HEAD"));
        Assert.Equal("s, half done\n", (await File.ReadAllTextAsync(Path.Combine(step.Path, "s.txt"))).ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// The step q2 and its source q1 as the service answers them once the person brought q2 forward on q1's current submission (desk 43, at
    /// <paramref name="commit"/>), and the carry-on the planner plans of the stood-down session that worked in <paramref name="tree"/>.
    /// </summary>
    private static (QuestView Step, QuestView Source, Consideration Start) BroughtForward(string tree, string commit)
    {
        var step = new QuestView("q2", "engine", "engine", "The step", "Body", "Taken") { Parent = "q1", GrewFrom = new QuestSubmissionRef("desk", 43) };
        var source = new QuestView("q1", "game", "engine", "The source", "Body", "Done")
        {
            Submission = new QuestSubmissionView("desk", 43, null, "ready", commit),
        };
        var start = new Consideration(step, StartVerdict.Start, "carrying on")
        {
            Resumes = new PriorSession("s1", tree, "stood-down", Daoris.Driver.Driver.LostClaimToReopen),
            BroughtForward = new ForwardTake("desk", 44, DateTimeOffset.UnixEpoch, "held"),
        };
        return (step, source, start);
    }

    /// <summary>The source's tree with its first submission (`p1`), and the step grown from its branch there with one commit (`s1`).</summary>
    private static async Task<(TreeOpened Source, TreeOpened Step)> SourceAndStepAsync(SessionTrees trees, string root)
    {
        var source = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(source.Path, "shared.txt", "one\n", "p1");
        var step = await trees.OpenAsync(root, "engine", "aurora", from: source.Branch);
        Assert.Equal(source.Branch, step.GrewFrom);
        await CommitAsync(step.Path, "s.txt", "s\n", "s1");
        return (source, step);
    }

    /// <summary>Git's exit code alone, for a question it answers by it.</summary>
    private static async Task<int> ExitAsync(string cwd, params string[] arguments) =>
        (await WorkingTree.GitAsync(cwd, arguments, CancellationToken.None)).Code;
}
