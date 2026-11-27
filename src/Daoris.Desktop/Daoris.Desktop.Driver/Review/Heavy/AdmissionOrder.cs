namespace Daoris.Driver;

/// <summary>
/// The order the runs of one queue take their turns in when they wait for the machine's one admission (<see cref="CheckAdmission"/>). Each
/// waiting run joins and is ordered by the one comparator (<see cref="QueueOrder"/>), read at each look and never kept. Only the run whose
/// turn it is asks the admission, so a freed slot goes to the first of the order and nothing running is cut.
/// </summary>
public sealed class AdmissionOrder
{
    private readonly Lock _gate = new();
    private readonly List<AdmissionTurn> _turns = [];
    private long _joined;

    /// <summary>
    /// Whether a refusal ends a pass over the runs in order: every slot running or the admission's lock held is the same wait for each run
    /// behind, so none tries; memory, the reserve and a repository's slots are one run's own, and the next may try.
    /// </summary>
    public static bool EndsPass(CheckWait? refused) => refused is { Code: CheckWait.Slots or CheckWait.Admitting };

    /// <summary>Join the order; the turn holds its place until it is disposed. <paramref name="place"/> is read at each look.</summary>
    public AdmissionTurn Join(string id, Func<QueuePlace> place)
    {
        lock (_gate)
        {
            var turn = new AdmissionTurn(this, id, place, ++_joined);
            _turns.Add(turn);
            return turn;
        }
    }

    internal bool MayTry(AdmissionTurn turn, out CheckWait? held)
    {
        lock (_gate)
        {
            held = null;
            foreach (var each in QueueOrder.Of(_turns, one => one.Place()).ThenBy(one => one.Joined))
            {
                if (each == turn) return true;

                // A run ahead that has not looked yet may take what is free, so the runs behind wait for its look.
                if (!each.Looked) return false;
                if (EndsPass(each.Last))
                {
                    held = each.Last;
                    return false;
                }
            }

            return false;
        }
    }

    internal void Leave(AdmissionTurn turn)
    {
        lock (_gate) _turns.Remove(turn);
    }

    internal void Told(AdmissionTurn turn, CheckWait? wait)
    {
        lock (_gate)
        {
            turn.Looked = true;
            turn.Last = wait;
        }
    }
}

/// <summary>
/// One waiting run's place in an <see cref="AdmissionOrder"/>: <see cref="MayTry"/> says whether the admission is the run's to ask now,
/// <see cref="Refused"/> tells why it is not given, <see cref="Admitted"/> that it is, which ends the wait.
/// </summary>
public sealed class AdmissionTurn : IDisposable
{
    private readonly AdmissionOrder _order;

    internal AdmissionTurn(AdmissionOrder order, string id, Func<QueuePlace> place, long joined)
    {
        _order = order;
        Id = id;
        Place = place;
        Joined = joined;
    }

    public string Id { get; }

    internal Func<QueuePlace> Place { get; }

    internal long Joined { get; }

    internal bool Looked { get; set; }

    internal CheckWait? Last { get; set; }

    /// <summary>Called with the wait the run is told, whether the admission refused it or a run ahead holds it back.</summary>
    public Action<CheckWait>? WhenRefused { get; set; }

    /// <summary>Called when the admission gives the run its slots.</summary>
    public Action? WhenAdmitted { get; set; }

    /// <summary>Whether the admission is this run's to ask; where not, <paramref name="held"/> is the wait of the run ahead, null before it has looked.</summary>
    public bool MayTry(out CheckWait? held) => _order.MayTry(this, out held);

    /// <summary>The admission refused the run for <paramref name="wait"/>.</summary>
    public void Refused(CheckWait wait)
    {
        _order.Told(this, wait);
        WhenRefused?.Invoke(wait);
    }

    /// <summary>The run asked nothing, for a run ahead is refused for <paramref name="wait"/>: its wait too.</summary>
    public void Waits(CheckWait wait) => WhenRefused?.Invoke(wait);

    /// <summary>The admission gave the run its slots: it leaves the order, so the next has its turn.</summary>
    public void Admitted()
    {
        _order.Leave(this);
        WhenAdmitted?.Invoke();
    }

    public void Dispose() => _order.Leave(this);
}
