using System.ComponentModel;

namespace Daoris.Desktop;

/// <summary>The browser's process as its bring-up holds it: whether it still runs, how it ended, a wait for its end, and its end.</summary>
public interface IBrowserChild
{
    bool HasExited { get; }

    int ExitCode { get; }

    Task WaitForExitAsync(CancellationToken ct);

    /// <summary>End the process and everything it started.</summary>
    void End();
}

/// <summary>
/// Daoris's own browser brought up: started, waited for until it shows a window, and kept for the shell's life. The one process it
/// holds is ended wherever the bring-up does not end with a window (its time out, its cancelling, a last window that will not go), so a
/// press after a failed one starts a browser and never waits on the one the failure left. The caller holds its start gate through a call.
/// </summary>
/// <param name="start">Starts the process, joined to the app's job; a refusal of the system is <see cref="Win32Exception"/>.</param>
/// <param name="windowOpen">Whether the engine shows a window.</param>
/// <param name="bringForward">Brings the first window in front of the person.</param>
/// <param name="exitHint">Said after a browser that stopped as it started: where to read why.</param>
/// <param name="limit">How long a started browser is given to show its window.</param>
/// <param name="going">How long a browser whose last window just closed is given to finish going.</param>
/// <param name="beforeStart">Run while no browser runs here, before the next start (a cache over its limit cleared).</param>
public sealed class EngineBringUp(
    Func<IBrowserChild> start,
    Func<CancellationToken, Task<bool>> windowOpen,
    Func<CancellationToken, Task> bringForward,
    string exitHint,
    TimeSpan limit,
    TimeSpan going,
    Func<CancellationToken, Task>? beforeStart = null)
{
    private IBrowserChild? _child;

    /// <summary>Whether a browser this holds still runs.</summary>
    public bool Running => _child is { HasExited: false };

    /// <summary>A browser with a window: the one running, brought forward where <paramref name="activate"/>, else a new one.</summary>
    /// <exception cref="InvalidOperationException">The system would not start it, or it stopped as it started.</exception>
    /// <exception cref="TimeoutException">It showed no window in its time; it is ended.</exception>
    public async Task EnsureAsync(bool activate, CancellationToken ct)
    {
        if (_child is { HasExited: false } running)
        {
            if (await windowOpen(ct).ConfigureAwait(false))
            {
                if (activate) await bringForward(ct).ConfigureAwait(false);
                return;
            }

            // Its last window closed a moment ago and it is on its way out: let it go, then start again, rather than
            // ask a closing engine for a window; one that will not go is ended.
            try
            {
                await running.WaitForExitAsync(ct).WaitAsync(going, ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Forget(running);
            }
        }

        if (beforeStart is not null) await beforeStart(ct).ConfigureAwait(false);
        IBrowserChild child;
        try
        {
            child = _child = start();
        }
        catch (Win32Exception error)
        {
            throw new InvalidOperationException($"Daoris's browser did not start: {error.Message}", error);
        }

        try
        {
            var deadline = DateTime.UtcNow + limit;
            while (DateTime.UtcNow < deadline)
            {
                if (child.HasExited)
                {
                    throw new InvalidOperationException($"Daoris's browser stopped as it started (exit {child.ExitCode}). {exitHint}");
                }

                if (await windowOpen(ct).ConfigureAwait(false)) return;
                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            throw new TimeoutException($"Daoris's browser opened no window within {limit.TotalSeconds:0} seconds.");
        }
        catch
        {
            Forget(child);
            throw;
        }
    }

    private void Forget(IBrowserChild child)
    {
        if (!child.HasExited) child.End();
        if (ReferenceEquals(_child, child)) _child = null;
    }
}
