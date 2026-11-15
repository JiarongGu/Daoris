using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The frames of one sitting as its harness and Daoris said them, one tape per session beside its transcript: every line of a door's
/// pipe, in the order it went, each with its moment and its direction, whichever harness is on it. The one recorder of a sitting's frames,
/// the protocol door's (both directions) and the native door's (what the harness says) alike.
/// </summary>
/// <remarks>
/// <para><b>Flushed as it is said</b>: a harness that is cut short, or never writes its own record of a turn, still leaves what it said.</para>
/// <para><b>Never a secret</b>: the values of an environment, headers and credentials a frame carries are left out where they sit.</para>
/// <para><b>It costs the run nothing</b>: a line that cannot be kept is said once and the sitting goes on; a tape past its bound says so
/// once and keeps no more.</para>
/// </remarks>
/// <param name="path">The tape's file (<see cref="PathOf"/>); appended to, so a resumed sitting goes on in the one tape.</param>
/// <param name="say">Told once the tape cannot keep a line or is full.</param>
/// <param name="cap">The most bytes one tape holds.</param>
internal sealed class FrameTape(string path, Action<string> say, long cap = FrameTape.DefaultCap) : IDisposable
{
    /// <summary>The suffix a tape carries after its session's name, as <see cref="SessionHomeFiles"/> counts it.</summary>
    internal const string Suffix = ".frames.jsonl";

    /// <summary>The most a tape holds: a long sitting's frames are bounded the way its transcript's tail is.</summary>
    internal const long DefaultCap = 16 * 1024 * 1024;

    private static readonly string[] Secrets = ["env", "headers", "authorization", "apikey", "api_key", "token", "password", "secret"];

    private readonly object _gate = new();
    private long _bytes;
    private bool _stopped;
    private bool _told;
    private bool _disposed;

    /// <summary>The tape of the session whose transcript is <paramref name="transcript"/>.</summary>
    public static string PathOf(string transcript) => Path.ChangeExtension(transcript, null) + Suffix;

    /// <summary>The pipe read through the tape: each line is kept as read, then handed on.</summary>
    public TextReader Hearing(TextReader inner) => new Heard(this, inner);

    /// <summary>The pipe written through the tape: each line is kept as written, then handed on.</summary>
    public TextWriter Speaking(TextWriter inner) => new Spoken(this, inner);

    /// <summary>One line kept, <paramref name="direction"/> <c>in</c> (the harness's) or <c>out</c> (Daoris's).</summary>
    public void Keep(string direction, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_gate)
        {
            if (_stopped || _disposed) return;
            try
            {
                var text = Entry(direction, line);
                var size = Encoding.UTF8.GetByteCount(text) + 1;
                if (_bytes + size > cap)
                {
                    _stopped = true;
                    Append(new JsonObject { ["at"] = Now(), ["stopped"] = "cap" }.ToJsonString());
                    Tell($"[the frames' tape is full at {cap} bytes, so no more are kept]");
                    return;
                }

                Append(text);
                _bytes += size;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _stopped = true;
                Tell($"[the frames' tape could not keep a line: {error.Message}]");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }

    private void Tell(string words)
    {
        if (_told) return;
        _told = true;
        say(words);
    }

    private void Append(string text)
    {
        // Opened and closed each line: the tape is a few lines a second, and a cut process never holds one half-written.
        using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        file.Write(Encoding.UTF8.GetBytes(text + "\n"));
    }

    private static string Entry(string direction, string line)
    {
        var entry = new JsonObject { ["at"] = Now(), ["dir"] = direction };
        try
        {
            entry["frame"] = Redacted(JsonNode.Parse(line));
        }
        catch (JsonException)
        {
            entry["raw"] = line;
        }

        return entry.ToJsonString();
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static JsonNode? Redacted(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject map:
                foreach (var (name, child) in map.ToList())
                {
                    if (Secrets.Contains(name, StringComparer.OrdinalIgnoreCase) && child is not null) map[name] = "[kept out]";
                    else Redacted(child);
                }

                break;
            case JsonArray list:
                foreach (var child in list) Redacted(child);
                break;
        }

        return node;
    }

    private sealed class Heard(FrameTape tape, TextReader inner) : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = await inner.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is not null) tape.Keep("in", line);
            return line;
        }

        public override string? ReadLine()
        {
            var line = inner.ReadLine();
            if (line is not null) tape.Keep("in", line);
            return line;
        }

        public override int Peek() => inner.Peek();

        public override int Read() => inner.Read();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class Spoken(FrameTape tape, TextWriter inner) : TextWriter
    {
        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value) => inner.Write(value);

        public override void Write(string? value) => inner.Write(value);

        public override void WriteLine(string? value)
        {
            if (value is not null) tape.Keep("out", value);
            inner.WriteLine(value);
        }

        public override Task WriteLineAsync(string? value)
        {
            if (value is not null) tape.Keep("out", value);
            return inner.WriteLineAsync(value);
        }

        public override Task FlushAsync() => inner.FlushAsync();

        public override void Flush() => inner.Flush();

        public override void Close() => inner.Close();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
