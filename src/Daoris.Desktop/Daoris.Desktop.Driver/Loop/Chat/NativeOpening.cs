namespace Daoris.Driver;

/// <summary>
/// A native-door conversation's opening held to its bound: its harness is given no words while a conversation is opened ahead, so the bound
/// runs from the first word written to the first thing the harness says. Past it the opening fails with <see cref="NotUp"/> said, and the
/// word stays kept to continue.
/// </summary>
internal sealed class NativeOpening(TimeSpan bound, Func<Noted> notUp, Action<Noted> pastBound) : IReadingSink, IDisposable
{
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _begun;
    private bool _answered;

    /// <summary>What the opening ended past its bound says, or null while it has not.</summary>
    public Noted? NotUp { get; private set; }

    bool IReadingSink.Watched => false;

    /// <summary>The first word is on the harness's stdin: its bound runs, once.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            if (_begun || _answered) return;
            _begun = true;
            _timer = new Timer(_ => Pass(), null, bound, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The harness said something: it is up.</summary>
    void IReadingSink.Read(WireReading reading) => Answered();

    public void Answered()
    {
        lock (_gate)
        {
            _answered = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Pass()
    {
        Noted said;
        lock (_gate)
        {
            if (_answered) return;
            said = NotUp = notUp();
        }

        pastBound(said);
    }

    public void Dispose() => Answered();
}
