using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The small store the driver's list files of one shape share: a JSON file holding one array
/// of records, read as none when the file is missing or does not read, and changed by reading everything, handing it to a
/// change, bounding the answer and writing it whole under the store's gate.
/// </summary>
/// <remarks>
/// <para><b>Who uses it, and who does not.</b> The session branches, the landed branches, the auto-landing entries and the
/// abandon record read and edit this way. The opinion dues and gates keep the file format of <c>OpinionGateFiles</c> and edit a
/// mutable list or a dictionary; the session archive reads into a map and writes only when it changed something. Bending them to
/// this shape would add more than it took away.</para>
///
/// <para><b>One gate per store, not per instance</b>: a store is made anew for each press, so the caller passes its own static
/// gate. Reading takes no gate; a file that does not parse is no record, and is only written over by an edit, while one that cannot
/// be opened (another process holds it) fails the edit with the IO error and is left as it is.</para>
/// </remarks>
/// <param name="gate">The store's static lock: one writer at a time in this process.</param>
/// <param name="array">The name of the array property in the file.</param>
/// <param name="read">One entry, or null where it lacks what makes it one.</param>
/// <param name="toJson">The text of the whole file for these entries.</param>
/// <param name="bound">The newest-N rule an edit applies before it writes; null keeps every entry.</param>
internal sealed class RecordList<T>(
    object gate,
    string array,
    Func<JsonElement, T?> read,
    Func<IReadOnlyList<T>, string> toJson,
    Func<IReadOnlyList<T>, IReadOnlyList<T>>? bound = null)
    where T : class
{
    /// <summary>Every entry that reads, in the file's order; none when the file is missing or does not read.</summary>
    public IReadOnlyList<T> Read(string path)
    {
        try
        {
            return ReadOrThrow(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The entries as <see cref="Read"/> has them, but a file that could not be opened says so: its content is unknown, not none.</summary>
    private IReadOnlyList<T> ReadOrThrow(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty(array, out var entries) || entries.ValueKind != JsonValueKind.Array) return [];
            return [.. entries.EnumerateArray().Select(read).OfType<T>()];
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    /// <summary>Write these entries whole, atomically, into a folder made on the way. The caller holds the gate.</summary>
    public void Write(string path, IReadOnlyList<T> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, toJson(entries));
    }

    /// <summary>
    /// Read everything, change it, bound it and write it, under the gate, and under the record's lock across processes where the store
    /// names one (<paramref name="across"/>): the gate first, so a process's writers queue on it rather than on the file.
    /// </summary>
    public void Edit(string path, Func<IReadOnlyList<T>, IReadOnlyList<T>> change, string? across = null)
    {
        lock (gate)
        {
            using var held = across is null ? null : RecordLock.Take(across);
            // A file that cannot be opened is not an empty list: writing over it would lose every entry it holds.
            var changed = change(ReadOrThrow(path));
            Write(path, bound is null ? changed : bound(changed));
        }
    }
}
