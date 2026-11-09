using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A terminal's heavy run in the running Daoris, the pipe's <c>RUN_HEAVY</c>: run by <see cref="HeavyTerminalRun"/> in this
/// process, so its gates are the application's children and in its jobs, and answered with the run's exit code. A run is held to
/// the connection that asked it, never to the instance's lifetime as a press is: that connection's <c>RUN_STOP</c>, its end (the
/// verb killed, its session ended) and Daoris's close each stop it, and it ends what it started before it answers. Nothing is
/// journaled, since a run whose terminal has gone has nobody to answer.
/// </summary>
internal sealed class PipeHeavyRuns(InstancePipe pipe)
{
    /// <summary>The machine log's event for a run's start (<c>state: running</c>) and its end (<c>state: ended</c>, <c>exit</c>).</summary>
    public const string LogEvent = "pipe.heavy";

    /// <summary><c>RUN_STOP</c> naming no run of this connection's that is under way.</summary>
    public const string NotRunning = "not-running";

    private readonly Lock _gate = new();
    private readonly Dictionary<PipeConnection, Dictionary<string, CancellationTokenSource>> _runs = [];

    /// <summary><c>RUN_HEAVY</c>: the run, answered once it ends; refused at once where Daoris closes or the terminal's process is unknown.</summary>
    public async Task<string> RunAsync(IpcRequest request, PipeConnection connection)
    {
        if (pipe.Closing) return IpcJson.Serialize(pipe.RefuseStopping(request));
        if (connection.Asker.Pid is not { } terminal)
        {
            return IpcJson.Serialize(pipe.Refuse(request, HeavyTerminalRun.Untaken, "the system did not say which process this terminal is, so its output cannot be taken."));
        }

        CancellationTokenSource stop;
        try
        {
            stop = CancellationTokenSource.CreateLinkedTokenSource(pipe.Lifetime.Token);
        }
        catch (ObjectDisposedException)
        {
            return IpcJson.Serialize(pipe.RefuseStopping(request));
        }

        // An id a run of this connection holds is another's: overwriting its entry would leave that run out of reach of its stop and
        // of the connection's end, and its own ending would remove the new run's entry.
        bool taken;
        lock (_gate)
        {
            if (!_runs.TryGetValue(connection, out var mine)) _runs[connection] = mine = new(StringComparer.Ordinal);
            taken = !mine.TryAdd(request.Id, stop);
        }

        if (taken)
        {
            stop.Dispose();
            return IpcJson.Serialize(pipe.RefuseTaken(request));
        }

        try
        {
            pipe.Log.Info(LogEvent, ("state", "running"), ("terminal", connection.Asker.Process ?? ""), ("door", PipeDoor.Terminal));
            var payload = request.Payload is { ValueKind: JsonValueKind.Object } body ? body : default;
            var answer = await HeavyTerminalRun.RunAsync(pipe.Home, payload, terminal, stop.Token).ConfigureAwait(false);
            pipe.Log.Info(LogEvent, ("state", "ended"), ("exit", answer.Exit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? answer.Refused ?? ""));
            return IpcJson.Serialize(answer.Exit is { } exit
                ? IpcResponse.CreateSuccess(request.Id, new { exit })
                : pipe.Refuse(request, answer.Refused ?? HeavyTerminalRun.Unread, answer.Message ?? "the run was not taken."));
        }
        finally
        {
            lock (_gate)
            {
                if (_runs.TryGetValue(connection, out var mine) && mine.Remove(request.Id) && mine.Count == 0) _runs.Remove(connection);
            }

            stop.Dispose();
        }
    }

    /// <summary><c>RUN_STOP { id }</c>: the run this connection asked under that id is stopped; it answers on its own once it has ended what it started.</summary>
    public IpcResponse Stop(IpcRequest request, PipeConnection connection)
    {
        var id = InstancePipe.Field(request, "id");
        CancellationTokenSource? stop = null;
        lock (_gate)
        {
            if (id is not null && _runs.TryGetValue(connection, out var mine)) mine.TryGetValue(id, out stop);
        }

        try
        {
            stop?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            stop = null;
        }

        return stop is null
            ? IpcResponse.CreateError(request.Id, NotRunning, null, "this terminal has no heavy run under way by that id.")
            : IpcResponse.CreateSuccess(request.Id, new { stopping = true });
    }

    /// <summary>A connection ended: every run it asked is stopped.</summary>
    public void ConnectionEnded(PipeConnection connection)
    {
        List<CancellationTokenSource> stops;
        lock (_gate)
        {
            if (!_runs.Remove(connection, out var mine)) return;
            stops = [.. mine.Values];
        }

        foreach (var stop in stops)
        {
            try
            {
                stop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ended as the connection did.
            }
        }
    }
}
