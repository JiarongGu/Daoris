using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What the pipe was asked, by each request's id (RPC1a, the terminal RPC design §6.1): a line when a press begins and a line when
/// it ends, with its answer, kept 24 hours under <c>&lt;home&gt;/pipe/</c>, one file per UTC day. So an id seen before is answered
/// from its line and never run twice, and <c>RESULT</c> says how a press ended whether or not its client was still there.
/// </summary>
/// <remarks>
/// <para><b>Read once, at the start</b>: an id whose last line says it began, written by an instance that stopped before it ended,
/// is <see cref="Seen.Interrupted"/>, whose outcome nobody knows. A line that does not read is passed over; a day's file older than
/// yesterday is removed.</para>
///
/// <para><b>Each line appended whole</b>, BOM-less UTF-8 and LF, under one lock: a reader never meets half a line it wrote. The
/// answer is the response envelope as it went on the wire, which carries this machine's paths only to this machine (D46).</para>
/// </remarks>
internal sealed class PipeJournal
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _folder;
    private readonly TimeSpan _kept;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private DateTimeOffset _pruned;

    /// <summary>How an id seen before stands.</summary>
    public enum Seen
    {
        /// <summary>Answered: its answer is kept.</summary>
        Answered,

        /// <summary>Begun by an instance that stopped before it ended: whether it was done is not known.</summary>
        Interrupted,
    }

    private sealed record Entry(Seen State, string? Answer, DateTimeOffset At);

    public PipeJournal(string folder, TimeSpan kept, Func<DateTimeOffset>? clock = null)
    {
        _folder = folder;
        _kept = kept;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _pruned = _clock();
        Load();
    }

    /// <summary>How <paramref name="id"/> stands, with its answer where it was answered; null where it was never seen, or not for a day.</summary>
    public (Seen State, string? Answer)? Find(string id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out var entry) && _clock() - entry.At < _kept ? (entry.State, entry.Answer) : null;
        }
    }

    /// <summary><paramref name="id"/> begins, for <paramref name="request"/> (its module and route, never its payload).</summary>
    public void Begin(string id, string request)
    {
        var now = _clock();
        Append(now, writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("at", now);
            writer.WriteString("state", "running");
            writer.WriteString("request", request);
        });
        lock (_gate)
        {
            // Begun here and answered here: until it ends, this instance holds it as under way, not this record.
            _entries.Remove(id);
            if (now - _pruned > TimeSpan.FromHours(1)) Prune(now);
        }
    }

    /// <summary><paramref name="id"/> ended, answered as <paramref name="answer"/> (the response envelope's JSON).</summary>
    public void End(string id, string answer)
    {
        var now = _clock();
        Append(now, writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("at", now);
            writer.WriteString("state", "answered");
            writer.WritePropertyName("answer");
            writer.WriteRawValue(answer);
        });
        lock (_gate)
        {
            _entries[id] = new Entry(Seen.Answered, answer, now);
        }
    }

    private string FileOf(DateTimeOffset day) =>
        Path.Combine(_folder, day.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");

    private void Append(DateTimeOffset now, Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                using var file = new FileStream(FileOf(now), FileMode.Append, FileAccess.Write, FileShare.Read);
                buffer.Position = 0;
                buffer.CopyTo(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Not kept is not a failure of the press: it ran, and its answer still goes to the client that asked.
            }
        }
    }

    private void Load()
    {
        if (!Directory.Exists(_folder)) return;
        var now = _clock();
        RemoveOldFiles(now);
        foreach (var path in Directory.EnumerateFiles(_folder, "*.jsonl").Order(StringComparer.Ordinal))
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Utf8);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var line in lines) Read(line, now);
        }
    }

    private void Read(string line, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } named
                || !root.TryGetProperty("at", out var at) || !at.TryGetDateTimeOffset(out var when)
                || !root.TryGetProperty("state", out var state)
                || now - when >= _kept)
            {
                return;
            }

            switch (state.GetString())
            {
                case "running":
                    _entries[named] = new Entry(Seen.Interrupted, null, when);
                    break;
                case "answered" when root.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.Object:
                    _entries[named] = new Entry(Seen.Answered, answer.GetRawText(), when);
                    break;
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            // A line that does not read is passed over: the rest of the day's file still does.
        }
    }

    private void Prune(DateTimeOffset now)
    {
        _pruned = now;
        foreach (var stale in _entries.Where(each => now - each.Value.At >= _kept).Select(each => each.Key).ToList())
        {
            _entries.Remove(stale);
        }

        RemoveOldFiles(now);
    }

    /// <summary>Removes each day's file older than yesterday's: a day's lines are kept a day after it ends, and no longer.</summary>
    private void RemoveOldFiles(DateTimeOffset now)
    {
        var oldest = FileOf(now - TimeSpan.FromDays(1));
        try
        {
            foreach (var path in Directory.EnumerateFiles(_folder, "*.jsonl"))
            {
                if (string.CompareOrdinal(Path.GetFileName(path), Path.GetFileName(oldest)) < 0) File.Delete(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next look.
        }
    }
}
