using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch home and a repository kept in memory for the pool's acts: the repository's commits, refs, working trees and files are a
/// <see cref="MemoryRepository"/>, so no git starts, and each slot's folder is an empty one on disk, which is all the pool's own disk
/// reads (links, repositories inside, the files record) need. A flow <see cref="Use"/>s it before it asks the pool anything.
/// </summary>
internal sealed class PoolMemoryFixture : IDisposable
{
    internal const string Workspace = "w";

    internal const string Repository = "engine";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-pool-memory", Guid.NewGuid().ToString("N")[..8]);

    internal PoolMemoryFixture()
    {
        Home = Path.Combine(_scratch, "home");
        Root = Path.Combine(_scratch, Repository);
        Directory.CreateDirectory(Home);
        Site = new PoolSite(Home, Root, Workspace, Repository);
        Repo = new MemoryRepository(Root);
        Repo.Write(Root, "README.md", "# engine\n");
        Repo.Write(Root, "Engine.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Repo.Write(Root, "src/Engine.cs", "class Engine {}\n");
        Repo.Write(Root, ".gitignore", "obj/\n_fixtures/\n*.log\n");
        Repo.CommitAllAsync(Root, "first", CancellationToken.None).GetAwaiter().GetResult();
        Start = Repo.HeadAsync(Root, CancellationToken.None).GetAwaiter().GetResult()!;
    }

    internal string Home { get; }

    internal string Root { get; }

    internal PoolSite Site { get; }

    internal MemoryRepository Repo { get; }

    /// <summary>The commit the repository's first line stands at.</summary>
    internal string Start { get; }

    /// <summary>Makes the memory repository this flow's, with a git that fails any ask a port did not answer, until disposed.</summary>
    internal IDisposable Use() => new Chosen(FlowRepository.Use(Repo), FlowGit.Use(new ScriptedGit()));

    internal TreePool Pool(string run = "run-a", Func<StoreRun, bool>? alive = null, PoolSeams? seams = null) =>
        new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell) { Seams = seams ?? PoolGitFixture.Landing() };

    internal PoolTake Take(int number, string line, string session = "") => new(Site, number, "quest-2", line, Start, false, "main", null, session);

    /// <summary>
    /// A slot made as an open leaves it, a line's commit made in it and the line let go as the pool records it: the slot is a folder, a
    /// working tree of the repository locked as the pool locks a free slot, on the line's branch at its tip, and a row and a let-go name it.
    /// </summary>
    internal async Task<KeptTree> LetGoAsync(TreePool pool, int number, string line, string file = "work.txt")
    {
        var folder = Site.FolderOf(number);
        Directory.CreateDirectory(folder);
        var ct = CancellationToken.None;
        Assert.True((await Repo.AddWorktreeAsync(Root, folder, Start, null, ct)).Ok);
        Assert.True((await Repo.SwitchAsync(folder, new GitSwitch($"{TreeFollows.SessionPrefix}{line}", Start, Creates: true), ct)).Ok);
        Repo.Write(folder, file, $"{line}\n");
        Assert.True((await Repo.CommitAllAsync(folder, line, ct)).Ok);
        Assert.True((await Repo.LockAsync(Root, folder, PoolLocks.Free, ct)).Ok);
        var tip = (await Repo.HeadAsync(folder, ct))!;

        var claimed = pool.Claim(new TreeClaimOpening(Workspace, Repository, number, TreeClaimKinds.Make, "quest-1", "", "", "", "", PoolGit.NewJobName()) { Phase = TreeClaimPhases.Opening });
        pool.Complete(claimed.Fence!, new SlotSettled($"k-{number}", folder, line, "", KeptStates.Kept, "", 0, ""));
        var now = DateTimeOffset.UtcNow;
        pool.RecordEnd(new LineEnd(Workspace, Repository, line, $"{TreeFollows.SessionPrefix}{line}", "session-1", number, LineEndKinds.Output, tip, now, "", "", now));
        return pool.Slot(Workspace, Repository, number)!;
    }

    internal async Task<IReadOnlyList<string>> BranchesAsync() =>
        [.. (await Repo.RefsAsync(Root, ["refs/heads"], null, null, CancellationToken.None))!.Select(each => each.Name["refs/heads/".Length..])];

    internal async Task<string?> LockReasonAsync(string slot) =>
        (await Repo.WorktreesAsync(Root, CancellationToken.None))!.First(each => TreeGit.SamePath(each.Path, slot)).LockReason;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    private sealed class Chosen(IDisposable repository, IDisposable git) : IDisposable
    {
        public void Dispose()
        {
            git.Dispose();
            repository.Dispose();
        }
    }
}
