using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR5a: a branch a landing made, whose pull request reached the line, goes by the same press as a
/// session's branch (D88 extended). A squash merge puts none of its commits on the line, so git calls it
/// unmerged; the proof is by content — every file it changed reads on the line as it left it — or by
/// ancestry into another landed branch that proof clears. Only branches a landing made and recorded are
/// judged. Real git throughout, in scratch repositories; the proof's cases are <see cref="LandedProofTests"/>
/// and what keeps a branch is <see cref="LandedKeepTests"/>.
/// </summary>
public sealed class LandedBranchTests : LandedFixture
{
    // ——— the record

    /// <summary>A landing records the branch it made — which repository, from which line, at which tip, for whom — under the home.</summary>
    [Fact]
    public async Task A_branch_landing_records_the_branch_it_made_under_the_home()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);

        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

        var entry = Assert.Single(new LandedBranches(Home).All());
        Assert.Equal("engine", entry.Repository);
        Assert.Equal("aurora", entry.Workspace);
        Assert.Equal(landed.Branch, entry.Branch);
        Assert.Equal("feature/0fda18-fix-the-api-gap", entry.Branch);
        Assert.Equal("main", entry.Line);
        Assert.Equal((await GitAsync(root, "rev-parse", entry.Branch)).Trim(), entry.Tip);
        Assert.Equal("s1a2b3c4", entry.Session);
        Assert.Equal("0fda18", entry.Quest);
        Assert.Equal("Fix the API gap", entry.Title);
        Assert.False(entry.Pushed);
        Assert.Null(entry.PullRequest);
        // Machine-local, under the home (D63) — never in the repository.
        Assert.True(File.Exists(Path.Combine(Home, LandedBranches.FileName)));
        Assert.Empty((await GitAsync(root, "status", "--porcelain")).Trim());
    }

    /// <summary>Only a branch a landing made is recorded: a merge makes none, and a refused landing made nothing.</summary>
    [Fact]
    public async Task A_merge_and_a_refused_landing_record_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var refused = await trees.OpenAsync(root, "engine", "aurora");
        Assert.False((await trees.LandAsync(refused.Path, new LandingSubject("s1", "q1", "Nothing"))).Landed);

        DriverConfig.Empty.Save(Path.Combine(Home, "driver.json"));
        var merged = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(merged.Path, "work.txt", "work\n", "the work");
        Assert.True((await trees.LandAsync(merged.Path, new LandingSubject("s2", "q2", "Merged"))).Landed);

        Assert.Empty(new LandedBranches(Home).All());
    }

    /// <summary>A file that does not read is no record at all — never a failure of the landing, and never a branch judged.</summary>
    [Fact]
    public void A_record_that_does_not_read_is_empty()
    {
        File.WriteAllText(Path.Combine(Home, LandedBranches.FileName), "{ not json");

        Assert.Empty(new LandedBranches(Home).All());
    }

    // ——— the real case

    /// <summary>
    /// The first real ticket's case: a landing made A; a later landing made B from a tree holding A's
    /// commits; B's pull request was squash-merged onto the line. Git calls both unmerged. B's files read
    /// on the line as it left them, and A is inside B — so both go, and a branch of the person's never
    /// is judged at all.
    /// </summary>
    [Fact]
    public async Task A_squash_merged_branch_and_the_landed_branch_inside_it_both_go_and_the_persons_own_stays()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var a = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"), extra: ("a.txt", "a\n"));
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\nthree\n", new LandingSubject("s2", "q2", "Second"), from: a.Branch, extra: ("b.txt", "b\n"));
        await GitAsync(root, "merge", "--squash", b.Branch!);
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "Second (#7)");
        // The person's own branch, with work of its own the line does not hold.
        await GitAsync(root, "branch", "feature/other", "main");
        await CommitOnAsync(root, "feature/other", "other.txt", "mine\n", "my own work");
        // The premise: git's own `branch -d` would refuse both.
        var merged = await GitAsync(root, "branch", "--merged", "main");
        Assert.DoesNotContain(a.Branch!, merged);
        Assert.DoesNotContain(b.Branch!, merged);
        var line = (await GitAsync(root, "rev-parse", "main")).Trim();

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(LandedKind.OnLine, Landed(plan, b.Branch!).Kind);
        Assert.Equal(LandedKind.Inside, Landed(plan, a.Branch!).Kind);
        Assert.Equal(b.Branch, Landed(plan, a.Branch!).Where);
        Assert.DoesNotContain(plan.Landed, item => item.Branch == "feature/other");

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(2, done.Landed.Count(result => result.Removed));
        var branches = await GitAsync(root, "branch", "--list");
        Assert.DoesNotContain(a.Branch!, branches);
        Assert.DoesNotContain(b.Branch!, branches);
        Assert.Contains("feature/other", branches);
        // Nothing else moved: the line, the checkout, the working tree.
        Assert.Equal(line, (await GitAsync(root, "rev-parse", "main")).Trim());
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Empty((await GitAsync(root, "status", "--porcelain")).Trim());
        // Forgotten once gone: the record holds what still stands.
        Assert.Empty(new LandedBranches(Home).All());
    }

    /// <summary>
    /// The line in its remote-tracking form: the platform squash-merged the pull request on `origin` and
    /// deleted the branch there, and this checkout's own `main` is behind — `origin/main` holds the work.
    /// </summary>
    [Fact]
    public async Task A_branch_squash_merged_on_the_remote_goes_while_the_local_line_is_behind()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await GitAsync(root, "push", "--quiet", "-u", "origin", b.Branch!);
        await SquashOnPlatformAsync(origin, b.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        Assert.NotEqual((await GitAsync(root, "rev-parse", "main")).Trim(), (await GitAsync(root, "rev-parse", "origin/main")).Trim());

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        var item = Landed(plan, b.Branch!);
        Assert.Equal(LandedKind.OnLine, item.Kind);
        Assert.Equal("origin/main", item.Where);
    }

    /// <summary>What each landed branch holds, in the terminal's words — the same sentence the screen's row says.</summary>
    [Fact]
    public void Each_kind_is_said_in_words()
    {
        LandedItem Item(string kind, string? where = null, IReadOnlyList<string>? files = null, int commits = 0, string? detail = null) =>
            new("engine", "aurora", "feature/x", kind, where, files ?? [], detail, null, commits);

        Assert.Contains("reached `origin/main`", LandedWords.Describe(Item(LandedKind.OnLine, "origin/main")));
        Assert.Contains("merged into `main`", LandedWords.Describe(Item(LandedKind.Merged, "main")));
        Assert.Contains("inside `feature/y`", LandedWords.Describe(Item(LandedKind.Inside, "feature/y")));
        Assert.Contains("1 file still differs on the line: shared.txt", LandedWords.Describe(Item(LandedKind.Differs, "main", ["shared.txt"])));
        Assert.Contains("5 files still differ on the line: a, b, c and 2 more", LandedWords.Describe(Item(LandedKind.Differs, "main", ["a", "b", "c", "d", "e"])));
        Assert.Contains("checked out", LandedWords.Describe(Item(LandedKind.CheckedOut)));
        Assert.Contains("2 commit(s) its remote does not have", LandedWords.Describe(Item(LandedKind.AheadOfRemote, commits: 2)));
        Assert.Contains("`daoris/s-1` still leans on it", LandedWords.Describe(Item(LandedKind.LeanedOn, "daoris/s-1")));
        Assert.Contains("git could not tell: no line", LandedWords.Describe(Item(LandedKind.Unknown, detail: "no line")));
    }
}
