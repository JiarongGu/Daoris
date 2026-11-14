using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch home and a repository kept in memory for the session trees: the repository's commits, refs, working trees and files are a
/// <see cref="MemoryRepository"/> that makes and removes each tree's folder as git does, so no git starts. A flow <see cref="Use"/>s it
/// before it asks the trees anything; a git that fails any ask no port answered stands beside it, which is how a leaked git is found.
/// </summary>
internal sealed class SessionTreeMemoryFixture : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-trees-memory", Guid.NewGuid().ToString("N")[..8]);

    internal SessionTreeMemoryFixture()
    {
        Home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(Home);
        Root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(Root);
        Repo = new MemoryRepository(Root) { Materializes = true };
        Repo.Write(Root, "README.md", "# engine\n");
        Repo.CommitAllAsync(Root, "first", CancellationToken.None).GetAwaiter().GetResult();
        Trees = new SessionTrees(Home);
    }

    internal string Home { get; }

    internal string Root { get; }

    internal MemoryRepository Repo { get; }

    internal SessionTrees Trees { get; }

    /// <summary>A folder in the scratch that no repository holds, as a person's own folder.</summary>
    internal string Folder(string name)
    {
        var folder = Path.Combine(_scratch, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Makes the memory repository this flow's, with a git that fails any ask a port did not answer, until disposed.</summary>
    internal IDisposable Use() => new Chosen(FlowRepository.Use(Repo), FlowGit.Use(new ScriptedGit()));

    internal async Task<IReadOnlyList<string>> BranchesAsync() =>
        [.. (await Repo.RefsAsync(Root, ["refs/heads/daoris"], null, null, CancellationToken.None))!.Select(each => each.Name)];

    internal async Task<int> TreesAsync() => (await Repo.WorktreesAsync(Root, CancellationToken.None))!.Count;

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
