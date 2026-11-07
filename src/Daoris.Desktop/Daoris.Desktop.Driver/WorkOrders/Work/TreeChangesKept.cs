using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What each running unit's tree changed since it opened, kept by what it was read from: the look's overlaps
/// (<see cref="WorkOverlaps.LookAsync"/>) ask git of a tree only where its <c>HEAD</c> or its index moved since the last read, and compare
/// the kept paths in memory, since a look comes every fifteen seconds and nearly every tree holds what it held at the last.
/// </summary>
/// <remarks>
/// <para><b>What a read rests on, read from git's files, no git started:</b> the commit it is compared with, the commit the tree's
/// <c>HEAD</c> stands at, and its index file's length and time. A tree whose files cannot say these is read as before, its top level asked,
/// and nothing is kept. An edit not yet staged moves no file of git's, so a kept read is read again once it is <see cref="Again"/> old.
/// The stamp is taken before the read, so a change during it is a different stamp the next time.</para>
/// </remarks>
internal sealed class TreeChangesKept
{
    /// <summary>How long a kept read stands where nothing of git's moved: the bound on an edit not yet staged being seen.</summary>
    internal static readonly TimeSpan Again = TimeSpan.FromMinutes(2);

    private static readonly ConcurrentDictionary<string, TreeChangesKept> Homes = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, Kept> _trees = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTimeOffset> _clock;
    private DateTimeOffset _swept;

    internal TreeChangesKept(Func<DateTimeOffset> clock) => _clock = clock;

    /// <summary>The kept reads of <paramref name="home"/>'s running trees, one for the process.</summary>
    internal static TreeChangesKept Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), _ => new TreeChangesKept(() => DateTimeOffset.UtcNow));

    /// <summary>The paths <paramref name="tree"/> changed since <paramref name="before"/> (<see cref="WorkOverlaps.Diff"/>): kept where nothing moved.</summary>
    internal async Task<IReadOnlyList<string>?> ReadAsync(string tree, string before, CancellationToken ct)
    {
        string key;
        try
        {
            key = Path.GetFullPath(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return await WorkOverlaps.DiffAsync(tree, before, ct).ConfigureAwait(false);
        }

        var now = _clock();
        Sweep(now);
        if (!WorkingTree.IsCommitId(before) || Stamp(key, before) is not { } stamp)
        {
            _trees.TryRemove(key, out _);
            return await WorkOverlaps.DiffAsync(tree, before, ct).ConfigureAwait(false);
        }

        if (_trees.TryGetValue(key, out var kept) && kept.Stamp == stamp && now - kept.At < Again) return kept.Paths;

        // The tree's own .git is its top level, so the diff is the one start.
        var paths = await WorkOverlaps.NamesAsync(tree, before, ct).ConfigureAwait(false);
        if (paths is null) _trees.TryRemove(key, out _);
        else _trees[key] = new Kept(stamp, now, paths);
        return paths;
    }

    /// <summary>What a tree's read rests on, from git's files; null where the tree has no <c>.git</c> of its own or a file cannot be read.</summary>
    private static string? Stamp(string tree, string before)
    {
        try
        {
            if (GitBranches.GitDirectory(tree) is not { } own || TreeTips.Read(tree) is not { } head) return null;
            var index = new FileInfo(Path.Combine(own, "index"));
            return string.Join('\n', before, head, index.Exists ? $"{index.Length}:{index.LastWriteTimeUtc.Ticks}" : "none");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // A tree tidied away is let go, looked for once an Again.
    private void Sweep(DateTimeOffset now)
    {
        if (now - _swept < Again) return;
        _swept = now;
        foreach (var tree in _trees.Keys)
        {
            if (!Directory.Exists(tree)) _trees.TryRemove(tree, out _);
        }
    }

    private sealed record Kept(string Stamp, DateTimeOffset At, IReadOnlyList<string> Paths);
}
