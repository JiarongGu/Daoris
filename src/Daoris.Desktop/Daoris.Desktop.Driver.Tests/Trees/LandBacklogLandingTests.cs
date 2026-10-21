using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACKLOG1 (D87's LANDBACKLOG1 note): a landing's replay onto the line's tip (LANDREBASE1) that conflicts only in the backlog the
/// line's <c>daoris.json</c> declares settles it by the merge base, as the merge tool settles it (MERGETASKS1): a row one side added is
/// kept, one a side removed is dropped. Replaying a branch onto <c>main</c> once conflicted in <c>TASKS.md</c> alone and was refused,
/// and in a repository where every session archives its own row that refusal cost the session a rebase turn over a line the merge tool
/// settles by itself. A row both sides edited, a backlog conflict beside one in code, and a file only the session declares its backlog
/// stay the session's conflict, refused with the rebase words. Real git in a scratch repository, so this is the suite's Process half.
/// The rule itself is <c>BacklogMergeTests</c>' table.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandBacklogLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    private const string Rows = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n- [ ] **A3** three.\n";

    /// <summary>
    /// The line declares <c>docs/backlog.md</c> its backlog. The work archives row A2 while the line adds B1 above it, which git reads as
    /// one conflict: the plan says none, the press lands the work replayed onto the line's tip with both changes, and says it settled it.
    /// </summary>
    [Fact]
    public async Task A_conflict_only_in_the_declared_backlog_is_settled_by_its_base_and_lands_replayed()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "backlog.md"), Rows);
        await CommitAsync(root, "daoris.json", "{ \"documents\": { \"backlog\": { \"path\": \"docs/backlog.md\" } } }\n", "the backlog declared");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, Path.Combine("docs", "backlog.md"), Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await CommitAsync(root, Path.Combine("docs", "backlog.md"), Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's row");
        var line = await HeadAsync(root);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);
        Assert.Equal(["the work"], plan.Lands!.Select(commit => commit.Subject));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith($"put the work on `{Branch}` — 1 commit(s) replayed onto `main`, which moved since the work began.", landed.Message);
        Assert.Contains(
            " The backlog `docs/backlog.md` conflicted with the line's and was settled by the merge base: 1 line(s) a side added kept, "
            + "1 line(s) a side removed dropped.",
            landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("# Tasks\n\n- [ ] **A1** one.\n- [ ] **B1** the line's.\n- [ ] **A3** three.\n", await GitAsync(root, "show", $"{Branch}:docs/backlog.md"));
        Assert.Equal("the work\n", await GitAsync(root, "show", $"{Branch}:work.txt"));
        Assert.Equal((await GitAsync(root, "rev-parse", Branch)).Trim(), await HeadAsync(tree.Path));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// One row both sides edited is the session's, as the merge tool leaves it for a hand: refused with git's line and the rebase words,
    /// nothing made and the tree as it was. With no manifest the backlog is <c>TASKS.md</c>.
    /// </summary>
    [Fact]
    public async Task A_backlog_row_edited_on_both_sides_is_refused_with_the_rebase_words()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, the work's."), "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, the line's."), "the line's edit");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("TASKS.md", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Refused, refused.Refusal);
        Assert.StartsWith("Nothing was landed: replaying its own commits onto `main` conflicts with what the line changed since: CONFLICT", refused.Message);
        Assert.Contains("CONFLICT (content): Merge conflict in TASKS.md", refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began, and landing it conflicts with what the line changed: CONFLICT",
            refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>A backlog conflict its base would settle, beside one in code, stays the session's: refused, the code's file named.</summary>
    [Fact]
    public async Task A_backlog_conflict_beside_a_code_conflict_is_refused()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\nfrom the tree\n");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "one\nfrom the line\n");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's own");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("shared.txt", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in shared.txt", refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`", refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// The declaration read is the line's, never the session's: a session that declares <c>TASKS.md</c> its backlog in its own commit,
    /// where the line declares another file, has its <c>TASKS.md</c> conflict refused as any file's is.
    /// </summary>
    [Fact]
    public async Task A_backlog_only_the_session_declared_is_not_settled()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "TASKS.md"), Rows);
        await CommitAsync(root, "daoris.json", "{ \"documents\": { \"backlog\": \"docs/backlog.md\" } }\n", "another backlog declared");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "daoris.json"), "{ \"documents\": { \"backlog\": \"TASKS.md\" } }\n");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's row");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("TASKS.md", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in TASKS.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    /// <summary>How many working trees git lists for the checkout: a tree of Daoris's own made for a replay is gone after it.</summary>
    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>What a refused landing must leave as it was, as <c>LandRebaseLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
