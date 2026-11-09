using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The one way a door kind's work leaves the pass: taken once by its start's key, refused while the drain says no, the pass's flow suppressed,
/// and the work then run in its own task under its start's authority (<see cref="StepAuthority"/>), so a door's work reads the step it is for
/// and nothing the pass's flow carried.
/// </summary>
/// <param name="runs">Whether a door's work may begin now: not while an update drains; null always may.</param>
internal sealed class NodeDoorRuns(Func<bool>? runs = null)
{
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="work"/> started off the pass for <paramref name="start"/>; false where one runs already by its key or none may begin now.
    /// A failure is handed to <paramref name="told"/>, which says whether it took it; one it did not is the task's own.
    /// </summary>
    public bool Start(NodeDoorStart start, Func<CancellationToken, Task> work, Func<Exception, bool> told)
    {
        if (runs is not null && !runs()) return false;
        var key = start.Key;
        var gate = new TaskCompletionSource();
        if (!_running.TryAdd(key, gate.Task)) return false;
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await StepAuthority.RunAsync(start.Authority, () => work(CancellationToken.None)).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (!told(error)) throw;
                }
                finally
                {
                    _running.TryRemove(key, out _);
                    gate.TrySetResult();
                }
            });
        }

        return true;
    }

    /// <summary>Completes once every work started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => Task.WhenAll(_running.Values);
}
