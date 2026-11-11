using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A folder department's copies, one for each session its workflow sets apart: every followed file of the folder copied into
/// <c>copies/c-&lt;8 hex&gt;</c> under the place's own folder in the home, beside its manifests, with a sidecar <c>c-&lt;8 hex&gt;.json</c>
/// naming the folder and the records that work in the copy. The manifest read at the making is kept in the place's store under the
/// copy's id, as its return's base. A folder whose files together pass the copy bound, or too large to follow, is never copied.
/// </summary>
public static class FolderCopies
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The folder a place's copies sit in.</summary>
    public static string Folder(string home, string workspace, string department) =>
        Path.Combine(DaorisHome.Entry(home, HomeEntry.Places, workspace, department), "copies");

    /// <summary>A copy of the folder at <paramref name="root"/>, or the words that refuse it.</summary>
    public static FolderCopyMade Make(
        string home, string workspace, string department, string root, ManifestBounds bounds, PlaceExcludes? excludes = null)
    {
        var store = new ManifestStore(home, workspace, department);
        var made = FolderManifestReader.Read(root, store.Content, store.Last(root), bounds, excludes);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        const string Then = "so it is not copied and its session waits: raise the bound with `daoris settings manifest copy <megabytes>`, "
            + "or choose a workflow for it with no isolate step, whose sessions work in the folder itself one at a time.";
        if (made.TooLarge) return new(null, $"`{name}` is too large to follow, {Then}");
        var size = made.Entries.Sum(entry => entry.Size ?? 0);
        if (size > bounds.Copy) return new(null, $"`{name}` holds {HistoryCommand.Size(size)}, past the copy bound of {HistoryCommand.Size(bounds.Copy)}, {Then}");
        if (made.Entries.Count > bounds.CopyFiles) return new(null, $"`{name}` holds {made.Entries.Count} files, past the copy bound of {bounds.CopyFiles} files, {Then}");
        // Small files cost far more than their bytes on disk, so the copy asks for twice the folder free beside the home.
        if (Free(home) is { } free && free < 2 * size)
        {
            return new(null, $"`{name}` holds {HistoryCommand.Size(size)} and the home's disk has {HistoryCommand.Size(free)} free, less than twice that, "
                + "so it is not copied and its session waits: free some space, or choose a workflow for it with no isolate step.");
        }

        var id = "c-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var copies = Folder(home, workspace, department);
        var folder = Path.Combine(copies, id);
        try
        {
            foreach (var entry in made.Entries)
            {
                var to = Path.Combine(folder, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(Path.Combine(root, entry.Path), to);
            }

            Directory.CreateDirectory(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            TryDelete(folder);
            return new(null, $"`{name}` could not be copied ({error.Message.TrimEnd().TrimEnd('.')}), so its session waits and the copy is tried again at a later look.");
        }

        // The return's base is the bytes the copy holds, every file hashed afresh, never a reading reused from size and time.
        var based = FolderManifestReader.Read(folder, store.Content, null, bounds, excludes);
        if (based.TooLarge || based.Entries.Count != made.Entries.Count || based.Entries.Any(entry => entry.Sha256 is null))
        {
            TryDelete(folder);
            return new(null, $"`{name}` could not be copied whole (a file could not be read), so its session waits and the copy is tried again at a later look.");
        }

        store.Keep(id, ManifestMoment.Start, based with { Root = root });
        var copy = new FolderCopy(id, folder, root, workspace, department);
        Save(copy);
        return new(copy, null);
    }

    /// <summary>Record <paramref name="record"/> as working in the copy at <paramref name="folder"/> for <paramref name="run"/>, if it is one.</summary>
    public static void Owned(string home, string folder, string record, string? run)
    {
        if (At(home, folder) is not { } copy) return;
        var owned = copy with
        {
            Records = copy.Records.Contains(record, StringComparer.Ordinal) ? copy.Records : [.. copy.Records, record],
            Runs = run is null || copy.Runs.Contains(run, StringComparer.Ordinal) ? copy.Runs : [.. copy.Runs, run],
        };
        if (owned.Records.Count != copy.Records.Count || owned.Runs.Count != copy.Runs.Count) Save(owned);
    }

    /// <summary>The copy whose folder <paramref name="folder"/> is, or null for any other folder.</summary>
    public static FolderCopy? At(string home, string? folder) => IdAt(home, folder) is null ? null : Read(Path.GetFullPath(Path.TrimEndingDirectorySeparator(folder!)) + ".json");

    public static string? IdAt(string home, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        var full = Path.GetFullPath(Path.TrimEndingDirectorySeparator(folder));
        var copies = Path.GetDirectoryName(full);
        if (copies is null || !string.Equals(Path.GetFileName(copies), "copies", StringComparison.OrdinalIgnoreCase)) return null;
        var places = Path.GetFullPath(DaorisHome.Entry(home, HomeEntry.Places));
        return full.StartsWith(places + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(full) : null;
    }

    /// <summary>The newest copy one of <paramref name="records"/> worked in, or null.</summary>
    public static FolderCopy? Of(string home, IEnumerable<string> records)
    {
        var asked = records.ToHashSet(StringComparer.Ordinal);
        return All(home).Where(copy => copy.Records.Any(asked.Contains)).OrderByDescending(copy => File.GetLastWriteTimeUtc(copy.Folder + ".json")).FirstOrDefault();
    }

    public static FolderCopy? Named(string home, string id) => All(home).FirstOrDefault(copy => copy.Id == id);

    /// <summary>Every copy of every place in the home, by its sidecar.</summary>
    public static IEnumerable<FolderCopy> All(string home)
    {
        var places = DaorisHome.Entry(home, HomeEntry.Places);
        if (!Directory.Exists(places)) return [];
        return Directory.EnumerateFiles(places, "c-*.json", SearchOption.AllDirectories).Select(Read).OfType<FolderCopy>();
    }

    /// <summary>The manifest of the folder read at <paramref name="copy"/>'s making: its return's base.</summary>
    public static RunManifest? Base(string home, FolderCopy copy) =>
        new ManifestStore(home, copy.Workspace, copy.Department).Read(copy.Id, ManifestMoment.Start);

    /// <summary>Remove the copy's folder and its sidecar once its return is done.</summary>
    public static void Remove(FolderCopy copy)
    {
        TryDelete(copy.Folder);
        try { File.Delete(copy.Folder + ".json"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static long? Free(string home)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(home))!).AvailableFreeSpace;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Save(FolderCopy copy) => AtomicFile.WriteText(copy.Folder + ".json", JsonSerializer.Serialize(copy, Json));

    private static FolderCopy? Read(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<FolderCopy>(AtomicFile.ReadText(file), Json) : null;
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            return null;
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the person: a copy half made or half removed holds nothing the folder lacks.
        }
    }
}

/// <summary>A copy of a folder department, made for its sessions working apart.</summary>
/// <param name="Id">Its name, <c>c-&lt;8 hex&gt;</c>: its folder's, and its base manifest's record in the place's store.</param>
/// <param name="Root">The folder it is a copy of, which its return writes into.</param>
public sealed record FolderCopy(string Id, string Folder, string Root, string Workspace, string Department)
{
    /// <summary>The records that worked in it.</summary>
    public IReadOnlyList<string> Records { get; init; } = [];

    /// <summary>The runs those records worked for, whose end and return the tidy reads (<see cref="CopyTidy"/>).</summary>
    public IReadOnlyList<string> Runs { get; init; } = [];
}

/// <summary>A copy made, or the words that refuse it.</summary>
public sealed record FolderCopyMade(FolderCopy? Copy, string? Refused);
