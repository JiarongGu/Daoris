using System.IO.Pipes;
using System.Runtime.Versioning;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The pipe's listening instances: each connection taken into the pipe's state
/// (<see cref="PipeState.Connections"/>, under its gate) and its place freed as it ends, at most <see cref="PipeLimits.Connections"/> at once.
/// It owns the places, one taken by the instance listening now.
/// </summary>
internal sealed class PipeListener(InstancePipe pipe, string name)
{
    private readonly PipeState _state = pipe.State;
    private readonly SemaphoreSlim _slots = new(pipe.Limits.Connections - 1, pipe.Limits.Connections);

    /// <summary>One listening instance, made by the driver's rule for this user's pipes (<see cref="UserPipe.Security"/>).</summary>
    [SupportedOSPlatform("windows")]
    public static NamedPipeServerStream Create(string name, int connections, bool first) =>
        NamedPipeServerStreamAcl.Create(
            name, PipeDirection.InOut, connections, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, 0, UserPipe.Security());

    public async Task AcceptAsync(NamedPipeServerStream first)
    {
        var listening = first;
        var token = pipe.Lifetime.Token;
        while (true)
        {
            try
            {
                await listening.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException)
            {
                await listening.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                // A client gone before it is accepted: this instance is spent, and the next one listens.
                await listening.DisposeAsync().ConfigureAwait(false);
                listening = await NextAsync(token, acquired: true).ConfigureAwait(false);
                if (listening is null) return;
                continue;
            }

            var connection = new PipeConnection(pipe, listening);
            lock (_state.Gate)
            {
                _state.Connections.Add(connection);
            }

            _ = connection.RunAsync();
            listening = await NextAsync(token, acquired: false).ConfigureAwait(false);
            if (listening is null) return;
        }
    }

    /// <summary>The next instance to listen on, once a connection's place is free; null once the instance ends.</summary>
    private async Task<NamedPipeServerStream?> NextAsync(CancellationToken token, bool acquired)
    {
        while (true)
        {
            try
            {
                if (!acquired) await _slots.WaitAsync(token).ConfigureAwait(false);
                acquired = true;
                if (!OperatingSystem.IsWindows()) return null;
                return Create(name, pipe.Limits.Connections, first: false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Every instance the name allows is open this moment: looked at again shortly.
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }
    }

    public void Ended(PipeConnection connection)
    {
        string[] unwaited;
        lock (_state.Gate)
        {
            if (!_state.Connections.Remove(connection)) return;
            // A press whose terminal's line ended waits on, and its card says no terminal waits on it now.
            unwaited = [.. _state.WaitingFor.Where(each => _state.Owners.GetValueOrDefault(each.Key) == connection).Select(each => each.Value)];
        }

        foreach (var confirmation in unwaited) pipe.Confirmations.WaitedOn(confirmation, false);

        try
        {
            _slots.Release();
        }
        catch (Exception error) when (error is SemaphoreFullException or ObjectDisposedException)
        {
            // The instance is ending: there is no next connection to make room for.
        }
    }
}
