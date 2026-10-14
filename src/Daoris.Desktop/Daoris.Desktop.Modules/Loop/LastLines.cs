using System.Text;

namespace Daoris.Desktop;

/// <summary>
/// The last few lines a program printed on a stream, read for as long as it runs (HOSTSTART1): what the HTTP host said
/// on its standard error before it exited, for the sentence the window shows.
/// </summary>
/// <remarks>
/// <para><b>Read from the start, never held.</b> A pipe nobody reads fills, and a host that writes to a full one waits on
/// it, so a supervisor that redirected the stream and read it only after an exit would stall a chatty host before it
/// answered. A thread of its own reads it to its end, as the host's own input watch does (LOG2a): the read blocks for
/// the host's whole life, which is no work for the thread pool.</para>
///
/// <para><b>Bounded.</b> At most <see cref="MostLines"/> lines are kept, the newest, and each is cut at
/// <see cref="MostCharacters"/> as it is read, so a host that prints without end costs a few hundred characters. What
/// is said is the newest lines that fit in <see cref="MostCharacters"/> together.</para>
///
/// <para><b>As printed.</b> A line is kept as the host wrote it, markup and all, with its control characters dropped:
/// they print nothing, and a NUL ends a window label's text. Not kept: blank lines, and the frames of an exception
/// the runtime printed (<c>   at …</c>, <c>--- End of …</c>), which come after the exception's reasons and would push
/// them out. Nothing printed on the host's standard error is a secret: its starter's key arrives on its input, and no
/// start failure repeats that input (PERSONDOOR1a).</para>
/// </remarks>
public sealed class LastLines
{
    /// <summary>The most lines kept.</summary>
    public const int MostLines = 4;

    /// <summary>The most characters a line keeps, and the most the lines said keep together.</summary>
    public const int MostCharacters = 500;

    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private readonly StringBuilder _open = new();
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _cut;

    private LastLines()
    {
    }

    /// <summary>Completes once the stream has ended, or broken; it never faults.</summary>
    public Task Ended => _ended.Task;

    /// <summary>The newest lines kept, oldest first, one to a line, within <see cref="MostCharacters"/>; null when none.</summary>
    public string? Said
    {
        get
        {
            lock (_gate)
            {
                var said = new List<string>();
                var characters = 0;
                foreach (var line in _lines.Reverse())
                {
                    if (said.Count > 0 && characters + line.Length > MostCharacters) break;
                    said.Insert(0, line);
                    characters += line.Length;
                }

                return said.Count == 0 ? null : string.Join('\n', said);
            }
        }
    }

    /// <summary>Start reading <paramref name="reader"/> to its end on a thread of its own.</summary>
    public static LastLines Drain(TextReader reader)
    {
        var lines = new LastLines();
        var thread = new Thread(() => lines.Read(reader))
        {
            IsBackground = true,
            Name = "daoris: the host's standard error",
        };
        thread.Start();
        return lines;
    }

    private void Read(TextReader reader)
    {
        try
        {
            var buffer = new char[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                foreach (var c in buffer.AsSpan(0, read)) Take(c);
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            // A pipe whose writer died with its process breaks rather than ends: what was read is what it said.
        }
        finally
        {
            EndLine();
            _ended.TrySetResult();
        }
    }

    private void Take(char c)
    {
        if (c == '\n')
        {
            EndLine();
        }
        else if (char.IsControl(c) && c != '\t')
        {
            // Prints nothing; a line's `\r` among them.
        }
        else if (_open.Length < MostCharacters)
        {
            _open.Append(c);
        }
        else
        {
            _cut = true;
        }
    }

    private void EndLine()
    {
        var line = _open.ToString();
        if (_cut)
        {
            var keep = MostCharacters - 1;
            if (char.IsHighSurrogate(line[keep - 1])) keep -= 1;
            line = line[..keep] + "…";
        }

        _open.Clear();
        _cut = false;
        if (!Worth(line)) return;

        lock (_gate)
        {
            _lines.Enqueue(line);
            if (_lines.Count > MostLines) _lines.Dequeue();
        }
    }

    /// <summary>Whether a line says anything: not blank, and not one of an exception's frames.</summary>
    private static bool Worth(string line)
    {
        var text = line.TrimStart();
        if (text.Length == 0) return false;
        if (text.Length < line.Length && text.StartsWith("at ", StringComparison.Ordinal)) return false;
        return !text.StartsWith("--- End of ", StringComparison.Ordinal);
    }
}
