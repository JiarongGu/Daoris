using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch home and a repository kept in memory for the integration run's steps: the repository's commits, refs, working trees and
/// files are a <see cref="MemoryRepository"/>, so no git starts, and the requests are kept in the home's own desktop store. A flow
/// <see cref="Use"/>s it before it asks a step anything; a git no port answered fails the case.
/// </summary>
internal sealed class IntegrationMemoryFixture : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-memory", Guid.NewGuid().ToString("N")[..8]);

    internal IntegrationMemoryFixture()
    {
        Home = Path.Combine(_scratch, "home");
        Root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(Home);
        Repo = new MemoryRepository(Root);
        Repo.Write(Root, "README.md", "# engine\n");
        Commit("first");
    }

    internal string Home { get; }

    internal string Root { get; }

    internal MemoryRepository Repo { get; }

    /// <summary>Where the integration trees of a run are made.</summary>
    internal string Trees => DaorisHome.Entry(Home, HomeEntry.Trees);

    internal string Head => Repo.HeadAsync(Root, Ct).GetAwaiter().GetResult()!;

    /// <summary>Makes the memory repository this flow's, with a git that fails any ask a port did not answer, until disposed.</summary>
    internal IDisposable Use() => new Chosen(FlowRepository.Use(Repo), FlowGit.Use(new ScriptedGit()));

    internal IntegrationStore Store(string run = "run-a", Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>Writes a file in the repository's own checkout and commits what changed, on the branch it stands on or detached where it is.</summary>
    internal string Commit(string message, string? file = null, string? content = null)
    {
        if (file is not null) Repo.Write(Root, file, content ?? "");
        Assert.True(Repo.CommitAllAsync(Root, message, Ct).GetAwaiter().GetResult().Ok);
        return Head;
    }

    internal void Switch(string? branch, string? start = null, bool creates = false) =>
        Assert.True(Repo.SwitchAsync(Root, new GitSwitch(branch, start, creates), Ct).GetAwaiter().GetResult().Ok);

    /// <summary>A branch of one commit writing <paramref name="file"/> from <paramref name="start"/>, made aside and left, the checkout detached at <paramref name="start"/>.</summary>
    internal string Branch(string name, string start, string file, string content)
    {
        Switch(name, start, creates: true);
        var tip = Commit($"work on {name}", file, content);
        Switch(null, start);
        return tip;
    }

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
