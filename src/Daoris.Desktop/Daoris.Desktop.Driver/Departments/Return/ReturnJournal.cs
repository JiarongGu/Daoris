using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A return's journal, in the folder the run that returns it keeps beside its record: each file's decision written
/// before its write and its effect after, one line each, and every byte a write replaced or a removal took kept under <c>kept/</c>. A
/// return cut off between the two lines is taken up from them (<see cref="ReturnWrite.Resume"/>), so it neither writes a file twice nor
/// guesses at one half written.
/// </summary>
public sealed class ReturnJournal(string folder)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The effect of a write that held: the folder's file is the session's.</summary>
    public const string Done = "done";

    /// <summary>The folder's file moved meanwhile: left as it is, and put to the person.</summary>
    public const string Moved = "moved";

    /// <summary>Cut off before anything of the folder moved: the next return decides it afresh.</summary>
    public const string Undone = "undone";

    private string Lines => Path.Combine(folder, "journal.jsonl");

    /// <summary>Where the bytes of <paramref name="path"/> a write replaced or a removal took are kept.</summary>
    public string Kept(string path) => Path.Combine(folder, "kept", path);

    /// <summary>Write <paramref name="entry"/>'s decision before anything of its file moves.</summary>
    public void Begin(ReturnEntry entry) => Append(entry with { Effect = null });

    /// <summary>Write the effect <paramref name="effect"/> of <paramref name="entry"/>'s write.</summary>
    public void End(ReturnEntry entry, string effect) => Append(entry with { Effect = effect });

    /// <summary>The files begun and never ended, in the order they were begun.</summary>
    public IReadOnlyList<ReturnEntry> Open()
    {
        if (!File.Exists(Lines)) return [];
        var open = new Dictionary<string, ReturnEntry>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Lines))
        {
            // A line cut off mid-write is the last one, written by a return that never passed its boundary.
            if (Read(line) is not { } entry) continue;
            if (entry.Effect is null) open[entry.Path] = entry;
            else open.Remove(entry.Path);
        }

        return [.. open.Values];
    }

    /// <summary>Move the bytes set aside at <paramref name="aside"/> into <see cref="Kept"/> for <paramref name="path"/>.</summary>
    public void Keep(string path, string aside)
    {
        var kept = Kept(path);
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.Move(aside, kept, overwrite: true);
    }

    private void Append(ReturnEntry entry)
    {
        Directory.CreateDirectory(folder);
        using var stream = new FileStream(Lines, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry, Json) + "\n"));
        stream.Flush(flushToDisk: true);
    }

    private static ReturnEntry? Read(string line)
    {
        try
        {
            return line.Length == 0 ? null : JsonSerializer.Deserialize<ReturnEntry>(line, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>One file of a return: what the folder held when it was read, what the copy holds, and the copy's file it comes from.</summary>
/// <param name="Expected">The folder's hash the write is conditioned on; null where the path is absent.</param>
/// <param name="Session">The copy's hash; null for a removal.</param>
/// <param name="From">The copy's path where it differs from <paramref name="Path"/>: a <i>Keep both</i>'s written beside.</param>
/// <param name="Effect">Null on the decision's line; <see cref="ReturnJournal.Done"/>, <see cref="ReturnJournal.Moved"/> or <see cref="ReturnJournal.Undone"/> on its effect's.</param>
public sealed record ReturnEntry(string Path, string? Expected, string? Session, string? From = null, string? Effect = null);
