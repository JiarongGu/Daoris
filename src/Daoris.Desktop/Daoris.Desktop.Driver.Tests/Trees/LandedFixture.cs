using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The scratch a landed branch's tests share (WSR5): a home whose `engine` lands on branches named
/// <c>feature/{quest}-{slug}</c>, repositories made in a scratch folder, and the moves a platform makes —
/// a squash merge onto the line, a branch deleted on the remote. Split over several classes so they run
/// side by side: each test spawns git dozens of times.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network.</b> Where a test needs `origin`, it is a bare repository under
/// the scratch folder, and a "platform" that squash-merges is a second clone of it.
/// </remarks>
public abstract class LandedFixture : IDisposable
{
    protected readonly string Scratch;
    protected readonly string Home;

    protected LandedFixture()
    {
        Scratch = Path.Combine(RepoRoot(), "_fixtures", "landed", Guid.NewGuid().ToString("N")[..8]);
        Home = Path.Combine(Scratch, "daoris-home");
        Directory.CreateDirectory(Home);
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
    }

    public void Dispose()
    {
        // A plugin's process, where a test ran one, stands in its folder a moment after it is told to go (FLAKE1). Git's read-only
        // objects are made writable first (PROCSPEED1): refused, every retry fails and the folder stays.
        for (var attempt = 0; Directory.Exists(Scratch); attempt++)
        {
            try
            {
                GitSeed.Remove(Scratch);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }

        GC.SuppressFinalize(this);
    }

    protected static LandedItem Landed(SweepPlan plan, string branch) => plan.Landed.Single(item => item.Branch == branch);

    /// <summary>A session's tree, one commit, and the press — the branch form, so a landing makes and records a branch.</summary>
    protected static async Task<TreeLanding> LandAsync(
        SessionTrees trees, string root, string file, string content, LandingSubject subject, string? from = null,
        (string File, string Content)? extra = null)
    {
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: from);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, file), content);
        if (extra is { } more) await File.WriteAllTextAsync(Path.Combine(tree.Path, more.File), more.Content);
        await GitAsync(tree.Path, "add", "-A");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", subject.Title ?? "work");
        var landed = await trees.LandAsync(tree.Path, subject);
        Assert.True(landed.Landed, landed.Message);
        return landed;
    }

    /// <summary>
    /// The records as an install from before FABRIC-END2 left them: no session branch names its unit, and no landing the units it carries
    /// (FABRIC-END2d). Both files are read and written whole, every other key as it was.
    /// </summary>
    protected void ForgetUnits()
    {
        foreach (var (entry, key) in new[] { (HomeEntry.SessionBranches, "unit"), (HomeEntry.Landings, "units") })
        {
            var path = DaorisHome.Entry(Home, entry);
            if (!File.Exists(path)) continue;
            var file = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach (var each in file["branches"]?.AsArray() ?? []) each?.AsObject().Remove(key);
            File.WriteAllText(path, file.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>
    /// A squashing landing from before FABRIC-END2 (the review of FABRIC-END4a's older squash record): a session's tree with one commit, landed
    /// under a squash rule, its landing branch merged onto the line, and the line then changing the session's file again, so the line's
    /// ancestry does not hold the session's own commit and its file differs on the line. The squash commit the line holds has the session's
    /// own tree, which the content proof reads (SQUASHTIDY1, the commit's tree). The units are forgotten.
    /// </summary>
    /// <returns>The session's tree, its branch, and the branch the landing made.</returns>
    protected async Task<(string Tree, string Branch, string Landing)> OlderSquashAsync(SessionTrees trees, string root)
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the work\n", "the work");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1", "q1", "Fix the gap"));
        Assert.True(landed.Landed, landed.Message);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {landed.Branch}", landed.Branch!);
        await CommitAsync(root, "work.txt", "the work, changed again\n", "the line changes it again");
        ForgetUnits();
        return (opened.Path, opened.Branch, landed.Branch!);
    }

    /// <summary>
    /// The first real ticket's case (<c>LandedBranchTests</c>): a landing made A; a later landing made B from A's branch; B was squash-merged
    /// onto the line, so B's files read there and A's shared file does not, and A is inside B.
    /// </summary>
    /// <returns>Both landings, and the first session's tree and branch, whose commit only A and B hold.</returns>
    protected static async Task<(TreeLanding A, TreeLanding B, string Tree, string Branch)> InsideLandingAsync(SessionTrees trees, string root)
    {
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(first.Path, "shared.txt"), "one\ntwo\n");
        await CommitAsync(first.Path, "a.txt", "a\n", "First");
        var a = await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "First"));
        Assert.True(a.Landed, a.Message);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\nthree\n", new LandingSubject("s2", "q2", "Second"), from: a.Branch, extra: ("b.txt", "b\n"));
        await SquashAsync(root, b.Branch!);
        return (a, b, first.Path, first.Branch);
    }

    /// <summary>A squash merge onto the checkout's own line, as a platform's "squash and merge" leaves it.</summary>
    protected static async Task SquashAsync(string root, string branch)
    {
        await GitAsync(root, "merge", "--squash", branch);
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", $"{branch} (squashed)");
    }

    /// <summary>
    /// The platform's side: a clone of the bare origin squash-merges the branch onto `main`, pushes, and deletes the branch there.
    /// <paramref name="then"/> is a later change on `main` after it (the line moving on, PLUGHOOK1a), pushed with it.
    /// </summary>
    /// <returns>The squash commit: what a platform answers as the pull request's merge commit.</returns>
    protected async Task<string> SquashOnPlatformAsync(
        string origin, string branch, bool deleteBranch = true, (string File, string Content)? then = null)
    {
        var platform = Path.Combine(Scratch, $"platform-{Guid.NewGuid():N}"[..20]);
        await GitAsync(Scratch, "clone", "--quiet", origin, platform);
        await GitAsync(platform, "merge", "--squash", $"origin/{branch}");
        await GitAsync(platform, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "Squashed (#7)");
        var squash = (await GitAsync(platform, "rev-parse", "HEAD")).Trim();
        if (then is { } change) await CommitAsync(platform, change.File, change.Content, "the line moves on");
        await GitAsync(platform, "push", "--quiet", "origin", "main");
        if (deleteBranch) await GitAsync(platform, "push", "--quiet", "origin", "--delete", branch);
        return squash;
    }

    /// <summary>
    /// A person's branch <paramref name="onto"/>, made from the line, that takes <paramref name="branch"/>'s commits by cherry-pick,
    /// then the checkout back on the line. 🔴 Each copy is a commit of its own (AUTOTIDY1r): a cherry-pick whose parent is the
    /// original's, by the same committer in the same second, writes the very same commit, and the person's branch then holds the
    /// session's work by ancestry, never by content. <c>-x</c> names the original in the message, so the copy's id always differs.
    /// </summary>
    protected static async Task CherryPickAsync(string root, string branch, string onto)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", onto);
        await GitAsync(root, "cherry-pick", "-x", $"main..{branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
        var (original, copy) = ((await GitAsync(root, "rev-parse", branch)).Trim(), (await GitAsync(root, "rev-parse", onto)).Trim());
        Assert.True(original != copy, $"the cherry-pick onto {onto} made a commit of its own, not {branch}'s own {original}");
    }

    /// <summary>A commit on a branch nobody has checked out, through a worktree made and removed for it.</summary>
    protected async Task CommitOnAsync(string root, string branch, string file, string content, string message)
    {
        var place = Path.Combine(Scratch, $"on-{Guid.NewGuid():N}"[..12]);
        await GitAsync(root, "worktree", "add", "--quiet", place, branch);
        await CommitAsync(place, file, content, message);
        await GitAsync(root, "worktree", "remove", place);
    }

    /// <summary>
    /// A repository on `main` with one commit, README and shared.txt: a copy of the run's seed of that name (<see cref="GitSeed"/>, PROC1h),
    /// whose commit is the seed's own, so a test that reads a fresh repository's exact history builds its own (<see cref="BuildRepositoryAsync"/>).
    /// </summary>
    protected async Task<string> RepositoryAsync(string name)
    {
        var seed = await GitSeed.SeedAsync($"repository:{name}", async folder => await BuildRepositoryAsync(Path.Combine(folder, name), name));
        var root = Path.Combine(Scratch, name);
        GitSeed.Copy(Path.Combine(seed, name), root);
        return root;
    }

    /// <summary>The same repository made step by step, for a test whose assertions read its commit ids or times.</summary>
    protected async Task<string> BuildRepositoryAsync(string name) => await BuildRepositoryAsync(Path.Combine(Scratch, name), name);

    private static async Task<string> BuildRepositoryAsync(string root, string name)
    {
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), $"# {name}\n");
        await CommitAsync(root, "shared.txt", "one\n", "first");
        return root;
    }

    /// <summary>
    /// A repository whose `origin` is a bare repository beside it — somewhere a push lands without leaving the machine. A copy of the run's
    /// seed of that name with its origin, whose url is repaired to the copy's own bare repository (PROC1h).
    /// </summary>
    protected async Task<(string Root, string Origin)> RepositoryWithOriginAsync(string name)
    {
        var seed = await GitSeed.SeedAsync($"origin:{name}", async folder =>
        {
            var seeded = Path.Combine(folder, $"{name}-origin.git");
            Directory.CreateDirectory(seeded);
            await GitAsync(seeded, "init", "--quiet", "--bare", "-b", "main");
            var repository = await BuildRepositoryAsync(Path.Combine(folder, name), name);
            await GitAsync(repository, "remote", "add", "origin", seeded);
            await GitAsync(repository, "push", "--quiet", "-u", "origin", "main");
        });
        var origin = Path.Combine(Scratch, $"{name}-origin.git");
        var root = Path.Combine(Scratch, name);
        GitSeed.Copy(Path.Combine(seed, $"{name}-origin.git"), origin);
        GitSeed.Copy(Path.Combine(seed, name), root);
        GitSeed.RepairOrigin(root, origin);
        return (root, origin);
    }

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    protected static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        var (beganUtc, began) = (DateTime.UtcNow, Stopwatch.GetTimestamp());
        using var process = Process.Start(TestGit.Past(info))!;
        var started = Stopwatch.GetTimestamp();
        // Both reads start before the wait: a child that fills the pipe nobody reads waits on it forever (TESTGIT1).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stderr;
        if (FixtureSpawns.On) FixtureSpawns.Note("LandedFixture", beganUtc, began, started, arguments);
        return await stdout;
    }

    /// <summary>
    /// git run at once, for a person acting between two of Daoris's own git starts (<see cref="WorkingTree.Started"/>), which is
    /// synchronous; both pipes read before the wait, as <see cref="GitAsync"/>'s are.
    /// </summary>
    protected static void GitNow(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo { FileName = "git", WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        stderr.GetAwaiter().GetResult();
        process.WaitForExit();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
