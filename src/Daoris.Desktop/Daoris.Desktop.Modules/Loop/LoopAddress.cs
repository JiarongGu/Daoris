using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The service's address this Daoris leaves in its home for a terminal to find, naming this process and, once served, its pipe
/// (<see cref="DriverLoop.Announce"/>, <see cref="DriverLoop.AnnouncePipe"/>). Written and taken away under one lock with the stop's
/// cancel, so nothing is written once the stop has taken it away.
/// </summary>
internal sealed class LoopAddress(Func<string> home, string serviceUrl, CancellationTokenSource stopping)
{
    private readonly object _gate = new();

    // The address left, or null before the service answered and after the stop.
    private RunningAddress? _address;

    // The pipe served, or null before.
    private string? _pipe;

    /// <summary>The address left once while the loop runs.</summary>
    public void Announce()
    {
        lock (_gate)
        {
            if (_address is not null || stopping.IsCancellationRequested) return;
            _address = RunningAddress.Write(home(), serviceUrl, _pipe);
        }
    }

    /// <summary>
    /// The pipe named in the address. The address is written again whole; the one before it no longer says what it wrote, so only the
    /// new one is taken away at the stop.
    /// </summary>
    public void AnnouncePipe(string pipe)
    {
        lock (_gate)
        {
            if (stopping.IsCancellationRequested) return;
            _pipe = pipe;
            _address = RunningAddress.Write(home(), serviceUrl, pipe) ?? _address;
        }
    }

    /// <summary>The address taken away and the loop's stop begun, together.</summary>
    public void Withdraw()
    {
        lock (_gate)
        {
            _address?.Dispose();
            _address = null;
            stopping.Cancel();
        }
    }
}
