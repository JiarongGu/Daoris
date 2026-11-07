using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The protocol door's console lines: the agent's words as the lines it wrote, never as the chunks the wire carried them in.
/// </summary>
/// <remarks>
/// <para>A message streams in pieces that break wherever the agent flushed, inside a word as often as not, so a piece joins the line it
/// continues, a newline in the words ends a line, and anything else the wire says (a tool call, a refusal, the turn's answer, the
/// stream's end) ends the open line before it is shown. A line the words leave open is shown once they go quiet, because an agent that
/// says something and then waits sends nothing that would end it.</para>
///
/// <para>The words are read from the event <see cref="AcpRender.Map"/> made, so the console and the record take them from one place.
/// The reader drives it and the quiet timer ends a line, so one lock holds both, and every line leaves in the order it is said.</para>
/// </remarks>
internal sealed class AcpConsole : IDisposable
{
    /// <summary>How long the words may go quiet before the line they left open is shown anyway.</summary>
    /// <remarks>
    /// 🔴 <b>Measured, not chosen</b>. Half a second broke real lines on the
    /// window (<c>al</c> / <c>pha: …</c>), because a real stream pauses that long mid-word. Across 13
    /// kept conversations, 2,968 gaps between one chunk and the next: median 48ms, p99 557ms, the
    /// longest 1,161ms, none over two seconds. So two seconds breaks no line that sample holds, and a
    /// held turn's words still show within two seconds.
    /// </remarks>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);

    private readonly Action<string> _onLine;
    private readonly TimeSpan _quiet;
    private readonly Timer _idle;
    private readonly object _gate = new();
    private readonly StringBuilder _open = new();
    private string? _openKind;

    /// <summary>The message the open line belongs to, where the wire named one.</summary>
    private string? _openId;
    private bool _disposed;

    /// <summary>Each tool call's title by its id, so its ending is said by name rather than by id.</summary>
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

    /// <param name="quiet">
    /// 🔴 How long before an open line is shown with nothing after it. Joining chunks alone held a
    /// line until the next update, and an agent that says something and then waits sends none: the
    /// words sat unseen for as long as it waited. A pause longer than this breaks the line there, which
    /// is rare and still readable; infinite turns the flush off.
    /// </param>
    public AcpConsole(Action<string> onLine, TimeSpan quiet)
    {
        _onLine = onLine;
        _quiet = quiet;
        _idle = new Timer(_ => End());
    }

    /// <summary>One update: its words joined to the open line, or the open line ended and the update shown.</summary>
    public void Update(JsonElement update, SessionEvent? mapped)
    {
        lock (_gate)
        {
            // A subagent's console is closed with its session while the reader may still hand it a
            // late update; a closed timer would throw into the reader.
            if (_disposed) return;

            if (mapped is { Kind: SessionEventKind.Message or SessionEventKind.Thought, Text: { } words })
            {
                // A new message ends the one before it, by the ids the wire gives them.
                if (_openKind != mapped.Kind || (_openId is not null && mapped.Id is not null && _openId != mapped.Id)) End();
                _openKind = mapped.Kind;
                _openId = mapped.Id;
                _open.Append(words);

                var text = _open.ToString();
                var cut = text.LastIndexOf('\n');
                if (cut >= 0)
                {
                    foreach (var line in text[..cut].Split('\n')) Say(line);
                    _open.Clear().Append(text[(cut + 1)..]);
                }

                if (_open.Length > 0) _idle.Change(_quiet, Timeout.InfiniteTimeSpan);
                return;
            }

            End();
            if (AcpRender.Render(update, _titles) is { } rendered) _onLine(rendered);
        }
    }

    /// <summary>A line of the door's own, after whatever the agent's words left open.</summary>
    public void Line(string text)
    {
        lock (_gate)
        {
            if (_disposed) return;
            End();
            _onLine(text);
        }
    }

    /// <summary>End the open line, if the agent's words left one.</summary>
    public void End()
    {
        lock (_gate)
        {
            // 🔴 A quiet flush already queued when the reader ended runs after the timer is gone, on a
            // pool thread, where a throw would take the process with it.
            if (_disposed) return;
            _idle.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (_open.Length > 0) Say(_open.ToString());
            _open.Clear();
            _openKind = null;
            _openId = null;
        }
    }

    /// <summary>The last line said, and the timer stopped: the reader has ended.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            End();
            _disposed = true;
            _idle.Dispose();
        }
    }

    private void Say(string line)
    {
        line = line.TrimEnd('\r');
        if (_openKind != SessionEventKind.Thought)
        {
            _onLine(line);
        }
        else if (line.Length > 0)
        {
            // A thought is marked on every line it runs to; a blank line in one marks nothing.
            _onLine($"· {line}");
        }
    }
}
