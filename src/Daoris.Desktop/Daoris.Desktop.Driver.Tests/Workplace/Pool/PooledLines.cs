using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pool in a home, for the cases that ask a reader of a record's tree where its line is: a record names a line, <see cref="Tree"/> is
/// that name (no folder is made there), and a slot is made, taken by a line or let go of it by the store's own writes.
/// </summary>
internal sealed class PooledLines : IDisposable
{
    private readonly TreePool _pool;

    private readonly bool _owns;

    /// <param name="home">A home that exists already, left alone at the end; null makes a scratch one.</param>
    internal PooledLines(string? home = null, string workspace = "w", string repository = "engine")
    {
        (Workspace, Repository, _owns) = (workspace, repository, home is null);
        Home = home ?? Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pooled-lines", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Home);
        _pool = new TreePool(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
    }

    internal string Workspace { get; }

    internal string Repository { get; }

    internal string Home { get; }

    private string Trees => Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), Workspace, Repository);

    /// <summary>The path a record names for a line: no folder is made there.</summary>
    internal string Tree(string line) => Path.Combine(Trees, line);

    /// <summary>A slot's fixed folder.</summary>
    internal string SlotFolder(int number) => KeptTree.FolderIn(Trees, number);

    /// <summary>Makes slot <paramref name="number"/>, serving <paramref name="line"/> or none; <paramref name="folder"/> false leaves the folder to git.</summary>
    internal KeptTree Slot(int number, string line = "", bool folder = true)
    {
        if (folder)
        {
            Directory.CreateDirectory(SlotFolder(number));
            File.WriteAllText(Path.Combine(SlotFolder(number), "file.txt"), "work");
        }

        var claimed = _pool.Claim(Opening(number, TreeClaimKinds.Make, "", TreeClaimPhases.Opening));
        _pool.Complete(claimed.Fence!, Settled(number, ""));
        if (line.Length > 0) Take(number, line);
        return _pool.Slot(Workspace, Repository, number)!;
    }

    /// <summary>Slot <paramref name="number"/> taken by <paramref name="line"/>.</summary>
    internal void Take(int number, string line)
    {
        var claimed = _pool.Claim(Opening(number, TreeClaimKinds.Take, line, TreeClaimPhases.Judged));
        _pool.Complete(claimed.Fence!, Settled(number, line));
    }

    /// <summary>The slot let go of its line, which keeps its let-go: the slot is free and the line can come back.</summary>
    internal void LetGo(int number, string line)
    {
        var now = DateTimeOffset.UtcNow;
        _pool.RecordEnd(new LineEnd(Workspace, Repository, line, $"daoris/{line}", "session-1", number, LineEndKinds.Output, "abc123", now, "", "tc-1", now));
        var claimed = _pool.Claim(Opening(number, TreeClaimKinds.End, line, TreeClaimPhases.Judged));
        _pool.Complete(claimed.Fence!, Settled(number, ""));
    }

    /// <summary>The moment the present line took slot <paramref name="number"/>, set by hand.</summary>
    internal void TookAt(int number, DateTimeOffset since)
    {
        using var store = _pool.Open();
        store.Write(TreePool.Family, write => write.Execute(
            "UPDATE kept_trees SET since = $since WHERE workspace = $workspace AND repository = $repository AND number = $number",
            ("$since", Moments.Write(since)), ("$workspace", Workspace), ("$repository", Repository), ("$number", number)));
    }

    private TreeClaimOpening Opening(int number, string kind, string line, string phase) =>
        new(Workspace, Repository, number, kind, "quest-1", line, line.Length == 0 ? "" : $"daoris/{line}", "abc123", "abc123", $"daoris-pool-{number}")
        {
            Phase = phase,
        };

    private SlotSettled Settled(int number, string line) =>
        new($"k-{number}", SlotFolder(number), line, line.Length == 0 ? "abc123" : "", KeptStates.Kept, "", 100, "tc-1");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (_owns) GitSeed.Discard(Home);
    }
}
