using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What the roles' judge holds in memory, which its parts reach (<see cref="StandingConfirmation"/>): every member is read and written
/// under <see cref="Gate"/> alone, and every helper here runs under it.
/// </summary>
/// <remarks>
/// <para><b>Who writes what.</b> The roles' terminals (<see cref="RoleTerminals"/>) own <see cref="Holds"/>, <see cref="Holders"/> and
/// <see cref="EndedTabs"/>, which the judge (<see cref="RoleJudge"/>) reads and, for a holder started on another configuration, ends; the
/// judge counts <see cref="Answers"/> and admits into <see cref="Acts"/>. The acts' ledger (<see cref="RoleActsLedger"/>) owns
/// <see cref="OwnerSet"/> and <see cref="OwnerUnresolved"/> and moves <see cref="Acts"/> on; the steward's record
/// (<see cref="StewardRecord"/>) owns <see cref="StewardDone"/>.</para>
/// <para><b>One lock, one mutation step</b> (§2.3, §3.3 point 5): an act is judged and admitted under <see cref="Gate"/>, and every
/// read-modify-write of the memory runs whole under it. It is never held across an await, nor while a decision's lock is taken, nor while
/// a tab is told or closed: those run after the step.</para>
/// </remarks>
internal sealed class StandingState
{
    /// <summary>The one lock every member here is read and written under.</summary>
    public Lock Gate { get; } = new();

    // The roles a terminal holds this run, each with the generation every act under it carries; the holders living this run by their id,
    // each with its key's hash, its tab, its harness's process and the roles ended in it, in memory alone.
    public List<Hold> Holds { get; } = [];

    public Dictionary<string, Holding> Holders { get; } = new(StringComparer.Ordinal);

    public HashSet<string> EndedTabs { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Answers { get; } = new(StringComparer.Ordinal);

    public RoleActsMemory Acts { get; } = new();

    // STEWARDROLE1a-ii (D209 §5.4): what the owner acted on this run, by its scope's key, through whichever door; and whether a target
    // of theirs could not be resolved, which holds every quest for the run. Memory alone: a restart forgets it.
    public HashSet<string> OwnerSet { get; } = new(StringComparer.Ordinal);

    public bool OwnerUnresolved { get; set; }

    // STEWARDROLE1b (D209 §5.1, §5.3): what each of the steward's acts did this run. Memory alone: a restart forgets it.
    public List<StewardDoneAct> StewardDone { get; } = [];

    /// <summary>A role a living holder holds this run: its catalogue id, its kind, the generation drawn at its start, its holder.</summary>
    public sealed record Hold(string Role, string Kind, string Generation, string Holder, DateTimeOffset Since);

    /// <summary>A holder of this run (D209 §2.2): apart from its holds, shared by them or its own.</summary>
    public sealed class Holding(string id, byte[] hash, string? tab, HolderProcess? process, DateTimeOffset since)
    {
        public string Id { get; } = id;
        public byte[] Hash { get; } = hash;
        public string? Tab { get; } = tab;
        public HolderProcess? Process { get; } = process;
        public DateTimeOffset Since { get; } = since;

        /// <summary>The roles ended in this holder this run: never added back to it (§2.2).</summary>
        public HashSet<string> Revoked { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>One of the steward's acts that ran this run, with what it acted on and when.</summary>
    public sealed record StewardDoneAct(string Id, string Act, string? Repository, string? Quest, string? Session, DateTimeOffset At);

    // Under the lock: a holder dropped with every hold it holds.
    public (IReadOnlyList<string> Ended, int Cancelled) EndHolder(Holding holding)
    {
        var generations = Holds.Where(each => each.Holder == holding.Id).Select(each => each.Generation).ToHashSet(StringComparer.Ordinal);
        Holds.RemoveAll(each => each.Holder == holding.Id);
        Holders.Remove(holding.Id);
        return ([.. generations], Acts.Cancel(generations));
    }

    // Under the lock: the holds ended by a role's change or a replacement taken out of memory. Each one's holder marks the role ended in
    // it; a holder left holding nothing goes, its tab told so. Their acts and operations are cancelled.
    public int Revoked(IReadOnlyList<Hold> gone, List<(string Tab, string Line, string? Title)> told)
    {
        var generations = gone.Select(each => each.Generation).ToHashSet(StringComparer.Ordinal);
        Holds.RemoveAll(each => generations.Contains(each.Generation));
        foreach (var each in gone)
        {
            if (!Holders.TryGetValue(each.Holder, out var holding)) continue;
            holding.Revoked.Add(each.Role);
            if (Holds.Any(other => other.Holder == each.Holder)) continue;
            Holders.Remove(each.Holder);
            if (holding.Tab is { } tab) told.Add((tab, HolderTerminal.TurnedOff, null));
        }

        return Acts.Cancel(generations);
    }

    // Under the lock: the kinds of the roles a holder holds now, in the build's order.
    public IReadOnlyList<string> RolesOf(string holder)
    {
        var held = Holds.Where(each => each.Holder == holder).Select(each => each.Kind).ToHashSet(StringComparer.Ordinal);
        return [.. AppointedRoles.Kinds.Where(held.Contains)];
    }

    // Under the lock: whether a generation is a hold a living holder holds now.
    public bool Live(string generation) => Holds.Any(each => each.Generation == generation && Holders.ContainsKey(each.Holder));

    // Under the lock: the live hold of a role by its catalogue id, or null.
    public Hold? HoldOf(string role) => Holds.FirstOrDefault(each => each.Role == role && Holders.ContainsKey(each.Holder));

    // Under the lock: whether the owner acted on the target this run (§5.4).
    public bool OwnerHolds(RoleScope target) =>
        OwnerSet.Contains(target.Key) || (OwnerUnresolved && target.Kind == OwnerKinds.Quest);

    // Under the lock: the steward's last act of the two named on a repository.
    public StewardDoneAct? Latest(string repository, string one, string other) =>
        StewardDone.LastOrDefault(each => each.Repository is { } named && string.Equals(named, repository, StringComparison.OrdinalIgnoreCase)
                                          && (each.Act == one || each.Act == other));

    public static string Count(int count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
