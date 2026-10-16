using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC1 (D87's amendment of 2026-10-15): a landing lands only what its line lacks. On the install a session's first commit
/// was squash-merged onto the line under a new id, the line then changed a changelog near it, and the session went on with two
/// more commits on its old base: Accept carried all three, so the next pull request held a second copy of merged work and
/// conflicted. Now the plan names what it leaves out and why, the press replays only the rest onto the line in a tree of
/// Daoris's own, a conflict is refused with nothing made, and the session's tree moves to what landed. Real git in a scratch
/// repository, so this is the suite's Process half; nothing reaches a network (an <c>origin</c> is a bare repository beside it).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LineHoldsLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Changelog = "# Changes\n\n- first\n";

    /// <summary>
    /// The install's case: the first commit squash-merged onto the line under a new id, the line's changelog changed beside it,
    /// two more commits on top. The plan leaves the first out by its patch id and lands the other two; the press puts those two on
    /// the current line, conflict-free, and the session's tree follows to what landed.
    /// </summary>
    [Fact]
    public async Task A_squash_merged_first_commit_is_left_out_and_the_rest_lands_on_the_current_line()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "CHANGELOG.md", Changelog, "a changelog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "CHANGELOG.md", Changelog + "- the fix\n", "the fix");
        var first = await HeadAsync(tree.Path);
        await SquashAsync(root, tree.Branch);
        var squash = await HeadAsync(root);
        await CommitAsync(root, "CHANGELOG.md", Changelog + "- the fix\n- the line's own\n", "the line's own entry");
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");
        var line = await HeadAsync(root);
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);

        Assert.Equal(before, await StateAsync(root, tree));
        var left = Assert.Single(plan.LeftOut!);
        Assert.Equal((first, LeftOutWhy.Patch, squash), (left.Sha, left.Why, left.On));
        Assert.Equal(["more of the work", "the notes"], plan.Lands!.Select(commit => commit.Subject));
        Assert.Null(plan.Conflict);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith("put the work on `feature/0fda18-fix-the-gap` — 2 commit(s) replayed onto `main`, leaving out 1 the line holds: "
            + $"`{first[..8]}` “the fix” (on the line as `{squash[..8]}`, the same change).", landed.Message);
        Assert.Equal(first, Assert.Single(landed.LeftOut!).Sha);
        var branch = "feature/0fda18-fix-the-gap";
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{branch}~2")).Trim());
        Assert.Equal("the notes\nmore of the work", (await GitAsync(root, "log", "--format=%s", $"main..{branch}")).Trim());
        Assert.Equal(0, await MergeTreeAsync(root, "main", branch));
        // The session's tree follows, on its own branch, so its next turn starts in sync.
        Assert.Equal((await GitAsync(root, "rev-parse", branch)).Trim(), await HeadAsync(tree.Path));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Contains("The session's tree now stands on what landed", landed.Message);
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>A squash of several commits matches no one commit's patch: the walk by content finds it, and only the next lands.</summary>
    [Fact]
    public async Task A_squash_of_several_is_left_out_by_the_walk_by_content()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "more of the work");
        await SquashAsync(root, tree.Branch);
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal([LeftOutWhy.Content, LeftOutWhy.Content], landed.LeftOut!.Select(commit => commit.Why));
        Assert.Contains("“the work” (on the line by content)", landed.Message);
        Assert.Equal("the notes", (await GitAsync(root, "log", "--format=%s", "main..feature/0fda18-fix-the-gap")).Trim());
    }

    /// <summary>A cherry-pick the line changed the same file after: the walk cannot read it, its patch id can.</summary>
    [Fact]
    public async Task A_cherry_pick_is_left_out_by_its_patch_id_where_the_line_changed_its_file_since()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(tree.Path, "b.txt", "b\n", "the second part");
        var second = await HeadAsync(tree.Path);
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", second);
        var picked = await HeadAsync(root);
        await CommitAsync(root, "b.txt", "b\nand the line's own\n", "the line goes on with it");

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        var left = Assert.Single(landed.LeftOut!);
        Assert.Equal((second, LeftOutWhy.Patch, picked), (left.Sha, left.Why, left.On));
        Assert.Equal("the first part", (await GitAsync(root, "log", "--format=%s", "main..feature/0fda18-fix-the-gap")).Trim());
    }

    /// <summary>
    /// Beside a cherry-pick that makes the landing replay, a squash of several the walk cannot read (the line changed their file
    /// since) and no one patch matches: kept, replayed, and found empty there, which the answer says.
    /// </summary>
    [Fact]
    public async Task A_kept_commit_that_becomes_empty_when_replayed_is_dropped_and_said()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "long.txt", Lines(), "a long file");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "p.txt", "p\n", "a part picked");
        var picked = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "long.txt", Lines(three: "three, fixed"), "fix the third");
        await CommitAsync(tree.Path, "long.txt", Lines(three: "three, fixed", nine: "nine, fixed"), "fix the ninth");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", picked);
        await CommitAsync(root, "long.txt", Lines(three: "three, fixed", nine: "nine, fixed"), "both fixes (squashed)");
        await CommitAsync(root, "long.txt", Lines(three: "three, fixed", six: "six, the line's", nine: "nine, fixed"), "the line's own");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Equal(picked, Assert.Single(plan.LeftOut!).Sha);
        Assert.Equal(["fix the third", "fix the ninth", "the notes"], plan.Lands!.Select(commit => commit.Subject));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal([LeftOutWhy.Content, LeftOutWhy.Empty, LeftOutWhy.Empty], landed.LeftOut!.Select(commit => commit.Why));
        Assert.Contains("Became empty when replayed, their changes already on the line: ", landed.Message);
        Assert.Equal("the notes", (await GitAsync(root, "log", "--format=%s", "main..feature/0fda18-fix-the-gap")).Trim());
    }

    /// <summary>
    /// A kept commit that conflicts with the line: refused, nothing made, the tree and its branch as they were, no tree of Daoris's
    /// left, and the words that ask its session to rebase.
    /// </summary>
    [Fact]
    public async Task A_conflict_is_refused_naming_the_files_and_makes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "a.txt", "a\n", "the first part");
        var first = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "shared.txt", "one\nfrom the tree\n", "the second part");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", first);
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("shared.txt", plan.Conflict!.Says);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal(AutoLandingCode.Refused, landed.Refusal);
        Assert.Contains("CONFLICT (content): Merge conflict in shared.txt", landed.Conflict!.Says);
        Assert.StartsWith("Nothing was landed: replaying its own commits onto `main` conflicts with what the line changed since: CONFLICT", landed.Message);
        Assert.Contains("Rebase", landed.Conflict.Words);
        Assert.Contains("shared.txt", landed.Conflict.Words);
        Assert.Contains($"`{first[..8]}`", landed.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
        Assert.Empty(trees.Recorded.All());
    }

    /// <summary>The control: the line moved, but holds none of the tree's commits, so it lands exactly as before.</summary>
    [Fact]
    public async Task A_tree_whose_commits_the_line_holds_none_of_lands_as_before()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        var tip = await HeadAsync(tree.Path);
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");

        var plan = await trees.PlanAsync(tree.Path, Subject);
        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.Null(plan.LeftOut);
        Assert.Null(plan.Lands);
        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith("put the work on `feature/0fda18-fix-the-gap` — 1 commit(s) from `main`.", landed.Message);
        Assert.Null(landed.LeftOut);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "feature/0fda18-fix-the-gap")).Trim());
        Assert.Equal(tip, await HeadAsync(tree.Path));
    }

    /// <summary>The merge form merges the replay's tip into the line, and the tree follows it.</summary>
    [Fact]
    public async Task The_merge_form_merges_only_what_the_line_lacks()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge)).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await CommitAsync(root, "work.txt", "the work\nand the line's own\n", "the line's own");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("the notes", (await GitAsync(root, "log", "--format=%s", "main^1..main^2")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "main^2")).Trim(), await HeadAsync(tree.Path));
    }

    /// <summary>A fetch that fails is no refusal: the landing is judged against the last fetch, and says so in WSR7's words.</summary>
    [Fact]
    public async Task A_failed_fetch_is_said_and_the_landing_goes_on()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "remote", "set-url", "origin", Path.Combine(Scratch, "gone.git"));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("`origin` was not fetched (", landed.Message);
        Assert.Contains("so this was judged against what it said when it was last fetched here (last fetched", landed.Message);
    }

    private static string Lines(string three = "three", string six = "six", string nine = "nine") =>
        string.Join("\n", "one", "two", three, "four", "five", six, "seven", "eight", nine, "ten") + "\n";

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    /// <summary>How many working trees git lists for the checkout: a tree of Daoris's own made for a replay is gone after it.</summary>
    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>What <c>git merge-tree --write-tree</c> exits with: 0 for a merge with no conflict.</summary>
    private static async Task<int> MergeTreeAsync(string root, string ours, string theirs)
    {
        var info = new System.Diagnostics.ProcessStartInfo { FileName = "git", WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "merge-tree", "--write-tree", ours, theirs }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }

    /// <summary>What a refused landing must leave as it was, as <c>ContentLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
