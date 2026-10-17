using System.Text;

namespace Daoris.Driver;

/// <summary>One line read, or one dropped unread for its length (<see cref="Text"/> null).</summary>
public readonly record struct BoundedLine(string? Text)
{
    public bool Dropped => Text is null;
}

/// <summary>
/// Lines from a plugin's stdout with a bound (FABRIC-PERFORM1a, the fabric events design §C4): a line longer than the limit, in UTF-8 bytes, is
/// dropped unread, its characters passed over to its end without being kept, so no frame a plugin writes can grow the driver's memory past it.
/// </summary>
/// <remarks>
/// It reads characters in blocks (<see cref="TextReader.ReadAsync(Memory{char}, CancellationToken)"/>), so the reader must give them: a process's
/// stdout does, and a test's stand-in that answers only <see cref="TextReader.ReadLineAsync()"/> is read by the peer without this bound.
/// </remarks>
public sealed class BoundedLines(TextReader reader, int limit)
{
    private readonly char[] _buffer = new char[4096];
    private readonly StringBuilder _line = new();
    private int _at;
    private int _end;
    private bool _ended;

    /// <summary>The next line, without its <c>\n</c> or <c>\r\n</c>; a dropped one for a line over the limit; null at the stream's end.</summary>
    public async ValueTask<BoundedLine?> ReadAsync(CancellationToken ct)
    {
        _line.Clear();
        long bytes = 0;
        var over = false;
        var any = false;
        while (true)
        {
            if (_at == _end)
            {
                if (_ended) break;
                _end = await reader.ReadAsync(_buffer.AsMemory(), ct).ConfigureAwait(false);
                _at = 0;
                if (_end == 0)
                {
                    _ended = true;
                    break;
                }
            }

            any = true;
            var newline = Array.IndexOf(_buffer, '\n', _at, _end - _at);
            var stop = newline < 0 ? _end : newline;
            if (!over)
            {
                bytes += Utf8Bytes(_buffer.AsSpan(_at, stop - _at));
                // One byte over is allowed while a line is open: it may be the `\r` of its `\r\n`.
                if (bytes > (long)limit + 1)
                {
                    over = true;
                    _line.Clear();
                }
                else
                {
                    _line.Append(_buffer, _at, stop - _at);
                }
            }

            if (newline >= 0)
            {
                _at = newline + 1;
                return Finish(over, bytes);
            }

            _at = _end;
        }

        return any ? Finish(over, bytes) : null;
    }

    private BoundedLine Finish(bool over, long bytes)
    {
        if (!over && _line.Length > 0 && _line[^1] == '\r')
        {
            _line.Length -= 1;
            bytes -= 1;
        }

        return over || bytes > limit ? new BoundedLine(null) : new BoundedLine(_line.ToString());
    }

    /// <summary>What <paramref name="chars"/> take in UTF-8, counted per character so a pair split between two blocks still counts four.</summary>
    private static long Utf8Bytes(ReadOnlySpan<char> chars)
    {
        long bytes = 0;
        foreach (var c in chars) bytes += c < 0x80 ? 1 : c < 0x800 ? 2 : char.IsSurrogate(c) ? 2 : 3;
        return bytes;
    }
}
