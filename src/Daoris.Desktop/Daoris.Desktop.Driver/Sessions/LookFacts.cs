namespace Daoris.Driver;

/// <summary>
/// The facts the looks' judges keep for the plan (PARALLEL1g, D181 point 3; the events design §A3): what each owed opinion's judgment
/// found (the quests it sits, the gate the attention band reads, and a pass it found owed), and each ask for a helper's asker as its
/// judgment read it. Under the watch these judgments run beside the look, one per subject, and the look reads them as last judged,
/// so a judgment that does not answer holds its own subject alone.
/// </summary>
/// <remarks>
/// <para><b>Not judged yet is a hold.</b> A subject no judgment has answered for has no fact here: the look holds what it could hold
/// (an opinion's chain, an ask that begins nothing) until one lands, never reading the absence as a let-go.</para>
///
/// <para><b>What takes a slot is begun by the look.</b> A pass a judgment found owed is kept here as its act, which one look takes and
/// begins before its plan, so the passes are counted under the cap as they were; a delivery takes no slot, and its judgment begins it.</para>
///
/// <para>Shared across the looks of one watch, as its running sessions are (<see cref="RunningSessions.Facts"/>). A one-look run and a
/// gate's run-until-idle judge in the look and keep nothing here.</para>
/// </remarks>
internal sealed class LookFacts
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OpinionFact> _opinions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PriorSession?> _askers = new(StringComparer.Ordinal);

    /// <summary>What the last judgment of <paramref name="due"/> found, or null where none has answered yet.</summary>
    internal OpinionFact? Opinion(string due)
    {
        lock (_gate) return _opinions.GetValueOrDefault(due);
    }

    /// <summary>
    /// Keep what a judgment of <paramref name="due"/> found. Whether the plan should read it at once: its first, a change in which quests
    /// it sits, or a pass it found owed. One that found the same holds again moves nothing.
    /// </summary>
    internal bool KeepOpinion(string due, OpinionFact fact)
    {
        lock (_gate)
        {
            var before = _opinions.GetValueOrDefault(due);
            _opinions[due] = fact;
            return before is null || fact.Act is not null || !before.Sits.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(fact.Sits.Keys);
        }
    }

    /// <summary>The pass <paramref name="due"/>'s last judgment found owed, taken once: one look begins it, or finds its claim held.</summary>
    internal OpinionAct? TakeAct(string due)
    {
        lock (_gate)
        {
            if (_opinions.GetValueOrDefault(due) is not { Act: { } act } fact) return null;
            _opinions[due] = fact with { Act = null };
            return act;
        }
    }

    /// <summary>Only the opinions still owed are kept: one closed since its last judgment is let go.</summary>
    internal void KeepOpinionsOf(IEnumerable<string> owed)
    {
        var open = owed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var gone in _opinions.Keys.Where(due => !open.Contains(due)).ToList()) _opinions.Remove(gone);
        }
    }

    /// <summary>The asker's record of <paramref name="ask"/> as its judgment read it (null where none is on record); false where none has yet.</summary>
    internal bool TryAsker(string ask, out PriorSession? asker)
    {
        lock (_gate) return _askers.TryGetValue(ask, out asker);
    }

    /// <summary>
    /// Keep the asker's record a judgment of <paramref name="ask"/> read. Whether it is the first: what the verdict reads of it (whether it is
    /// on record, a teammate's, a pass's, an ask's) does not move, so a later one moves nothing.
    /// </summary>
    internal bool KeepAsker(string ask, PriorSession? asker)
    {
        lock (_gate) return _askers.TryAdd(ask, asker);
    }

    // The subjects whose last judgment failed, said once (PARALLEL1g).
    private readonly HashSet<string> _failing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a judgment's failure is said: once, until <paramref name="subject"/> is judged again. A line beside the look wakes the next
    /// one, which asks the judgment again; one that keeps failing (git refusing a tree, a door that answers wrongly) would spin the loop.
    /// Said once, it waits for whatever wakes the look next.
    /// </summary>
    internal bool Failed(string subject)
    {
        lock (_gate) return _failing.Add(subject);
    }

    /// <summary><paramref name="subject"/> was judged: a later failure is said again.</summary>
    internal void Judged(string subject)
    {
        lock (_gate) _failing.Remove(subject);
    }

    /// <summary>Only the asks still on file are kept.</summary>
    internal void KeepAsksOf(IEnumerable<string> asks)
    {
        var on = asks.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var gone in _askers.Keys.Where(ask => !on.Contains(ask)).ToList()) _askers.Remove(gone);
        }
    }
}

/// <summary>
/// What one judgment of an owed opinion found (PARALLEL1g): the quests it sits, each with its sentence (<see cref="Snapshot.Opinions"/>);
/// the landing opinion's gate, as the attention band reads it (XAGENT1g3); and the pass it found owed, for a look to begin.
/// </summary>
internal sealed record OpinionFact(IReadOnlyDictionary<string, OpinionSit> Sits, OpinionJudged? Judged, OpinionAct? Act);

/// <summary>
/// A pass an owed opinion's judgment found owed (PARALLEL1g): the look's own ask at <paramref name="Occasion"/>, or the person's
/// (<paramref name="Person"/>, taken from the gate as it is begun), under the chain's claim, with what its ask reads.
/// </summary>
internal sealed record OpinionAct(
    OpinionDue Due, string Key, string Claim, IReadOnlyList<QuestView> Chain, string Occasion, bool Person, WorkflowProcess Process);
