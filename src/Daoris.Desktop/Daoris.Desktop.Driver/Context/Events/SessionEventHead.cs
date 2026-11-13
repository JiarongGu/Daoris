using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What an events file's whole read would answer for its long-lived questions, kept with how far into the file it was read: the last
/// number, the turns ended, where the opening and the last plan are, the first call that failed and the answer each word is pressed as,
/// and the byte offset of every 256th event. It is built by reading the file from <see cref="Length"/> on, so a head that stands for
/// the file's start reads only what was written since.
/// </summary>
/// <param name="Length">How far into the file the head reached: just past the last whole line's newline.</param>
/// <param name="LineStart">Where the last whole line begins.</param>
/// <param name="Tail">The last whole line's hash, which says the file read on is the one read before.</param>
/// <param name="OpeningAt">The byte offset of the first <c>user</c> event's line, or -1 for none.</param>
/// <param name="PlanAt">The byte offset of the newest plan event's line, or -1 for none.</param>
/// <param name="FailedId">The id of the first call that failed, or null for none.</param>
/// <param name="FirstFailure">The number of the first event of that call, or 0 while none is found.</param>
/// <param name="Offsets">The byte offset of each event whose number is a multiple of <see cref="Every"/>, by number, ascending.</param>
internal sealed record SessionEventHead(
    long Length,
    long LineStart,
    string Tail,
    long FirstSeq,
    long LastSeq,
    long Turns,
    long OpeningAt,
    long OpeningSeq,
    long PlanAt,
    string? FailedId,
    long FirstFailure,
    IReadOnlyDictionary<string, string> Choices,
    IReadOnlyList<KeyValuePair<long, long>> Offsets)
{
    /// <summary>Every this-many events a number's byte offset is kept.</summary>
    public const int Every = 256;

    public static SessionEventHead Empty { get; } = new(0, 0, "", 0, 0, 0, -1, 0, -1, null, 0, new Dictionary<string, string>(), []);

    /// <summary>Whether the file's bytes before <see cref="Length"/> are the ones this head was read from.</summary>
    public bool Stands(FileStream stream)
    {
        if (Length == 0) return true;
        if (stream.Length < Length) return false;
        var line = new byte[Length - LineStart];
        stream.Seek(LineStart, SeekOrigin.Begin);
        stream.ReadExactly(line);
        return line[^1] == (byte)'\n' && Hash(line.AsSpan(0, line.Length - 1)) == Tail;
    }

    /// <summary>The head with every whole line written after <see cref="Length"/> read in.</summary>
    public SessionEventHead ReadOn(FileStream stream)
    {
        var head = this;
        var choices = new Dictionary<string, string>(Choices, StringComparer.Ordinal);
        var offsets = new List<KeyValuePair<long, long>>(Offsets);
        var firstSeq = FirstSeq;
        var lastSeq = LastSeq;
        var turns = Turns;
        var openingAt = OpeningAt;
        var openingSeq = OpeningSeq;
        var planAt = PlanAt;
        var failedId = FailedId;
        var lineStart = LineStart;
        var tail = Tail;
        var at = Length;
        var seen = Length;
        stream.Seek(Length, SeekOrigin.Begin);
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

                if (SessionSpentKept.Event(line) is { } e)
                {
                    if (firstSeq == 0) firstSeq = e.Seq;
                    lastSeq = e.Seq;
                    if (e.Seq > 0 && e.Seq % Every == 0) offsets.Add(new(e.Seq, at));
                    if (e.Kind == SessionEventKind.Turn) turns++;
                    if (e.Kind == SessionEventKind.User)
                    {
                        if (openingAt < 0)
                        {
                            openingAt = at;
                            openingSeq = e.Seq;
                        }

                        if (e.Id is { Length: > 0 } id && e.Choice is { Length: > 0 } choice) choices[id] = choice;
                    }

                    if (e.Kind == SessionEventKind.Plan && e.Entries is not null) planAt = at;
                    if (failedId is null && e is { Kind: SessionEventKind.Tool, Status: "failed", Id: { } failed }) failedId = failed;
                }

                lineStart = at;
                tail = Hash(line);
                at = seen + i + 1;
                pending.SetLength(0);
                start = i + 1;
            }

            pending.Write(buffer, start, count - start);
            seen += count;
        }

        head = new SessionEventHead(at, lineStart, tail, firstSeq, lastSeq, turns, openingAt, openingSeq, planAt, failedId, FirstFailure, choices, offsets);
        return failedId is not null && head.FirstFailure == 0 ? head with { FirstFailure = FirstCallOf(stream, failedId) } : head;
    }

    /// <summary>The number of the first call event under <paramref name="id"/>, read from the file's start; 0 where none reads.</summary>
    private static long FirstCallOf(FileStream stream, string id)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        while (reader.ReadLine() is { } line)
        {
            if (SessionSpentKept.Event(System.Text.Encoding.UTF8.GetBytes(line)) is { Kind: SessionEventKind.Tool } e && e.Id == id) return e.Seq;
        }

        return 0;
    }

    /// <summary>The byte offset to read from to reach the events numbered above <paramref name="seq"/>: the last kept offset at or before it, or the file's start.</summary>
    public long OffsetBefore(long seq)
    {
        var found = 0L;
        foreach (var (at, offset) in Offsets)
        {
            if (at > seq) break;
            found = offset;
        }

        return found;
    }

    /// <summary>The offset kept before the one <see cref="OffsetBefore"/> gives, or the file's start where there is none.</summary>
    public long OffsetBeforeThat(long offset)
    {
        var found = 0L;
        foreach (var (_, kept) in Offsets)
        {
            if (kept >= offset) break;
            found = kept;
        }

        return found;
    }

    private static string Hash(ReadOnlySpan<byte> line) => Convert.ToHexString(SHA256.HashData(line));

    /// <summary>The choices as the store keeps them.</summary>
    public static string WriteChoices(IReadOnlyDictionary<string, string> choices) => JsonSerializer.Serialize(choices);

    public static IReadOnlyDictionary<string, string> ReadChoices(string text) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? [];
}
