using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What the line holds of a tree's work at a tip (<see cref="HoldReads.ContentAsync"/>), kept while nothing it rests on moved, so a look
/// over an unchanged branch starts no git. A read rests on what <see cref="ContentStamp"/> reads from git's files and the
/// home's: the tree's head, its repository's counted and packed refs (the line, the person's branches and the tags among them), the
/// home's landings and choices, and the tree's working files and index. A read is kept only where the stamp taken before it stands after
/// it; a tree no stamp can be taken of is read afresh each time, as before. Observations alone: an act judges afresh.
/// </summary>
internal sealed class ContentReadMemo
{
    private const int Most = 256;

    private static readonly ConcurrentDictionary<string, ContentReadMemo> Homes = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (ContentStamp Stamp, HoldContent Read)> _kept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The memo of <paramref name="home"/>'s trees, one for the process.</summary>
    internal static ContentReadMemo Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), _ => new ContentReadMemo());

    /// <summary>Forgets what this process kept of <paramref name="home"/>: the tests' restart.</summary>
    internal static void Release(string home) => Homes.TryRemove(Path.GetFullPath(home), out _);

    /// <summary>The kept read of <paramref name="tree"/> while <paramref name="stamp"/> answers as at its keeping, else <paramref name="read"/>'s.</summary>
    internal async Task<HoldContent?> ReadAsync(string tree, Func<ContentStamp?> stamp, Func<CancellationToken, Task<HoldContent?>> read, CancellationToken ct)
    {
        var full = Path.GetFullPath(tree);
        var before = stamp();
        if (before is not null && _kept.TryGetValue(full, out var kept) && kept.Stamp == before) return kept.Read;
        var answer = await read(ct).ConfigureAwait(false);
        if (answer is not null && before is not null && stamp() == before)
        {
            if (_kept.Count >= Most) _kept.Clear();
            _kept[full] = (before, answer);
        }
        else
        {
            _kept.TryRemove(full, out _);
        }

        return answer;
    }
}

/// <summary>
/// What a content read of a tree at a tip rests on, from files alone: <see cref="Text"/> the tip, the tree's link and head, the commit its
/// branch names, its repository's counted refs' version and its packed refs, and the home's landings and choices; <see cref="Files"/> the
/// generation of its working files and index (<see cref="TreeReadChanges"/>). Null where any of it cannot be read so.
/// </summary>
internal sealed record ContentStamp(string Text, TreeReadChanges.Version Files)
{
    internal static ContentStamp? Of(SessionTrees trees, string tree, string tip)
    {
        try
        {
            var full = Path.GetFullPath(tree);
            if (TreeReadPass.HeadOf(full) is not { } head || CountedRefs.Read(head.Common) is not { } refs) return null;
            var at = head.Name is { } name ? head.Loose ?? refs.Packed.GetValueOrDefault(name) : head.Text;
            if (at is not { Length: > 0 } || !string.Equals(at, tip, StringComparison.OrdinalIgnoreCase)) return null;
            var landings = FileStamp.Of(trees.LandingsPath);
            var config = FileStamp.Of(trees.ConfigPath);
            if (landings.Unread || config.Unread) return null;
            if (TreeReadMemo.Of(trees.Context.Home).Changes.Read(full, head.GitDir) is not { } files) return null;
            var packed = string.Join('\n', refs.Packed.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key} {each.Value}"));
            var tags = LooseTags(head.Common);
            if (tags is null) return null;
            return new ContentStamp(
                string.Join('\n', tip, head.Link, head.Text, at, refs.Version, TreeReadPass.Hash(packed), tags, landings.Content ?? "none", config.Content ?? "none"),
                files);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The loose tags under <paramref name="common"/>, by name and commit, as one stamp; null where one cannot be read.</summary>
    private static string? LooseTags(string common)
    {
        var folder = Path.Combine(common, "refs", "tags");
        if (!Directory.Exists(folder)) return "";
        var lines = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (file.EndsWith(".lock", StringComparison.Ordinal)) continue;
            if (File.ReadAllText(file).Trim() is not { Length: > 0 } value) return null;
            lines.Add($"{Path.GetRelativePath(folder, file).Replace('\\', '/')} {value}");
        }

        return TreeReadPass.Hash(string.Join('\n', lines));
    }
}
