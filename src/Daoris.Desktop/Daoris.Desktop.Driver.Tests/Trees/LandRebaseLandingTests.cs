using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDREBASE1 (D87's LANDREBASE1 note): a session's work lands on the line's current tip. On the install six sessions landed as
/// branches at the bases their trees started from, so nearly every merge after conflicted, and the parent resolved them without the
/// sessions' context. Now every landing whose line moved since its tree left it replays the work onto the line's tip first, aside, as
/// LANDSYNC1 replays what the line lacks: a clean replay lands as replayed and the session's tree follows it; a conflict is refused
/// with the rebase words, for the session to resolve in its own tree; and a conflict only in files the repository marks generated
/// takes the line's copy of them, to be written again where the work is merged. Real git in a scratch repository, so this is the
/// suite's Process half; nothing reaches a network (an <c>origin</c> is a bare repository beside it).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandRebaseLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    /// <summary>
    /// The line moved and holds none of the tree's commits: the plan says it replays them, the press puts them on the line's tip, the
    /// branch descends from it, and the session's tree follows, so its next turn starts in sync.
    /// </summary>
    [Fact]
    public async Task A_branch_landing_whose_line_moved_lands_replayed_onto_its_tip_and_the_tree_follows()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        var line = await HeadAsync(root);
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);

        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Empty(plan.LeftOut!);
        Assert.Equal(["the work"], plan.Lands!.Select(commit => commit.Subject));
        Assert.Null(plan.Conflict);
        Assert.Contains("trees: the line moved since its work began, so it would replay its 1 commit(s) onto the line first: ", string.Join("\n", LandingSyncWords.Plan(plan)));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith($"put the work on `{Branch}` — 1 commit(s) replayed onto `main`, which moved since the work began.", landed.Message);
        Assert.Empty(landed.LeftOut!);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("the work", (await GitAsync(root, "log", "--format=%s", $"main..{Branch}")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", Branch)).Trim(), await HeadAsync(tree.Path));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Contains("The session's tree now stands on what landed", landed.Message);
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// A replay that conflicts with what the line changed: refused with git's lines and the rebase words, nothing made and the tree as it
    /// was; the session rebases in its own tree as the words ask, resolves and commits, and the next press lands its work on the line's
    /// tip as it stands.
    /// </summary>
    [Fact]
    public async Task A_conflicting_landing_is_refused_with_the_rebase_words_and_lands_after_the_session_rebases()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "one\nfrom the tree\n", "the work");
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("shared.txt", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Refused, refused.Refusal);
        Assert.StartsWith("Nothing was landed: replaying its own commits onto `main` conflicts with what the line changed since: CONFLICT", refused.Message);
        Assert.Contains("CONFLICT (content): Merge conflict in shared.txt", refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began, and landing it conflicts with what the line changed: CONFLICT",
            refused.Conflict.Words);
        Assert.Contains("rerun the tests your change reaches, and commit.", refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
        Assert.Empty(trees.Recorded.All());

        // The session does as it is told, in its own tree: onto the line, resolved, committed.
        await GitAsync(tree.Path, "-c", "core.editor=:", "rebase", "main");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\nfrom the line\nfrom the tree\n");
        await GitAsync(tree.Path, "add", "shared.txt");
        await GitAsync(tree.Path, "-c", "core.editor=:", "rebase", "--continue");
        var line = await HeadAsync(root);
        Assert.Equal(line, (await GitAsync(tree.Path, "rev-parse", "HEAD~1")).Trim());

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith($"put the work on `{Branch}` — 1 commit(s) from `main`.", landed.Message);
        Assert.Equal(await HeadAsync(tree.Path), (await GitAsync(root, "rev-parse", Branch)).Trim());
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
    }

    /// <summary>
    /// A conflict only in a file the repository marks generated (<c>linguist-generated</c> in its <c>.gitattributes</c>) is not the
    /// session's: the replay takes the line's copy of it and goes on, the plan says no conflict, and the landing names the file, to be
    /// written again where the work is merged.
    /// </summary>
    [Fact]
    public async Task A_conflict_only_in_a_generated_file_takes_the_line_s_copy_and_lands()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "index"));
        await CommitAsync(root, ".gitattributes", "index/** linguist-generated\n", "generated paths");
        await CommitAsync(root, "index/map.md", "map: one\n", "the index");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, "index/map.md", "map: one\nmap: the work\n", "the work");
        await CommitAsync(root, "index/map.md", "map: one\nmap: the line\n", "the line's own");
        var line = await HeadAsync(root);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);
        Assert.Equal(["the work"], plan.Lands!.Select(commit => commit.Subject));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("1 generated file(s) the line changed too took the line's copy (index/map.md), to be written again where the work is merged.",
            landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("map: one\nmap: the line\n", await GitAsync(root, "show", $"{Branch}:index/map.md"));
        Assert.Equal("the work\n", await GitAsync(root, "show", $"{Branch}:work.txt"));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// GENREGEN1: where the repository declares the command that writes the generated folder (<c>daoris.gates.json</c>'s
    /// <c>generated</c>) and the person accepted that declaration for its landed route, the replay writes the file again from the replayed
    /// tree, in a commit of Daoris's on top, so the landed index reads as its tree's, the line's work and the session's both.
    /// </summary>
    [Fact]
    public async Task A_generated_file_the_replay_took_is_written_again_by_the_writer_the_person_accepted()
    {
        var root = await RepositoryAsync("engine");
        var declared = await GeneratedDeclaredAsync(root, Writer);
        Accept(declared);
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, "index/map.md", "shared.txt\nwork.txt\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "zline.txt"), "the line\n");
        await CommitAsync(root, "index/map.md", "shared.txt\nzline.txt\n", "the line's own");
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains($"1 generated file(s) the line changed too took the line's copy (index/map.md), then were written again from the replayed tree by `{Writer}`.",
            landed.Message);
        Assert.Equal("shared.txt\nwork.txt\nzline.txt\n", await GitAsync(root, "show", $"{Branch}:index/map.md"));
        Assert.Equal("Write index again from the replayed tree", (await GitAsync(root, "log", "-1", "--format=%s", Branch)).Trim());
        Assert.Equal("the work", (await GitAsync(root, "log", "-1", "--format=%s", $"{Branch}~1")).Trim());
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~2")).Trim());
        Assert.Equal("the work\n", await GitAsync(root, "show", $"{Branch}:work.txt"));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// GENREGEN1: a writer runs only as the person accepted it, from the declaration at the line, never the session's own copy: a session
    /// that declares one in its work lands with the line's copy of the generated file, as a repository declaring none does.
    /// </summary>
    [Fact]
    public async Task A_writer_the_session_declared_itself_is_not_run_and_the_line_s_copy_stays()
    {
        var root = await RepositoryAsync("engine");
        var declared = await GeneratedDeclaredAsync(root, writer: null);
        Accept(declared);
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, LandedGates.File), Declaration(Writer));
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, "index/map.md", "shared.txt\nwork.txt\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "zline.txt"), "the line\n");
        await CommitAsync(root, "index/map.md", "shared.txt\nzline.txt\n", "the line's own");
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("1 generated file(s) the line changed too took the line's copy (index/map.md), to be written again where the work is merged.",
            landed.Message);
        Assert.Equal("shared.txt\nzline.txt\n", await GitAsync(root, "show", $"{Branch}:index/map.md"));
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
    }

    /// <summary>The same conflict beside one in code stays the session's: refused, the code's file named, nothing made.</summary>
    [Fact]
    public async Task A_conflict_in_code_beside_a_generated_file_is_still_refused()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "index"));
        await CommitAsync(root, ".gitattributes", "index/** linguist-generated\n", "generated paths");
        await CommitAsync(root, "index/map.md", "map: one\n", "the index");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\nfrom the tree\n");
        await CommitAsync(tree.Path, "index/map.md", "map: one\nmap: the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "one\nfrom the line\n");
        await CommitAsync(root, "index/map.md", "map: one\nmap: the line\n", "the line's own");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("shared.txt", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in shared.txt", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// TREEFOLLOW1's own branch, not on its remote, whose line moved: replayed in place, keeping its name, and the tree stays on it.
    /// </summary>
    [Fact]
    public async Task The_tree_s_own_branch_is_replayed_in_place_where_the_line_moved()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/own", landed.Branch);
        Assert.StartsWith("lands `feature/own`, the session's own branch, replayed in place — 1 commit(s) replayed onto `main`, which moved since the work began.",
            landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", "feature/own~1")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "feature/own")).Trim(), await HeadAsync(tree.Path));
        Assert.Equal("feature/own", (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.False(await BranchAsync(root, Branch), "no other branch is made");
    }

    /// <summary>
    /// The same branch already on its remote: Daoris never rewrites a pushed branch (D109), and here the line holds none of its
    /// commits, so it lands as it stands, its pull request the place it meets the line, as before.
    /// </summary>
    [Fact]
    public async Task The_tree_s_own_branch_on_its_remote_lands_as_it_stands_where_the_line_holds_none_of_it()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        var tip = await HeadAsync(tree.Path);
        await GitAsync(tree.Path, "push", "--quiet", "-u", "origin", "feature/own");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");

        var plan = await trees.PlanAsync(tree.Path, Subject);
        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.Null(plan.Lands);
        Assert.Null(plan.Conflict);
        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith("lands `feature/own`, the session's own branch, as it stands — 1 commit(s) from `main`", landed.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "feature/own")).Trim());
        Assert.Equal(tip, await HeadAsync(tree.Path));
    }

    /// <summary>The merge form replays the work onto the line before it merges, so the merge's second parent grows from the line's tip.</summary>
    [Fact]
    public async Task The_merge_form_merges_the_work_replayed_onto_the_line_where_it_moved()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge)).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("1 commit(s) replayed onto `main`, which moved since the work began", landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", "main^2~1")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "main^2")).Trim(), await HeadAsync(tree.Path));
    }

    /// <summary>
    /// A stand-in for the index's writer (GENREGEN1): it lists the tree's top <c>.txt</c> files, so the line and the work, each adding
    /// one, disagree on it, and only the replayed tree's writing holds both. Read alike by <c>cmd.exe</c> and <c>/bin/sh</c>.
    /// </summary>
    private const string Writer = "git ls-files -- *.txt > index/map.md";

    private static string Declaration(string? writer) =>
        writer is null
            ? "{ \"gates\": [] }\n"
            : $"{{ \"gates\": [], \"generated\": [{{ \"paths\": \"index\", \"run\": \"{writer}\" }}] }}\n";

    /// <summary>The line's generated folder, marked so, its first copy, and its declaration; git's blob id of that declaration.</summary>
    private static async Task<string> GeneratedDeclaredAsync(string root, string? writer)
    {
        Directory.CreateDirectory(Path.Combine(root, "index"));
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "index/** linguist-generated\n");
        await File.WriteAllTextAsync(Path.Combine(root, "index", "map.md"), "shared.txt\n");
        await CommitAsync(root, LandedGates.File, Declaration(writer), "the index and its declaration");
        return (await GitAsync(root, "rev-parse", $"HEAD:{LandedGates.File}")).Trim();
    }

    /// <summary>The person's acceptance of <paramref name="gates"/> for the repository's landed route, as <i>Accept the gates</i> writes it.</summary>
    private void Accept(string gates)
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        var config = DriverConfig.Load(path);
        (config with
        {
            Workflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase)
            {
                ["engine"] = new WorkflowChoice(null, []) { Landed = new WorkflowLanded("verify-landed", gates) },
            },
        }).Save(path);
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>How many working trees git lists for the checkout: a tree of Daoris's own made for a replay is gone after it.</summary>
    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>What a refused landing must leave as it was, as <c>LineHoldsLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
