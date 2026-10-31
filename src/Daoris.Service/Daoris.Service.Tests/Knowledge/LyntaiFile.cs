using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;
using ICountableMemoryGraphStore = Lyntai.Memory.ICountableMemoryGraphStore;

namespace Daoris.Service.Tests;

/// <summary>
/// Lyntai's engines over a file of their own under the repository's gitignored <c>_fixtures/</c> (KNOWCENTRAL2d), and a reading of every
/// effect a read could have on an engine's entries: each one's decay state and revision, its links' weights, and the review rows.
/// </summary>
internal sealed class LyntaiFile : IAsyncDisposable
{
    private readonly string _root;

    private LyntaiFile(string root, TimeProvider? time)
    {
        _root = root;
        Directory.CreateDirectory(root);
        Memory = new LyntaiMemory(System.IO.Path.Combine(root, LyntaiMemory.FileName), time);
    }

    public LyntaiMemory Memory { get; }

    public string Path => System.IO.Path.Combine(_root, LyntaiMemory.FileName);

    public static LyntaiFile Open(string name, TimeProvider? time = null) =>
        new(System.IO.Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", name, Guid.NewGuid().ToString("N")[..8]), time);

    /// <summary>The engine of <paramref name="repository"/> as the coordinator opens it.</summary>
    public IMemoryEngine Engine(string repository) => Memory.Open(repository, MemoryReinforcement.None)!;

    /// <summary>Every effect a read could leave on <paramref name="ids"/> of <paramref name="repository"/>'s memory, as one text to compare.</summary>
    public async Task<string> StateAsync(string repository, params string[] ids)
    {
        var store = Memory.Store;
        var numbers = ids.Select(long.Parse).ToList();
        var text = new StringBuilder();
        foreach (var id in numbers)
        {
            var node = (await store.GetAsync(repository, id))!;
            text.AppendLine($"{id} r{node.Revision} recalls {node.RecallCount} s {node.Stability} d {node.Difficulty} age {node.Age}/{node.OrdinalAge} degree {node.Degree} ended {node.EndedReason}");
        }

        foreach (var link in (await store.NeighboursAsync(repository, LyntaiMemory.TaskKey, numbers, 100)).OrderBy(each => each.Node.Id))
        {
            text.AppendLine($"to {link.Node.Id} weight {link.EdgeWeight}");
        }

        text.AppendLine($"reviews {(await store.ReviewsAsync(repository)).Count}");
        return text.ToString();
    }

    /// <summary>How many entries <paramref name="repository"/>'s memory keeps.</summary>
    public async Task<int> CountAsync(string repository) =>
        (await ((ICountableMemoryGraphStore)Memory.Store).CountAsync(repository, LyntaiMemory.TaskKey, null)).Entries;

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearPool(new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path }.ToString()));
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file still open for a moment: the next prune of _fixtures takes it.
        }

        return ValueTask.CompletedTask;
    }
}
