namespace Daoris.Driver;

/// <summary>
/// A hold today's gates stand at, as a hold point reads it: its key (stable while it stands), today's name for it, the act it is, who answers
/// it today, its place, its subject, and what today's gate does (<see cref="Today"/>, and whether that holds the work).
/// </summary>
public sealed record RiskHold(string Key, string Hold, RiskAct Act, string Actor, RiskPlace Place, string Subject, string Today, bool TodayHeld)
{
    public string? Run { get; init; }

    public string? Step { get; init; }
}

/// <summary>
/// The judge in shadow: beside each hold today's gates open and release, the verdict the judge would give is
/// journaled (<see cref="RiskJournal"/>) with what today's gate did, deciding nothing and changing no behaviour. A hold point hands the holds
/// standing now in its domain; a hold first seen is journaled at its open, one gone at its release, read again under the policy then, so a
/// policy changed between them is read again. What the journal holds open survives a restart. A fault of its own is swallowed: it decides
/// nothing, so it never fails the look it rides on.
/// </summary>
public sealed class RiskShadow(string home, Func<DateTimeOffset> clock)
{
    private readonly Dictionary<string, RiskJudged> _open = new(StringComparer.Ordinal);

    private readonly object _one = new();

    private bool _read;

    /// <summary>
    /// The holds of <paramref name="domain"/> standing now: each new one journaled at its open, each gone journaled at its release with what
    /// today did then (<paramref name="released"/>, by key).
    /// </summary>
    public void Look(string domain, RiskPolicy policy, IReadOnlyList<RiskHold> holds, Func<string, string> released)
    {
        try
        {
            lock (_one)
            {
                if (!_read) Seed();
                var now = holds.Where(hold => hold.Key.StartsWith(domain + ":", StringComparison.Ordinal))
                    .GroupBy(hold => hold.Key, StringComparer.Ordinal).Select(each => each.Last()).ToList();
                foreach (var hold in now.Where(hold => !_open.ContainsKey(hold.Key)))
                {
                    var line = Judged(RiskJudged.Opened, policy, hold);
                    RiskJournal.Append(home, line);
                    _open[hold.Key] = line;
                }

                var standing = now.Select(hold => hold.Key).ToHashSet(StringComparer.Ordinal);
                foreach (var gone in _open.Keys.Where(key => key.StartsWith(domain + ":", StringComparison.Ordinal) && !standing.Contains(key)).ToList())
                {
                    var opened = _open[gone];
                    var place = new RiskPlace(opened.Workspace, opened.Repository, opened.Mode == RiskPlace.FullMode);
                    var act = new RiskAct(opened.Kind, opened.Qualifier);
                    var hold = new RiskHold(gone, opened.Hold, act, opened.Actor, place, opened.Subject, released(gone), opened.TodayHeld)
                    {
                        Run = opened.Run, Step = opened.Step,
                    };
                    RiskJournal.Append(home, Judged(RiskJudged.Released, policy, hold));
                    _open.Remove(gone);
                }
            }
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            Faults++;
        }
    }

    /// <summary>How many looks a fault of the shadow's own passed over, for a test and the logs.</summary>
    public int Faults { get; private set; }

    private RiskJudged Judged(string stage, RiskPolicy policy, RiskHold hold)
    {
        var verdict = RiskGate.Judge(policy, hold.Act, hold.Actor, hold.Place, hold.Subject);
        return new RiskJudged(
            clock(), stage, hold.Key, hold.Hold, verdict.Kind, verdict.Qualifier, hold.Actor, hold.Place.Workspace, hold.Place.Repository, hold.Place.Mode,
            hold.Run, hold.Step, hold.Subject, verdict.Outcome, verdict.Class, verdict.By, verdict.Source, verdict.Revision, verdict.Reason, hold.Today,
            hold.TodayHeld);
    }

    /// <summary>The holds the journal leaves open: each key whose last line is its open.</summary>
    private void Seed()
    {
        _read = true;
        foreach (var line in RiskJournal.Read(home))
        {
            if (line.Stage == RiskJudged.Opened) _open[line.Key] = line;
            else _open.Remove(line.Key);
        }
    }
}
