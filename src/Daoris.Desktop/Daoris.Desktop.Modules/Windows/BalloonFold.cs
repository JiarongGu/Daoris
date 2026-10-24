namespace Daoris.Desktop;

/// <summary>
/// The balloon's own coalescing of a terminal's presses (CONFIRMWAIT1a-i, the confirm-wait design §8.2): at most one balloon a minute
/// across every press. The first ask of a quiet minute is shown at once, by its act; the asks after it in that minute are folded into
/// one balloon at the minute's end, naming how many came in it (<see cref="FoldedKind"/>), and that balloon begins the next minute. Every
/// other kind passes through. The press's <c>SESSION_ATTENTION</c> event stays one per press, since the page's toast and the window's
/// announcement are that event's (D196 point 4): the balloon coalesces, never the event.
/// </summary>
public sealed class BalloonFold(TimeProvider clock, Action<BalloonFacts> show) : IDisposable
{
    /// <summary>The folded balloon's kind: several presses from a terminal wait, <see cref="BalloonFacts.Count"/> of them.</summary>
    public const string FoldedKind = "ConfirmingMany";

    /// <summary>How long a minute is.</summary>
    public static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private ITimer? _timer;
    private int _asked;
    private int _folded;

    /// <summary>One event's balloon: shown now, or folded into the minute's.</summary>
    public void Offer(BalloonFacts facts)
    {
        if (facts.Kind != PipeConfirmations.AttentionKind)
        {
            show(facts);
            return;
        }

        lock (_gate)
        {
            if (_timer is not null)
            {
                _asked++;
                _folded++;
                return;
            }

            _asked = 1;
            _folded = 0;
            _timer = clock.CreateTimer(_ => Close(), null, Minute, Timeout.InfiniteTimeSpan);
        }

        show(facts);
    }

    /// <summary>The minute's end: its folded asks said in one balloon, which begins the next minute; a quiet minute ends the fold.</summary>
    private void Close()
    {
        int count;
        lock (_gate)
        {
            if (_folded == 0)
            {
                _timer?.Dispose();
                _timer = null;
                return;
            }

            count = _asked;
            _asked = 0;
            _folded = 0;
            _timer?.Change(Minute, Timeout.InfiniteTimeSpan);
        }

        show(new BalloonFacts(FoldedKind) { Count = count });
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
