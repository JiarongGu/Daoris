using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A store entry as eviction weighs it: its key, its size, and its last placement or seal.</summary>
internal sealed record StoreEntry(string Key, long Bytes, DateTimeOffset Placed);

/// <summary>
/// Daoris's dependency store, the home's <c>work/deps/</c>: one entry per key, each a read-only copy of a folder Daoris's own install
/// made and sealed, with its manifest beside it. It is filled only by sealing, copied and never moved, so a package manager's hard links
/// into its own store (pnpm's) never share a file record, or the read-only mark, with an entry; and it is placed into a tree only by
/// copy, never a link or a junction, so a write in one tree or a manager's clean never reaches another tree or the entry.
/// </summary>
internal static class DependencyStore
{
    private const string Files = "files";
    private const string ManifestName = "manifest.json";
    private const string PlacedName = "placed";

    /// <summary>The name a folder is made under beside where it goes, a random tail after it.</summary>
    internal const string MakingPrefix = ".daoris-making-";

    /// <summary>How many files are copied at once: eight, past which a measured copy gets no faster.</summary>
    private static readonly int AtOnce = Math.Clamp(Environment.ProcessorCount, 1, 8);

    /// <summary>The test seam: run with each file's path before a placement copies it, where a test cuts a placement off. Production sets none.</summary>
    internal static readonly AsyncLocal<Action<string>?> Copying = new();

    internal static string EntryOf(string home, string key) => DaorisHome.Entry(home, HomeEntry.Deps, key);

    internal static string RootOf(string home) => Path.GetDirectoryName(EntryOf(home, "entry"))!;

    /// <summary>The manifest of <paramref name="key"/>'s entry, or null where the store holds none whole.</summary>
    internal static DependencyManifest? Hit(string home, string key)
    {
        var manifest = Path.Combine(EntryOf(home, key), ManifestName);
        if (!File.Exists(manifest)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            return DependencyManifest.FromJson(document.RootElement);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// <paramref name="folder"/> of <paramref name="tree"/>, as <paramref name="manifest"/> read it, copied into the store at
    /// <paramref name="key"/>: made under a name of its own and renamed to its key, create-only, so a second sealer of one key discards
    /// its own. The read-only mark is set on the store's copy alone. True where the entry stands after it, whoever sealed it.
    /// </summary>
    internal static bool Seal(string home, string key, string tree, string folder, DependencyManifest manifest, CancellationToken ct)
    {
        var root = RootOf(home);
        var entry = EntryOf(home, key);
        if (Hit(home, key) is not null) return true;
        var making = Path.Combine(root, MakingPrefix + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var into = Path.Combine(making, Files);
            Directory.CreateDirectory(into);
            foreach (var each in manifest.Folders) Directory.CreateDirectory(Path.Combine(into, each));
            AtOnceEach(manifest.Files, ct, each =>
            {
                var to = Path.Combine(into, each.Path);
                File.Copy(Path.Combine(tree, folder, each.Path), to);
                File.SetAttributes(to, File.GetAttributes(to) | FileAttributes.ReadOnly);
            });
            AtomicFile.WriteText(Path.Combine(making, ManifestName), manifest.ToJson().ToJsonString());
            File.WriteAllText(Path.Combine(making, PlacedName), "");
            try
            {
                Directory.Move(making, entry);
                return true;
            }
            catch (IOException) when (Directory.Exists(entry))
            {
                Discard(making);
                return Hit(home, key) is not null;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Discard(making);
            if (error is OperationCanceledException) throw;
            return false;
        }
    }

    /// <summary>
    /// <paramref name="key"/>'s entry copied into <paramref name="folder"/> of <paramref name="tree"/>, an absent folder: made under a
    /// name of its own beside it, eight files at once, the read-only mark cleared, each link made again inside the tree, the files at
    /// the folder's top (a manager's own record of its install) written last and stamped now, then renamed into place. The copy's
    /// manifest, each file's write time its own, or null where it placed nothing: a source missing or of another size evicts the entry.
    /// </summary>
    internal static DependencyManifest? Place(string home, string key, string tree, string folder, CancellationToken ct)
    {
        if (Hit(home, key) is not { } manifest) return null;
        var entry = EntryOf(home, key);
        var source = Path.Combine(entry, Files);
        foreach (var each in manifest.Files)
        {
            var info = new FileInfo(Path.Combine(source, each.Path));
            if (!info.Exists || info.Length != each.Size)
            {
                Evict(home, key);
                return null;
            }
        }

        var final = Path.Combine(tree, folder);
        var making = Path.Combine(Path.GetDirectoryName(final.TrimEnd(Path.DirectorySeparatorChar))!, MakingPrefix + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(making);
            foreach (var each in manifest.Folders) Directory.CreateDirectory(Path.Combine(making, each));
            var top = manifest.Files.Where(each => !each.Path.Contains('/')).ToList();
            AtOnceEach(manifest.Files.Except(top), ct, each => Copy(source, making, each));
            ct.ThrowIfCancellationRequested();
            foreach (var link in manifest.Links)
            {
                DependencyLinks.Make(Path.Combine(making, link.Path), Path.Combine(final, link.Path), Path.Combine(tree, link.Target), link.Folder);
            }

            foreach (var each in top)
            {
                Copy(source, making, each);
                File.SetLastWriteTimeUtc(Path.Combine(making, each.Path), DateTime.UtcNow);
            }

            Directory.Move(making, final);
        }
        catch
        {
            Discard(making);
            throw;
        }

        try
        {
            File.WriteAllText(Path.Combine(entry, PlacedName), "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Only the entry's age reads it: a placement that cannot stamp it is placed all the same.
        }

        return manifest.WrittenIn(tree, folder);
    }

    /// <summary>A parallel loop wraps a file's failure in an AggregateException, which no caller's filter for a disk's failure matches.</summary>
    private static void AtOnceEach(IEnumerable<ManifestFile> files, CancellationToken ct, Action<ManifestFile> copy)
    {
        try
        {
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = AtOnce, CancellationToken = ct }, copy);
        }
        catch (AggregateException error) when (error.InnerExceptions.Count > 0)
        {
            ExceptionDispatchInfo.Capture(error.InnerExceptions[0]).Throw();
        }
    }

    private static void Copy(string source, string into, ManifestFile each)
    {
        var from = Path.Combine(source, each.Path);
        Copying.Value?.Invoke(from);
        var to = Path.Combine(into, each.Path);
        File.Copy(from, to);
        File.SetAttributes(to, File.GetAttributes(to) & ~FileAttributes.ReadOnly);
    }

    /// <summary>Every entry the store holds whole, with its size and its last placement.</summary>
    internal static IReadOnlyList<StoreEntry> Entries(string home)
    {
        var root = RootOf(home);
        if (!Directory.Exists(root)) return [];
        var entries = new List<StoreEntry>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var key = Path.GetFileName(folder);
            if (key.StartsWith('.') || Hit(home, key) is not { } manifest) continue;
            var placed = Path.Combine(folder, PlacedName);
            entries.Add(new StoreEntry(key, manifest.Bytes, File.Exists(placed) ? File.GetLastWriteTimeUtc(placed) : Directory.GetLastWriteTimeUtc(folder)));
        }

        return entries;
    }

    /// <summary>
    /// The entries not placed within <paramref name="days"/>, then the least recently placed while the store holds more than
    /// <paramref name="bytes"/>, removed: each one's key. A placed copy is its tree's, so an entry's eviction leaves it.
    /// </summary>
    internal static IReadOnlyList<string> Evict(string home, TimeSpan days, long bytes, DateTimeOffset now)
    {
        Sweep(home, now);
        var entries = Entries(home).OrderBy(each => each.Placed).ToList();
        var gone = new List<string>();
        var held = entries.Sum(each => each.Bytes);
        foreach (var each in entries)
        {
            if (now - each.Placed <= days && held <= bytes) continue;
            if (!Evict(home, each.Key)) continue;
            gone.Add(each.Key);
            held -= each.Bytes;
        }

        return gone;
    }

    /// <summary>A cut-off seal's making folder and a key's folder an unfinished eviction left without its manifest, which refuses that key's next seal.</summary>
    private static void Sweep(string home, DateTimeOffset now)
    {
        var root = RootOf(home);
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(folder);
            var left = name.StartsWith(MakingPrefix, StringComparison.Ordinal) || (!name.StartsWith('.') && !File.Exists(Path.Combine(folder, ManifestName)));
            if (left && now - Directory.GetLastWriteTimeUtc(folder) > TimeSpan.FromDays(1)) Discard(folder);
        }
    }

    /// <summary>One entry removed, its manifest first so no reader takes a half entry for a hit; false where it stays.</summary>
    internal static bool Evict(string home, string key)
    {
        var entry = EntryOf(home, key);
        try
        {
            File.Delete(Path.Combine(entry, ManifestName));
            Discard(entry);
            return !Directory.Exists(entry);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Discard(string folder)
    {
        if (!Path.Exists(folder)) return;
        try
        {
            TreeSetAside.DeleteNeverFollowing(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Under a name no reader takes for an entry or a placement; the next eviction or open passes it.
        }
    }
}
