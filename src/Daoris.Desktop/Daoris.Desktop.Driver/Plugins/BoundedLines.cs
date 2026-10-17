using System.Text;

namespace Daoris.Driver;

/// <summary>One line read, or one dropped unread for its length (<see cref="Text"/> null), with what passing over it saw.</summary>
/// <param name="Id">A dropped frame's top-level integer <c>id</c>, where it had one: the request it answers, failed at once rather than left to wait.</param>
/// <param name="Method">Whether a dropped frame had a top-level <c>method</c>: a request or a notification, never an answer.</param>
public readonly record struct BoundedLine(string? Text, int? Id = null, bool Method = false)
{
    public bool Dropped => Text is null;
}

/// <summary>
/// Lines from a plugin's stdout with a bound (FABRIC-PERFORM1a, the fabric events design §C4): a line longer than the limit, in UTF-8 bytes, is
/// dropped unread, its characters passed over to its end without being kept, so no frame a plugin writes can grow the driver's memory past it.
/// </summary>
/// <remarks>
/// <para>It reads characters in blocks (<see cref="TextReader.ReadAsync(Memory{char}, CancellationToken)"/>), so the reader must give them: a process's
/// stdout does, and a test's stand-in that answers only <see cref="TextReader.ReadLineAsync()"/> is read by the peer without this bound.</para>
///
/// <para><b>A line ends as <see cref="TextReader.ReadLineAsync()"/> ends one</b> (FABRIC-PERFORM1a, round 2): at LF, CRLF or a bare CR. The wire
/// was read with it before the bound, so a plugin that answers <c>JSON\r</c> was answered, and still is.</para>
///
/// <para><b>Passing over a dropped line keeps two facts of it</b> (round 2): its top-level integer <c>id</c> and whether it names a top-level
/// <c>method</c>, read by a scan that holds a few fields whatever the line's length. An answer too long to read is then failed at once, as the
/// plugin's unreadable answer, rather than left to wait out its patience as a late one.</para>
/// </remarks>
public sealed class BoundedLines(TextReader reader, int limit)
{
    private readonly char[] _buffer = new char[4096];
    private readonly StringBuilder _line = new();
    private readonly FrameScan _scan = new();
    private int _at;
    private int _end;
    private bool _ended;

    // A line ended by a CR that was the last of its block: an LF opening the next block ends that same line, not another.
    private bool _afterCr;

    /// <summary>The next line, without its LF, CRLF or CR; a dropped one for a line over the limit; null at the stream's end.</summary>
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

            if (_afterCr)
            {
                _afterCr = false;
                if (_buffer[_at] == '\n')
                {
                    _at += 1;
                    continue;
                }
            }

            any = true;
            var found = _buffer.AsSpan(_at, _end - _at).IndexOfAny('\r', '\n');
            var stop = found < 0 ? _end : _at + found;
            if (over)
            {
                _scan.Feed(_buffer, _at, stop - _at);
            }
            else
            {
                bytes += Utf8Bytes(_buffer.AsSpan(_at, stop - _at));
                if (bytes > limit)
                {
                    // Over: what was kept is scanned once and let go, and the rest of the line is scanned as it passes.
                    over = true;
                    _scan.Reset();
                    foreach (var kept in _line.GetChunks()) _scan.Feed(kept.Span);
                    _scan.Feed(_buffer, _at, stop - _at);
                    _line.Clear();
                }
                else
                {
                    _line.Append(_buffer, _at, stop - _at);
                }
            }

            if (found >= 0)
            {
                _at = stop + 1;
                if (_buffer[stop] == '\r')
                {
                    if (_at < _end)
                    {
                        if (_buffer[_at] == '\n') _at += 1;
                    }
                    else
                    {
                        _afterCr = true;
                    }
                }

                return Finish(over);
            }

            _at = _end;
        }

        return any ? Finish(over) : null;
    }

    private BoundedLine Finish(bool over) => over ? _scan.Dropped() : new BoundedLine(_line.ToString());

    /// <summary>What <paramref name="chars"/> take in UTF-8, counted per character so a pair split between two blocks still counts four.</summary>
    private static long Utf8Bytes(ReadOnlySpan<char> chars)
    {
        long bytes = 0;
        foreach (var c in chars) bytes += c < 0x80 ? 1 : c < 0x800 ? 2 : char.IsSurrogate(c) ? 2 : 3;
        return bytes;
    }

    /// <summary>
    /// A JSON object's top level, read a character at a time and kept in a few fields: its integer <c>id</c> and whether it names a
    /// <c>method</c>. Anything nested, any other key and any value but the id's is passed over; a frame that is not an object names neither.
    /// </summary>
    private sealed class FrameScan
    {
        // The longest key kept: "method" and "id" are all it looks for.
        private const int KeyAtMost = 8;

        private readonly StringBuilder _key = new();
        private int _depth;
        private bool _inString;
        private bool _escaped;
        private bool _expectKey;
        private bool _readingKey;
        private bool _keyEscaped;
        private string? _lastKey;
        private string? _valueOf;
        private long? _number;
        private int? _id;
        private bool _method;

        public void Reset()
        {
            _key.Clear();
            (_depth, _inString, _escaped, _expectKey, _readingKey, _keyEscaped) = (0, false, false, false, false, false);
            (_lastKey, _valueOf, _number, _id, _method) = (null, null, null, null, false);
        }

        public void Feed(char[] chars, int start, int count) => Feed(chars.AsSpan(start, count));

        public void Feed(ReadOnlySpan<char> chars)
        {
            foreach (var c in chars) Step(c);
        }

        public BoundedLine Dropped()
        {
            EndNumber();
            return new BoundedLine(null, _id, _method);
        }

        private void Step(char c)
        {
            if (_inString)
            {
                if (_escaped)
                {
                    _escaped = false;
                    _keyEscaped |= _readingKey;
                }
                else if (c == '\\')
                {
                    _escaped = true;
                }
                else if (c == '"')
                {
                    _inString = false;
                    if (_readingKey)
                    {
                        _readingKey = false;
                        _lastKey = _keyEscaped || _key.Length > KeyAtMost ? null : _key.ToString();
                        if (_lastKey == "method") _method = true;
                    }
                }
                else if (_readingKey && _key.Length <= KeyAtMost)
                {
                    _key.Append(c);
                }

                return;
            }

            if (_number is { } number)
            {
                if (char.IsAsciiDigit(c))
                {
                    // Past an int, it is no id this wire gave: passed over.
                    _number = number > int.MaxValue ? number : number * 10 + (c - '0');
                    return;
                }

                EndNumber();
            }

            switch (c)
            {
                case '"':
                    _inString = true;
                    _readingKey = _depth == 1 && _expectKey;
                    if (_readingKey)
                    {
                        _expectKey = false;
                        _keyEscaped = false;
                        _key.Clear();
                    }

                    break;
                case '{' or '[':
                    _depth += 1;
                    if (_depth == 1) _expectKey = c == '{';
                    break;
                case '}' or ']':
                    _depth -= 1;
                    break;
                case ',' when _depth == 1:
                    _expectKey = true;
                    _valueOf = null;
                    break;
                case ':' when _depth == 1:
                    _valueOf = _lastKey;
                    break;
                default:
                    if (_depth == 1 && _valueOf == "id" && char.IsAsciiDigit(c))
                    {
                        _number = c - '0';
                        _valueOf = null;
                    }

                    break;
            }
        }

        private void EndNumber()
        {
            if (_number is { } number && number <= int.MaxValue) _id = (int)number;
            _number = null;
        }
    }
}
