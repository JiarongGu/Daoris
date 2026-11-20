namespace Daoris.Driver;

/// <summary>
/// The facts the looks' judges keep for the plan: what each owed opinion's judgment
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
/// <para><b>A fact stands while what it read stands.</b> Each opinion's judgment keeps, with its fact, a stamp of what it read
/// (<c>OpinionJudgments</c>), and a later look judges it again only where that stamp moved, <see cref="Refresh"/> after it is kept, or
/// past the moment it lasts to. A pass the look begins for it lets the stamp go, as does the end of a pass or a delivery for it, and a
/// judgment that read its stamp before that keeps none; another opinion's pass on the same chain moves its stamp by what it writes.</para>
///
/// <para>Shared across the looks of one watch, as its running sessions are (<see cref="RunningSessions.Facts"/>): the watch builds a
/// driver per look, so a stamp kept by a look's own parts would never reach the next. A stamp lives and goes with the fact it
/// vouches for. A one-look run and a gate's run-until-idle judge in the look and keep nothing here.</para>
/// </remarks>
/// <param name="clock">The clock a stamp ages on, the machine's unless a test moves it by hand.</param>
internal sealed class LookFacts(TimeProvider clock)
{
    /// <summary>How long a judgment's stamp stands before it is judged again whatever moved.</summary>
    internal static readonly TimeSpan Refresh = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly Dictionary<string, IOpinionFact> _opinions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PriorSession?> _askers = new(StringComparer.Ordinal);

    // Each opinion's last judgment's stamp: what it read, the timestamp it is kept at, and the moment it lasts to at most.
    private readonly Dictionary<string, Stamp> _stamps = new(StringComparer.OrdinalIgnoreCase);

    // Each opinion's latest let-go of its stamp, counted by every let-go: a judgment marked before it keeps no stamp.
    private readonly Dictionary<string, long> _letGo = new(StringComparer.OrdinalIgnoreCase);
    private long _letGoes;

    private sealed record Stamp(string Read, long At, DateTimeOffset? Until);

    /// <summary>What the last judgment of <paramref name="due"/> found, or null where none has answered yet.</summary>
    internal IOpinionFact? Opinion(string due)
    {
        lock (_gate) return _opinions.GetValueOrDefault(due);
    }

    /// <summary>Taken by a judgment before it reads its stamp: a let-go after it keeps the judgment's stamp out (<see cref="Stamped"/>).</summary>
    internal long Mark()
    {
        lock (_gate) return _letGoes;
    }

    /// <summary>
    /// Whether <paramref name="due"/>'s last judgment stands at <paramref name="stamp"/>: its fact is kept, nothing it read moved, nothing
    /// let its stamp go, and neither <see cref="Refresh"/> nor the moment it lasts to has passed.
    /// </summary>
    internal bool Stands(string due, string stamp)
    {
        var (now, wall) = (clock.GetTimestamp(), clock.GetUtcNow());
        lock (_gate)
        {
            if (!_opinions.ContainsKey(due) || !_stamps.TryGetValue(due, out var kept) || kept.Read != stamp) return false;
            return clock.GetElapsedTime(kept.At, now) < Refresh && (kept.Until is not { } until || wall < until);
        }
    }

    /// <summary>
    /// A judgment of <paramref name="due"/> made over <paramref name="stamp"/>, lasting at most to the earliest of <paramref name="turns"/>
    /// still ahead, since a moment already past bounds nothing: kept unless a let-go of its stamp came after <paramref name="mark"/>, since
    /// what it read may predate the pass that let it go.
    /// </summary>
    internal void Stamped(string due, string stamp, IEnumerable<DateTimeOffset> turns, long mark)
    {
        var (now, wall) = (clock.GetTimestamp(), clock.GetUtcNow());
        var until = turns.Where(turn => turn > wall).Select(turn => (DateTimeOffset?)turn).Min();
        lock (_gate)
        {
            if (_letGo.GetValueOrDefault(due) > mark) return;
            _stamps[due] = new(stamp, now, until);
        }
    }

    /// <summary>What <paramref name="due"/>'s judgment read may have moved where no stamp sees it: the next look judges it again.</summary>
    internal void Unstamped(string due)
    {
        lock (_gate)
        {
            _stamps.Remove(due);
            _letGo[due] = ++_letGoes;
        }
    }

    /// <summary>
    /// Keep what a judgment of <paramref name="due"/> found. Whether the plan should read it at once: its first, a change in which quests
    /// it sits, or a pass it found owed. One that found the same holds again moves nothing.
    /// </summary>
    internal bool KeepOpinion(string due, IOpinionFact fact)
    {
        lock (_gate)
        {
            var before = _opinions.GetValueOrDefault(due);
            _opinions[due] = fact;
            // A pass found owed that an earlier judgment found and no look has taken (a draining look leaves it kept) moves
            // nothing: its wake is given, and found again at every look it would spin the loop for as long as the update drains.
            return before is null || (fact.Act is not null && before.Act is null) || !before.SatQuests.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(fact.SatQuests);
        }
    }

    /// <summary>The pass <paramref name="due"/>'s last judgment found owed, taken once: one look begins it, or finds its claim held.</summary>
    internal IOpinionAct? TakeAct(string due)
    {
        lock (_gate)
        {
            if (_opinions.GetValueOrDefault(due) is not { Act: { } act } fact) return null;
            _opinions[due] = fact.WithoutAct();
            return act;
        }
    }

    /// <summary>The pass <paramref name="due"/>'s last judgment found owed, left kept: a draining look reads it and begins nothing.</summary>
    internal IOpinionAct? PeekAct(string due)
    {
        lock (_gate) return _opinions.GetValueOrDefault(due)?.Act;
    }

    /// <summary>Only the opinions still owed are kept, with their stamps: one closed since its last judgment is let go.</summary>
    internal void KeepOpinionsOf(IEnumerable<string> owed)
    {
        var open = owed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var gone in _opinions.Keys.Where(due => !open.Contains(due)).ToList()) _opinions.Remove(gone);
            foreach (var gone in _stamps.Keys.Where(due => !open.Contains(due)).ToList()) _stamps.Remove(gone);
            foreach (var gone in _letGo.Keys.Where(due => !open.Contains(due)).ToList()) _letGo.Remove(gone);
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

    // The subjects whose last judgment failed, said once.
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

    /// <summary><paramref name="subject"/> is judged: a later failure is said again.</summary>
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
/// What one judgment of an owed opinion found, as the looks keep it for the plan: the quests it sits and the pass it
/// found owed. The review's <c>OpinionFact</c> implements it, so the facts name no more of the opinion than they read.
/// </summary>
internal interface IOpinionFact
{
    /// <summary>The quests it sits (<see cref="Snapshot.Opinions"/>).</summary>
    IEnumerable<string> SatQuests { get; }

    /// <summary>The pass it found owed, for a look to begin; null where none.</summary>
    IOpinionAct? Act { get; }

    /// <summary>The same finding with its pass taken.</summary>
    IOpinionFact WithoutAct();
}

/// <summary>A pass an owed opinion's judgment found owed, which a look takes once and begins; the review's <c>OpinionAct</c> implements it.</summary>
internal interface IOpinionAct;
