namespace Daoris.Desktop;

/// <summary>
/// The machine's resume from sleep, as the shell hears it (FABRIC-EVENT2d, the design §A4.1): the desktop raises it from the
/// system's power event, a test raises it by hand, and a loop's change watch reopens its roots and judges them. The watch's own
/// overrun rule stays behind it as the fallback where no power event was heard.
/// </summary>
/// <remarks>In Modules, which takes no <c>SystemEvents</c>: the shell's window process owns that hook and calls <see cref="Raise"/>.</remarks>
public sealed class PowerResume
{
    private readonly object _gate = new();
    private readonly List<Action> _listeners = [];

    /// <summary>Tell <paramref name="listener"/> at each resume until the returned handle is disposed.</summary>
    public IDisposable Hear(Action listener)
    {
        lock (_gate) _listeners.Add(listener);
        return new Hearing(this, listener);
    }

    /// <summary>The machine resumed. One listener that fails does not keep the rest from hearing it.</summary>
    public void Raise()
    {
        Action[] heard;
        lock (_gate) heard = [.. _listeners];
        foreach (var listener in heard)
        {
            try
            {
                listener();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A listener's failure is its own: the resume still reaches the others, and a missed one costs a wait.
            }
        }
    }

    private sealed class Hearing(PowerResume power, Action listener) : IDisposable
    {
        public void Dispose()
        {
            lock (power._gate) power._listeners.Remove(listener);
        }
    }
}
