using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A git place for the department tests, kept in memory: the repository's commits and status are a <see cref="MemoryRepository"/>, no git
/// starts, and the folder on the disk holds the files (which a manifest hashes) and an empty <c>.git</c> (which says it is a git place).
/// Chosen for the flow that makes it, until disposed.
/// </summary>
internal sealed class PlaceMemory : IDisposable
{
    private readonly IDisposable _repository;
    private readonly IDisposable _git;

    internal PlaceMemory(string place)
    {
        Place = place;
        Repository = new MemoryRepository(place);
        _repository = FlowRepository.Use(Repository);
        _git = FlowGit.Use(new ScriptedGit());
    }

    internal string Place { get; }

    internal MemoryRepository Repository { get; }

    /// <summary>The place made a git place holding <paramref name="files"/>, committed as one.</summary>
    internal async Task MakeAsync(params (string Path, string Text)[] files)
    {
        Directory.CreateDirectory(Path.Combine(Place, ".git"));
        foreach (var (path, text) in files) Put(path, text);
        await CommitAsync("one");
    }

    /// <summary>A file written on the disk, where a manifest reads its bytes, and in the repository, where status reads its state.</summary>
    internal void Put(string path, string text)
    {
        var full = Path.Combine(Place, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        Repository.Write(Place, path, text);
    }

    internal void Delete(string path)
    {
        File.Delete(Path.Combine(Place, path));
        Repository.Remove(Place, path);
    }

    /// <summary>Commits every change, as a person's commit does.</summary>
    internal async Task CommitAsync(string message) => Assert.True((await Repository.CommitAllAsync(Place, message, CancellationToken.None)).Ok);

    public void Dispose()
    {
        _git.Dispose();
        _repository.Dispose();
    }
}
