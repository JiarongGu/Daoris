namespace Daoris.Driver;

/// <summary>
/// The stats an answer rests on, read before git answers it: an answer no bytes of its own prove is admitted only where the fence read
/// again after it is equal and every write time in it settled on its volume's clock. A change that restores an input's former content
/// leaves its stats moved: a rewrite moves the file's, an add or a remove its folder's.
/// </summary>
internal sealed class StatFence
{
    private readonly List<(string Path, FileStat? Stat)> _held = [];

    internal IReadOnlyList<(string Path, FileStat? Stat)> Held => _held;

    /// <summary>The stat of <paramref name="path"/>, or of its nearest folder where it is absent.</summary>
    internal void Add(string path)
    {
        for (var at = path; at is { Length: > 0 }; at = Path.GetDirectoryName(at))
        {
            if (FileStat.Of(at) is { } stat)
            {
                _held.Add((at, stat));
                return;
            }
        }

        _held.Add((path, null));
    }

    /// <summary>The folder <paramref name="path"/> and every folder under it; its nearest folder where it is absent.</summary>
    internal void AddFolders(string path)
    {
        if (!Directory.Exists(path))
        {
            Add(path);
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Prepend(path).Order(StringComparer.Ordinal)) Add(folder);
    }

    /// <summary>Whether every write time held is settled on its volume's clock, read now.</summary>
    internal bool Settled()
    {
        var clocks = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, stat) in _held)
        {
            if (stat is not { } held) continue;
            var root = Path.GetPathRoot(path) ?? "";
            if (!clocks.TryGetValue(root, out var now)) clocks[root] = now = VolumeClock.Now(path);
            if (!held.SettledAt(now)) return false;
        }

        return true;
    }

    /// <summary>Whether each stat read again now is the one held.</summary>
    internal bool Holds() => _held.All(each => FileStat.Of(each.Path) == each.Stat);
}
