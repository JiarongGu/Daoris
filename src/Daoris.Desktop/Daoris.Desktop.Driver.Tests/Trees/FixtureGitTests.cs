using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git fixtures' own git (PROCSPEED1b), over real git: the plain repository most git classes copy is the one its build made; a
/// seed's git starts no second git behind a commit or a push, on either side (<see cref="GitSeed.NoMaintenanceAsync"/>); and the
/// platform's moves on a bare origin (<see cref="LandedFixture"/>'s squash and its commit) leave it as a clone that committed and pushed
/// would, with no clone. <see cref="GitSeedTests"/> holds what a copy keeps and mends with no git. Git's own trace counts the starts,
/// handed to the one git a case runs.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FixtureGitTests : LandedFixture
{
    [Fact]
    public async Task A_plain_copy_is_on_main_with_its_first_commit_its_readme_and_its_identity()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");

        Assert.Equal("main", (await GitAsync(root, "symbolic-ref", "--short", "HEAD")).Trim());
        Assert.Equal("first", (await GitAsync(root, "log", "--format=%s")).Trim());
        Assert.Equal("# engine\n", await File.ReadAllTextAsync(Path.Combine(root, "README.md")));
        Assert.Equal("", (await GitAsync(root, "status", "--porcelain")).Trim());
        Assert.Equal("Fixture <fixture@example.test>", (await GitAsync(root, "log", "-1", "--format=%an <%ae>")).Trim());
    }

    /// <summary>A commit in a plain copy is one git: none looks for maintenance after it.</summary>
    [Fact]
    public async Task A_commit_in_a_plain_copy_starts_no_second_git()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");

        var started = await StartedAsync(root, "commit", "--quiet", "--allow-empty", "-m", "more");

        Assert.Equal(["commit --quiet --allow-empty -m more"], started);
    }

    /// <summary>A landed fixture's copy and its bare origin: a commit, then its push, start no maintenance on either side.</summary>
    [Fact]
    public async Task A_commit_and_its_push_in_a_landed_copy_start_no_maintenance_on_either_side()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");

        var committed = await StartedAsync(root, "commit", "--quiet", "--allow-empty", "-m", "more");
        var pushed = await StartedAsync(root, "push", "--quiet", "origin", "main");

        Assert.Equal(["commit --quiet --allow-empty -m more"], committed);
        Assert.DoesNotContain(pushed, each => each.Contains("maintenance", StringComparison.Ordinal) || each.StartsWith("gc", StringComparison.Ordinal));
        // The receiving side ran, named by its repository: the trace saw both sides of the push.
        Assert.Contains(pushed, each => each.Contains(Path.GetFileName(origin), StringComparison.Ordinal));
    }

    /// <summary>
    /// The platform's squash: origin's <c>main</c> one commit on, its parent the line it found and its tree the branch merged onto it,
    /// named as the platform names it; the branch gone there; and no clone made for it.
    /// </summary>
    [Fact]
    public async Task The_platforms_squash_moves_origins_line_one_commit_on_and_deletes_the_branch_with_no_clone()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var line = (await GitAsync(origin, "rev-parse", "main")).Trim();
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(root, "x.txt", "x\n", "x one");
        await CommitAsync(root, "shared.txt", "one\nx\n", "x two");
        await GitAsync(root, "push", "--quiet", "origin", "feature/x");

        var squash = await SquashOnPlatformAsync(origin, "feature/x");

        Assert.Equal(squash, (await GitAsync(origin, "rev-parse", "main")).Trim());
        Assert.Equal(line, (await GitAsync(origin, "rev-parse", "main^")).Trim());
        Assert.Equal("", (await GitAsync(origin, "rev-parse", "--verify", "--quiet", "main^2")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "feature/x^{tree}")).Trim(), (await GitAsync(origin, "rev-parse", "main^{tree}")).Trim());
        Assert.Equal("Squashed (#7)|Fixture <fixture@example.test>", (await GitAsync(origin, "log", "-1", "--format=%s|%an <%ae>", "main")).Trim());
        Assert.Equal("", (await GitAsync(origin, "branch", "--list", "feature/x")).Trim());
        Assert.Empty(Directory.EnumerateDirectories(Scratch, "platform-*"));
    }

    /// <summary>The squash kept on its branch, and the line moving on after it: one more commit changing a file the squash brought.</summary>
    [Fact]
    public async Task The_platforms_squash_then_the_line_moving_on_is_two_commits_and_the_branch_stays_where_asked()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(root, "x.txt", "x\n", "x one");
        await GitAsync(root, "push", "--quiet", "origin", "feature/x");
        var branch = (await GitAsync(origin, "rev-parse", "feature/x")).Trim();

        var squash = await SquashOnPlatformAsync(origin, "feature/x", deleteBranch: false, then: ("x.txt", "x, changed on the line since\n"));

        Assert.Equal(squash, (await GitAsync(origin, "rev-parse", "main^")).Trim());
        Assert.Equal("the line moves on", (await GitAsync(origin, "log", "-1", "--format=%s", "main")).Trim());
        Assert.Equal("x, changed on the line since\n", await GitAsync(origin, "show", "main:x.txt"));
        Assert.Equal("x\n", await GitAsync(origin, "show", $"{squash}:x.txt"));
        Assert.Equal(["README.md", "shared.txt", "x.txt"], (await GitAsync(origin, "ls-tree", "--name-only", "main")).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(branch, (await GitAsync(origin, "rev-parse", "feature/x")).Trim());
    }

    /// <summary>Someone else's commit on origin's line: one commit with the files written at its top and the rest as they were, no clone.</summary>
    [Fact]
    public async Task A_commit_on_origins_line_is_one_commit_of_its_files_with_no_clone()
    {
        var (_, origin) = await RepositoryWithOriginAsync("engine");
        var line = (await GitAsync(origin, "rev-parse", "main")).Trim();

        await CommitOnOriginAsync(origin, "the line moves on", ("shared.txt", "one\ntheirs\n"), ("later.txt", "later\n"));

        Assert.Equal(line, (await GitAsync(origin, "rev-parse", "main^")).Trim());
        Assert.Equal("the line moves on|Fixture <fixture@example.test>", (await GitAsync(origin, "log", "-1", "--format=%s|%an <%ae>", "main")).Trim());
        Assert.Equal("one\ntheirs\n", await GitAsync(origin, "show", "main:shared.txt"));
        Assert.Equal("later\n", await GitAsync(origin, "show", "main:later.txt"));
        Assert.Equal("# engine\n", await GitAsync(origin, "show", "main:README.md"));
        Assert.Empty(Directory.EnumerateDirectories(Scratch, "platform-*"));
    }

    /// <summary>The arguments of every git that ran for one start in <paramref name="cwd"/>: itself, then each it started, by git's own trace.</summary>
    private async Task<IReadOnlyList<string>> StartedAsync(string cwd, params string[] arguments)
    {
        var trace = Path.Combine(Scratch, $"trace-{Guid.NewGuid():N}"[..14] + ".jsonl");
        var run = await GitFixture.RunWithAsync(cwd, new Dictionary<string, string> { ["GIT_TRACE2_EVENT"] = trace }, null, arguments);
        Assert.True(run.ExitCode == 0, run.Stderr);
        return
        [
            .. File.ReadLines(trace)
                .Select(line => JsonDocument.Parse(line).RootElement)
                .Where(each => each.GetProperty("event").GetString() == "start")
                .Select(each => string.Join(' ', each.GetProperty("argv").EnumerateArray().Skip(1).Select(argument => argument.GetString()))),
        ];
    }
}
