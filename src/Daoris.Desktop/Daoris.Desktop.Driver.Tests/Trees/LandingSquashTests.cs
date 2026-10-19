using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PROCESS1b-ii (D164's amendment point 2 and its PROCESS1b-ii note): where the repository's landing rule asks for <c>squash</c>,
/// Accept makes the pull request's branch one commit, the first commit Daoris itself makes: the session's tip's tree, its parent
/// where the session left the line, its message the quest's title and the session's commits' subjects, made as git's own
/// identity in that repository and refused where git has none. The record keeps the session tip it squashed, which is what
/// proves the session landed: its head, Discard's offer and the clean-up read the session branch as landed while its tip is
/// that commit or an ancestor of it. A chain's later landing adds one commit squashing only what was added since. A tree on a
/// branch of its own lands as it stands. Real git, as <see cref="LandingTidyTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandingSquashTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private const string Named = "feature/0fda18-fix-the-gap";

    private readonly string _scratch;
    private readonly string _home;

    public LandingSquashTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-squash", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// One commit: the session's tree, its parent the commit the session grew from though the line moved on since, its message the
    /// title, each commit's subject and the session, its author git's own identity here; the record names the tip it squashed.
    /// </summary>
    [Fact]
    public async Task A_squash_lands_one_commit_of_the_sessions_tree_onto_where_it_left_the_line()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var grewFrom = (await GitAsync(root, "rev-parse", "main")).Trim();
        await CommitAsync(tree.Path, "first.txt", "the first step", "the first step");
        await CommitAsync(tree.Path, "second.txt", "the second step", "the second step");
        await CommitAsync(root, "line.txt", "the line moved on", "the line moved on");
        var sessionTip = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains($"put the work on `{Named}`", landed.Message);
        Assert.Contains("squashed", landed.Message);
        var made = (await GitAsync(root, "rev-parse", Named)).Trim();
        Assert.Equal(grewFrom, (await GitAsync(root, "rev-parse", $"{Named}^")).Trim());
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", $"{grewFrom}..{Named}")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", $"{sessionTip}^{{tree}}")).Trim(), (await GitAsync(root, "rev-parse", $"{Named}^{{tree}}")).Trim());
        Assert.Equal("Fix the gap\n\n- the first step\n- the second step\n\nSquashed by Daoris from session `s1`\n",
            (await GitAsync(root, "log", "-1", "--format=%B", Named)).Replace("\r\n", "\n").TrimEnd('\n') + "\n");
        Assert.Equal("Fixture <fixture@example.test>", (await GitAsync(root, "log", "-1", "--format=%an <%ae>", Named)).Trim());
        Assert.Equal("Fixture <fixture@example.test>", (await GitAsync(root, "log", "-1", "--format=%cn <%ce>", Named)).Trim());

        var record = trees.Recorded.Of("engine", Named);
        Assert.NotNull(record);
        Assert.Equal(made, record.Tip);
        Assert.Equal(sessionTip, record.Squashed);
        Assert.Equal(sessionTip, (await GitAsync(root, "rev-parse", tree.Branch)).Trim());
    }

    /// <summary>
    /// ACCEPTFOOT2b (D164's 2026-10-16 notes): a conversation's squash is subjected by its work, the oldest commit's subject beyond
    /// the line, never its opening; a quest's stays its title, under a pattern that reads words and one that does not.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_conversations_squash_is_subjected_by_its_work_and_a_quests_by_its_title(bool conversation)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{session}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "first.txt", "the first step", "Add the first step");
        await CommitAsync(tree.Path, "second.txt", "the second step", "Add the second step");
        var subject = conversation
            ? new LandingSubject("s1", null, "please could you fix the gap in the login screen")
            : new LandingSubject("s1", "0fda18", "Fix the gap");

        var landed = await trees.LandAsync(tree.Path, subject, inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var branch = (await GitAsync(root, "for-each-ref", "--format=%(refname:short)", "refs/heads/feature/")).Trim();
        Assert.DoesNotContain('\n', branch);
        var message = (await GitAsync(root, "log", "-1", "--format=%B", branch)).Replace("\r\n", "\n");
        Assert.Equal(conversation ? "Add the first step" : "Fix the gap", message.Split('\n')[0]);
        Assert.Contains("- Add the second step", message);
    }

    /// <summary>
    /// The record's squashed tip proves the session landed (point 3): its head counts nothing unlanded and offers no Discard of
    /// work held elsewhere, and its tree and branch go on an unforced removal, as a branch the landing made by ancestry lets them,
    /// keeping no recovery ref, since the record is Daoris's own word of what it squashed.
    /// </summary>
    [Fact]
    public async Task The_head_Discard_and_the_removal_read_a_squashed_session_as_landed()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work", "the work");
        Assert.Equal(1, (await trees.WorkAsync(tree.Path))!.Commits);

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var work = await trees.WorkAsync(tree.Path);
        Assert.Equal(0, work!.Commits);
        Assert.False(work.Holds);
        Assert.Null(await trees.DiscardOfferAsync(tree.Path));

        var plan = await trees.SweepPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        var row = Assert.Single(plan, item => item.Branch == tree.Branch);
        Assert.Equal(SweepKind.Landed, row.Kind);
        Assert.Equal(Named, row.Where);
        Assert.Null(row.HeldBy);

        var removed = await trees.RemoveAsync(tree.Path);
        Assert.True(removed.Removed, removed.Message);
        Assert.False(await BranchAsync(root, tree.Branch));
        Assert.Contains(Named, removed.Message);
        Assert.Empty((await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
        Assert.True(await BranchAsync(root, Named), "the pull request's branch stands");
    }

    /// <summary>
    /// A chain's earlier step, whose commits rode into the accepted one, is an ancestor of the squashed tip: the rule's tidy takes
    /// it with the pressed session, as it does where the branch holds the commits themselves (LAND3).
    /// </summary>
    [Fact]
    public async Task A_squashing_landings_tidy_takes_the_chains_earlier_branch_its_squash_holds()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Squash: true));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step", "the first step");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step", "the verify step");

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.False(await BranchAsync(root, second.Branch), "the pressed session's branch goes on the record's proof");
        Assert.False(Directory.Exists(second.Path));
        Assert.False(await BranchAsync(root, first.Branch), "the earlier step's branch the squash holds goes too");
        Assert.Equal(new TidiedBranch(first.Branch, Removed: true, Tree: true), Assert.Single(landed.Tidied!));
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", $"main..{Named}")).Trim());
    }

    /// <summary>
    /// A chain's later landing (point 4) squashes only what was added since the recorded tip: one more commit on the pull
    /// request's branch, its tree the new tip's, its parent the branch's tip, its body the new commits alone, and the record moved
    /// to the new tip. A session whose tip does not grow from the recorded one is refused, and nothing moves.
    /// </summary>
    [Fact]
    public async Task A_chains_later_landing_adds_one_commit_and_moves_the_record_on()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step", "the first step");
        var once = await trees.LandAsync(first.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);
        Assert.True(once.Landed, once.Message);
        var squashedOnce = (await GitAsync(root, "rev-parse", Named)).Trim();

        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step", "the verify step");
        await CommitAsync(second.Path, "third.txt", "the docs step", "the docs step");
        var secondTip = (await GitAsync(root, "rev-parse", second.Branch)).Trim();

        var grown = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(grown.Landed, grown.Message);
        Assert.Equal(squashedOnce, grown.AdvancedFrom);
        Assert.Contains($"moved `{Named}` on", grown.Message);
        var made = (await GitAsync(root, "rev-parse", Named)).Trim();
        Assert.Equal(squashedOnce, (await GitAsync(root, "rev-parse", $"{Named}^")).Trim());
        Assert.Equal("2", (await GitAsync(root, "rev-list", "--count", $"main..{Named}")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", $"{secondTip}^{{tree}}")).Trim(), (await GitAsync(root, "rev-parse", $"{Named}^{{tree}}")).Trim());
        Assert.Equal("Fix the gap\n\n- the verify step\n- the docs step\n\nSquashed by Daoris from session `s2`\n",
            (await GitAsync(root, "log", "-1", "--format=%B", Named)).Replace("\r\n", "\n").TrimEnd('\n') + "\n");
        var record = trees.Recorded.Of("engine", Named)!;
        Assert.Equal(made, record.Tip);
        Assert.Equal(secondTip, record.Squashed);
        var advance = Assert.Single(record.Advances);
        Assert.Equal((squashedOnce, made, secondTip), (advance.From, advance.To, advance.Squashed));
        Assert.Equal(0, (await trees.WorkAsync(first.Path))!.Commits);
        Assert.Equal(0, (await trees.WorkAsync(second.Path))!.Commits);

        // Grown from the line, not from the chain: no squash of it would be what the pull request's branch already says.
        var stray = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(stray.Path, "stray.txt", "a stray step", "a stray step");
        var refused = await trees.LandAsync(stray.Path, new LandingSubject("s3", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("does not grow from", refused.Message);
        Assert.Equal(made, (await GitAsync(root, "rev-parse", Named)).Trim());
        Assert.Equal(secondTip, trees.Recorded.Of("engine", Named)!.Squashed);
    }

    /// <summary>
    /// Where git has no identity in the repository, Accept is refused before anything is made, saying how to set one or turn the
    /// squash off. A repository's own empty <c>user.name</c> and <c>user.email</c> stand in for none, whatever the machine's are.
    /// </summary>
    [Fact]
    public async Task Accept_is_refused_where_git_has_no_identity_and_nothing_is_made()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work", "the work");
        await GitAsync(root, "config", "user.name", "");
        await GitAsync(root, "config", "user.email", "");

        var refused = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("git config user.name", refused.Message);
        Assert.Contains("git config user.email", refused.Message);
        Assert.Contains("daoris driver landing", refused.Message);
        Assert.False(await BranchAsync(root, Named));
        Assert.Empty(trees.Recorded.All());
        Assert.Equal(1, (await trees.WorkAsync(tree.Path))!.Commits);
    }

    /// <summary>A tree its agent moved onto a branch of its own (TREEFOLLOW1) lands as that branch stands: never squashed.</summary>
    [Fact]
    public async Task A_tree_on_its_own_branch_lands_as_it_stands_and_is_never_squashed()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "-b", "feature/TK-1-own");
        await CommitAsync(tree.Path, "first.txt", "the first step", "the first step");
        await CommitAsync(tree.Path, "second.txt", "the second step", "the second step");
        var own = (await GitAsync(root, "rev-parse", "feature/TK-1-own")).Trim();

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/TK-1-own", landed.Branch);
        Assert.Equal(own, (await GitAsync(root, "rev-parse", "feature/TK-1-own")).Trim());
        Assert.Equal("2", (await GitAsync(root, "rev-list", "--count", "main..feature/TK-1-own")).Trim());
        Assert.False(await BranchAsync(root, Named));
        Assert.Null(trees.Recorded.Of("engine", "feature/TK-1-own")?.Squashed);
    }

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>A copy of the run's seed of this shape (<see cref="GitSeed"/>; PROCSPEED1): no test here reads the first commit's id or time.</summary>
    private async Task<string> RepositoryAsync(string name)
    {
        var seed = await GitSeed.SeedAsync($"landing-squash:{name}", folder => BuildRepositoryAsync(Path.Combine(folder, name), name));
        var root = Path.Combine(_scratch, name);
        GitSeed.Copy(Path.Combine(seed, name), root);
        return root;
    }

    private static async Task BuildRepositoryAsync(string root, string name)
    {
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
