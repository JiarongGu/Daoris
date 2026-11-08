using Daoris.Driver;
using Microsoft.Data.Sqlite;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch home and a repository for the pool's acts over real git: a project file (so build folders are classified), a tracked
/// source, a <c>.gitignore</c> for <c>obj/</c>, <c>_fixtures/</c> and <c>*.log</c>, and pools that stand for processes of their own.
/// </summary>
internal sealed class PoolGitFixture : IDisposable
{
    internal const string Workspace = "w";

    internal const string Repository = "engine";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-pool-git", Guid.NewGuid().ToString("N")[..8]);

    private PoolGitFixture()
    {
        Home = Path.Combine(_scratch, "home");
        Root = Path.Combine(_scratch, Repository);
        Directory.CreateDirectory(Home);
        Site = new PoolSite(Home, Root, Workspace, Repository);
    }

    internal string Home { get; }

    internal string Root { get; }

    internal PoolSite Site { get; }

    /// <summary>The commit the repository's first line stands at.</summary>
    internal string Start { get; set; } = "";

    /// <param name="rules">The repository's own rule file, tracked from the first commit; null tracks none.</param>
    internal static async Task<PoolGitFixture> CreateAsync(string? rules = null)
    {
        var fixture = new PoolGitFixture();
        var root = fixture.Root;
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await File.WriteAllTextAsync(Path.Combine(root, "Engine.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Engine.cs"), "class Engine {}\n");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "obj/\n_fixtures/\n*.log\n");
        if (rules is not null) await File.WriteAllTextAsync(Path.Combine(root, "daoris.files.json"), rules);
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "--quiet", "-m", "first");
        fixture.Start = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        return fixture;
    }

    /// <summary>A pool standing for a process of its own; <paramref name="alive"/> says which runs live.</summary>
    internal TreePool Pool(string run = "run-a", Func<StoreRun, bool>? alive = null, PoolSeams? seams = null) =>
        new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell) { Seams = seams ?? Landing() };

    internal static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    /// <summary>Seams whose keyed open lands, answering a record named for the line.</summary>
    internal static PoolSeams Landing() => new() { Open = (claim, _) => Task.FromResult<string?>($"record-{claim.Line}") };

    internal PoolMake Make(int number, string line, string session = "") => new(Site, number, "quest-1", line, session);

    internal PoolTake Take(int number, string line, string? start = null, string session = "") =>
        new(Site, number, "quest-2", line, start ?? Start, false, "main", null, session);

    internal async Task<KeptTree> MadeAsync(TreePool pool, int number, string line)
    {
        var done = await pool.MakeAsync(Make(number, line));
        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        return done.Slot!;
    }

    /// <summary>A slot made for <paramref name="line"/>, the line's commit made in it, and the line let go.</summary>
    internal async Task<KeptTree> LetGoAsync(TreePool pool, int number, string line, string file = "work.txt")
    {
        var slot = await MadeAsync(pool, number, line);
        await File.WriteAllTextAsync(Path.Combine(slot.Path, file), $"{line}\n");
        await GitAsync(slot.Path, "add", file);
        await GitAsync(slot.Path, "commit", "--quiet", "-m", line);
        var ended = await pool.EndAsync(new PoolEnd(Site, number, "quest-1", "session-1"));
        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
        return ended.Slot!;
    }

    internal Task<string> BranchesAsync() => GitAsync(Root, "branch", "--format=%(refname:short)");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }
}
