using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A session's live console, the page's `bridge/console.ts` (MOD5): its backlog (D49 §2), the streams it
/// runs beside itself (CONSOLE2c), and a task stopped from its tab (CONSOLE3a).
/// </summary>
public sealed partial class DriverModule
{
    // The console's backlog (D49 §2): what this session has said, or what it has said since
    // the page last heard. Live lines arrive as `SESSION_OUTPUT` events; this is how a page
    // that just opened catches up, and how one that missed a batch closes the gap — the
    // sequence numbers are the driver's, so neither side has to remember the other.
    [DriverRoute("TAIL_SESSION")]
    private object? TailSession(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // Absent means "everything you have": a page opening a drawer has seen nothing, and
        // making it say so explicitly would be ceremony with a wrong default available.
        var tail = _loop.Output.Tail(id, Number(request, "after") ?? 0);
        return new
        {
            Session = id,
            Lines = tail.Lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
            tail.Sequence,
            tail.Live,
            tail.Dropped,
        };
    }

    // What a session runs beside itself (CONSOLE2c): each subagent and background task, with
    // the key its console is tailed by over `TAIL_SESSION`. Asked on open and again when a
    // `SESSION_STREAMS` event names the session.
    [DriverRoute("SESSION_STREAMS")]
    private object? SessionStreams(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return new
        {
            Session = id,
            Streams = _loop.Output.Streams(id)
                .Select(stream => new { stream.Key, stream.Kind, stream.Name, stream.Live, stream.State, stream.CanStop })
                .ToArray(),
        };
    }

    // Stopping one task a session runs, from its tab (CONSOLE3a): the harness's own stop, sent by
    // the session that runs it. Its stream ends when the wire says how. False is an answer — no
    // session here runs it, or its harness stopped nothing — and a key that is not one of this
    // session's tasks is refused in the driver's words, because the page never sends one.
    [DriverRoute("STOP_TASK")]
    private async Task<object?> StopTaskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var key = PayloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var prefix = SessionOutput.Key(id, $"{SessionStreamKind.Task}/");
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Length == prefix.Length)
        {
            throw new DriverException($"`{key}` is not background work of session {id}: only a task can be stopped from its tab.");
        }

        var stopped = await _loop.Processes.StopTaskAsync(id, key[prefix.Length..], CancellationToken.None)
            .ConfigureAwait(false);
        return new { Stopped = stopped ?? false };
    }
}
