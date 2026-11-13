using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What each conversation record spent, kept with how far into its file the reading reached: a read takes only the whole lines
/// written since the last, so the Parts strip's ask of a long conversation reads what it said since, never its whole record again.
/// </summary>
/// <remarks>
/// <para><b>Shared by every reader</b>, as the numbering is (<see cref="SessionEvents"/>): callers each build their own instance over one
/// folder. The record is append-only; a file shorter than the reading reached, or whose last line read no longer stands where it
/// stood (forgotten and written again, or replaced), is read whole.</para>
///
/// <para><b>A torn last line</b> waits: only lines ended by their newline are counted, so a line still being written is read once whole.</para>
/// </remarks>
internal sealed class SessionSpentKept
{
    public static SessionSpentKept Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, Kept> _kept = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="LineStart">Where the last whole line read begins.</param>
    /// <param name="Read">How far into the file the reading reached: just past the last whole line's newline.</param>
    /// <param name="LastLine">The last whole line's hash, which says the file read on is the one read before.</param>
    private sealed record Kept(long LineStart, long Read, byte[] LastLine, SessionSpent Spent);

    /// <summary>What the record at <paramref name="path"/> spent; none where there is no record.</summary>
    /// <exception cref="IOException">The record could not be read.</exception>
    public SessionSpent Of(string path)
    {
        lock (_gate)
        {
            if (!File.Exists(path))
            {
                _kept.Remove(path);
                return SessionSpent.None;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var kept = _kept.GetValueOrDefault(path);
            if (kept is not null && !Stands(stream, kept)) kept = null;
            var read = ReadOn(stream, kept ?? new Kept(0, 0, [], SessionSpent.None));
            _kept[path] = read;
            return read.Spent;
        }
    }

    /// <summary>Let go of the reading of <paramref name="path"/>: its record is gone.</summary>
    public void Forget(string path)
    {
        lock (_gate) _kept.Remove(path);
    }

    private static bool Stands(FileStream stream, Kept kept)
    {
        if (kept.Read == 0) return true;
        if (stream.Length < kept.Read) return false;
        var line = new byte[kept.Read - kept.LineStart];
        stream.Seek(kept.LineStart, SeekOrigin.Begin);
        stream.ReadExactly(line);
        return line[^1] == (byte)'\n' && SHA256.HashData(line.AsSpan(0, line.Length - 1)).AsSpan().SequenceEqual(kept.LastLine);
    }

    private static Kept ReadOn(FileStream stream, Kept from)
    {
        var spent = from.Spent;
        var lineStart = from.LineStart;
        var lastLine = from.LastLine;
        var at = from.Read;
        var seen = from.Read;
        stream.Seek(from.Read, SeekOrigin.Begin);
        var buffer = new byte[64 * 1024];
        using var pending = new MemoryStream();
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            var start = 0;
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] != (byte)'\n') continue;
                ReadOnlySpan<byte> line;
                if (pending.Length > 0)
                {
                    pending.Write(buffer, start, i - start);
                    line = pending.GetBuffer().AsSpan(0, (int)pending.Length);
                }
                else line = buffer.AsSpan(start, i - start);

                if (Event(line) is { Kind: SessionEventKind.Turn } turn) spent = spent.With(turn.Tokens);
                lineStart = at;
                lastLine = SHA256.HashData(line);
                at = seen + i + 1;
                pending.SetLength(0);
                start = i + 1;
            }

            pending.Write(buffer, start, count - start);
            seen += count;
        }

        return new Kept(lineStart, at, lastLine, spent);
    }

    /// <summary>One line read as its event, or null for a blank, torn or foreign line, as the record's own reader reads it.</summary>
    internal static SessionEvent? Event(ReadOnlySpan<byte> line)
    {
        line = line.TrimEnd((byte)'\r');
        if (line.Trim(" \t"u8).IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<SessionEvent>(line, SessionEventText.Json) is { Kind.Length: > 0 } e ? e : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
