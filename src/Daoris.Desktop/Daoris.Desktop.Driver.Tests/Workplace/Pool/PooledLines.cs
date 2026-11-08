using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch home whose pool serves lines in slots, for the cases that ask a reader of a record's tree where its line is: a record names
/// a line, <see cref="Tree"/> is that name (never a folder), and a slot is made, taken by a line or let go of it by the store's own writes.
/// </summary>
internal sealed class PooledLines : IDisposable
{
    internal const string Workspace = "w";

    internal const string Repository = "engine";

    private readonly TreePool _pool;

    internal PooledLines()
    {
        Home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pooled-lines", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Home);
        _pool = new TreePool(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
    }

    internal string Home { get; }

    /// <summary>The path a record names for a line: no folder is made there.</summary>
    internal string Tree(string line) => Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), Workspace, Repository, line);

    /// <summary>A slot's fixed folder.</summary>
    internal string SlotFolder(int number) => KeptTree.FolderIn(Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), Workspace, Repository), number);

    /// <summary>Makes slot <paramref name="number"/> with its folder, serving <paramref name="line"/> or none.</summary>
    internal KeptTree Slot(int number, string line = "")
    {
        Directory.CreateDirectory(SlotFolder(number));
        File.WriteAllText(Path.Combine(SlotFolder(number), "file.txt"), "work");
        var claimed = _pool.Claim(new TreeClaimOpening(Workspace, Repository, number, TreeClaimKinds.Make, "quest-1", "", "", "abc123", "abc123", $"daoris-pool-{number}")
        {
            Phase = TreeClaimPhases.Opening,
        });
        _pool.Complete(claimed.Fence!, new SlotSettled($"k-{number}", SlotFolder(number), "", "abc123", KeptStates.Kept, "", 100, "tc-1"));
        if (line.Length > 0) Take(number, line);
        return _pool.Slot(Workspace, Repository, number)!;
    }

    /// <summary>Slot <paramref name="number"/> taken by <paramref name="line"/> at <paramref name="at"/>.</summary>
    internal void Take(int number, string line, DateTimeOffset? at = null)
    {
        var claimed = _pool.Claim(new TreeClaimOpening(Workspace, Repository, number, TreeClaimKinds.Take, "quest-1", line, $"daoris/{line}", "abc123", "abc123", $"daoris-pool-{number}")
        {
            Phase = TreeClaimPhases.Judged,
        });
        _pool.Complete(claimed.Fence!, new SlotSettled($"k-{number}", SlotFolder(number), line, "", KeptStates.Kept, "", 100, "tc-1"));
        if (at is { } since) SetSince(number, since);
    }

    /// <summary>The slot let go of its line, which keeps its let-go: the slot is free and the line can come back.</summary>
    internal void LetGo(int number, string line)
    {
        var now = DateTimeOffset.UtcNow;
        _pool.RecordEnd(new LineEnd(Workspace, Repository, line, $"daoris/{line}", "session-1", number, LineEndKinds.Output, "abc123", now, "", "tc-1", now));
        var claimed = _pool.Claim(new TreeClaimOpening(Workspace, Repository, number, TreeClaimKinds.End, "quest-1", line, $"daoris/{line}", "abc123", "abc123", $"daoris-pool-{number}")
        {
            Phase = TreeClaimPhases.Judged,
        });
        _pool.Complete(claimed.Fence!, new SlotSettled($"k-{number}", SlotFolder(number), "", "abc123", KeptStates.Kept, "", 100, "tc-1"));
    }

    private void SetSince(int number, DateTimeOffset since)
    {
        using var store = _pool.Open();
        store.Write(TreePool.Family, write => write.Execute(
            "UPDATE kept_trees SET since = $since WHERE workspace = $workspace AND repository = $repository AND number = $number",
            ("$since", Moments.Write(since)), ("$workspace", Workspace), ("$repository", Repository), ("$number", number)));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(Home);
    }
}
