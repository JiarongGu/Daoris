using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The tool calls announced and not yet finished in one session, by id, and those a steer cut (STEER2a): open when the steer was
/// answered <c>injected</c>, and never heard of since. Written on the reader's thread, read by whoever steers.
/// </summary>
internal sealed class StepCalls
{
    private readonly object _gate = new();
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private readonly HashSet<string> _finished = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cut = new(StringComparer.Ordinal);

    /// <summary>A steer was answered <c>injected</c>: every call still open is cut until it is heard again.</summary>
    public void CutOpen()
    {
        lock (_gate) _cut.UnionWith(_open);
    }

    /// <summary>
    /// The calls a steer cut that never heard again, taken and counted finished, in a fixed order: the turn's prompt is answered, and
    /// the record closes them as cut before its turn's end, so the page never shows them running.
    /// </summary>
    public string[] TakeCut()
    {
        lock (_gate)
        {
            if (_cut.Count == 0) return [];
            string[] cut = [.. _cut.Order(StringComparer.Ordinal)];
            _cut.Clear();
            _open.ExceptWith(cut);
            _finished.UnionWith(cut);
            return cut;
        }
    }

    /// <summary>
    /// A call announced or updated: heard again, it was not cut. <c>pending</c> and <c>in_progress</c> open it, as does an
    /// announcement that says no status, which the protocol reads as <c>pending</c>; any other status finishes it. An update that says
    /// no status keeps what the call was, so a finished call stays finished and is never closed as cut.
    /// </summary>
    public void Track(JsonElement update, SessionEvent? mapped)
    {
        if (mapped is not { Kind: SessionEventKind.Tool, Id: { } id }) return;
        lock (_gate)
        {
            _cut.Remove(id);
            switch (mapped.Status)
            {
                case "pending" or "in_progress":
                    _finished.Remove(id);
                    _open.Add(id);
                    break;
                case null:
                    if (AcpRender.Kind(update) == "tool_call" && !_finished.Contains(id)) _open.Add(id);
                    break;
                default:
                    _open.Remove(id);
                    _finished.Add(id);
                    break;
            }
        }
    }
}
