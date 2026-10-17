namespace Daoris.Driver;

/// <summary>
/// One unit link 2 is pending for (FABRIC-END4a): its id, its repository, the branches of it still standing that the line holds, the
/// tree its session branch has here (null where none stands), and when the line was first found holding it.
/// </summary>
public sealed record TidyUnit(string Unit, string Repository, string Workspace, IReadOnlyList<string> Branches, string? Tree, DateTimeOffset Since);

/// <summary>
/// The look's automatic tidy as the chain's link 2 (FABRIC-END4a; the fabric events design §C3, §C6 and §D3): an effect, pending while a
/// unit's end is held on the line and a branch of it still stands, tried when link 1 finds a repository's facts changed, again on the
/// wakes that can unblock it (a session leaving its tree or ending, a press, the home's files, the <c>refs</c> deadline), and found
/// again at the start from the persisted facts (<see cref="Chain.RecoverAsync"/>).
/// </summary>
/// <remarks>
/// <para><b>The chain's subject is the repository</b> link 1 names; what is acted on is that repository's held units. A try is the
/// repository's tidy (<see cref="SessionTrees.TidyEmptyAsync"/>), whose candidates are the kept facts alone, judged again inside its hold
/// and once more right before each goes, with every guard (D88). A try that removes nothing is a refusal, never an outcome: the unit
/// stays pending.</para>
///
/// <para><b>Pending is read from the inventory</b> (<see cref="WorkInventory.Derive"/>) over the records alone, with no git: a unit whose
/// end is held, with a session or landing branch the records say still stands. A unit no record names is never ended, so it is never
/// pending: an unknown owner blocks (§D3, canary 27). An empty branch whose tree stands is a conversation's place (D137), the press's, and
/// is not pending either, so no wake retries it for nothing.</para>
///
/// <para><b>A busy unit costs no git</b> (canary 17): where every pending unit's tree is one a session running or waiting holds, the
/// try is refused before the tidy starts, and the next unblocking wake tries it again.</para>
///
/// <para><b>Dispositions are the records'</b> (§D3, MILESTONE1a): a removal leaves its entry a trace with <c>GoneAt</c> and its kept
/// fact, which the inventory reads as <i>removed</i> with its proof; a guard that keeps a held session branch is written on its entry
/// as <see cref="GrownBranch.Blocked"/>, which the inventory reads as <i>blocked</i> with why. A branch's removal never retires its unit:
/// retirement stays derived at read (§D3, canary 35).</para>
/// </remarks>
public sealed class TidyEffect : IChainEffect
{
    private readonly string _home;
    private readonly Func<string, CancellationToken, Task<RefRepository?>> _where;
    private readonly Func<CancellationToken, Task<IReadOnlySet<string>>> _inUse;
    private readonly Func<RefRepository, Func<CancellationToken, Task<IReadOnlySet<string>>>, CancellationToken, Task<TidyPass>> _tidy;

    /// <param name="where">A repository's checkout by its name, as the ref watch watches it; null where it has none here now.</param>
    /// <param name="inUse">The trees sessions running or waiting hold, a review step's included (<see cref="BranchTidying.InUseAsync"/>).</param>
    /// <param name="tidy">One repository's tidy; <see cref="SessionTrees.TidyEmptyAsync"/> unless a test hands its own.</param>
    public TidyEffect(
        string home, Func<string, CancellationToken, Task<RefRepository?>> where, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse,
        Func<RefRepository, Func<CancellationToken, Task<IReadOnlySet<string>>>, CancellationToken, Task<TidyPass>>? tidy = null)
    {
        _home = home;
        _where = where;
        _inUse = inUse;
        _tidy = tidy ?? ((repository, busy, ct) => new SessionTrees(home).TidyEmptyAsync(repository.Root, repository.Repository, repository.Workspace, busy, ct));
    }

    /// <summary>What says a try's removals and keeps, once each, in the report's words and the machine log: the look's own tidy's.</summary>
    public BranchTidying? Saying { get; init; }

    /// <summary>Where <see cref="Saying"/> writes; none by default.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>A try's lines, for the next look's report; none by default.</summary>
    public Action<IReadOnlyList<string>>? Said { get; init; }

    /// <summary>What time it is; the system's by default.</summary>
    internal Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// Every unit the tidy is pending for, read from the records through the inventory with no git: its end held on the line, a branch
    /// of it standing, and not an empty branch whose tree stands (D137).
    /// </summary>
    public static IReadOnlyList<TidyUnit> Pending(string home, DateTimeOffset now)
    {
        var grown = new SessionBranches(home).Entries();
        var facts = new InventoryFacts(
            grown, new LandedBranches(home).Entries(), [], new AutoLandings(home).All(), [], new HashSet<string>(), _ => false, now);
        var treesRoot = new SessionTrees(home).TreesRoot;
        var pending = new List<TidyUnit>();
        foreach (var unit in WorkInventory.Derive(facts))
        {
            // Nothing goes before *held* (canary 15); an unknown owner is never ended, so never pending (canary 27).
            if (unit.End.State != "ended") continue;
            var standing = unit.Entries
                .Where(entry => entry.Kind is InventoryKind.SessionBranch or InventoryKind.LandingBranch && entry.Role == InventoryRole.Temporary
                    && entry.Disposition is InventoryDisposition.Pending or InventoryDisposition.Blocked)
                .ToList();
            if (standing.Count == 0) continue;

            var branch = grown.LastOrDefault(entry => entry.GoneAt is null && entry.Branch == unit.Branch
                && string.Equals(entry.Repository, unit.Repository, StringComparison.OrdinalIgnoreCase));
            var tree = branch is { Branch: var name } && name.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
                ? Path.GetFullPath(Path.Combine(treesRoot, branch.Workspace, branch.Repository, name[TreeFollows.SessionPrefix.Length..]))
                : null;
            if (tree is not null && !Directory.Exists(tree)) tree = null;
            // D137: a tree whose branch never moved is a conversation's place, which the look leaves for the press.
            var conversation = branch?.Held?.By == EndBy.Empty && tree is not null;
            var branches = standing.Where(entry => !(conversation && entry.Kind == InventoryKind.SessionBranch)).Select(entry => entry.Resource).ToList();
            if (branches.Count == 0) continue;
            pending.Add(new TidyUnit(
                unit.Unit, unit.Repository, branch?.Workspace ?? "default", branches, tree, unit.End.At ?? now));
        }

        return pending;
    }

    /// <summary>The repositories link 2 is pending for, by the name link 1 emits: each tried once per wake.</summary>
    public Task<IReadOnlyList<ChainPending>> PendingAsync(CancellationToken ct)
    {
        IReadOnlyList<ChainPending> repositories =
        [
            .. Pending(_home, Clock()).GroupBy(unit => unit.Repository, StringComparer.OrdinalIgnoreCase).Select(same => new ChainPending(same.Key)),
        ];
        return Task.FromResult(repositories);
    }

    /// <summary>
    /// One try of a repository's pending units: refused with no git where a session holds every one's tree; else the repository's tidy,
    /// said and its keeps written. Done once something went, emitting <c>work.tidied</c>; refused where nothing did.
    /// </summary>
    public Task<ChainTry> TryAsync(string unit, CancellationToken ct) => TryAsync(unit, due: false, ct);

    /// <inheritdoc cref="TryAsync(string, CancellationToken)"/>
    /// <param name="due">The <c>refs</c> deadline woke it: a real try, whatever the settle says (FABRIC-END4a, second review).</param>
    public async Task<ChainTry> TryAsync(string unit, bool due, CancellationToken ct)
    {
        var began = Clock();
        var units = Pending(_home, began).Where(each => string.Equals(each.Repository, unit, StringComparison.OrdinalIgnoreCase)).ToList();
        if (units.Count == 0)
        {
            lock (_settled) _settled.Remove(unit);
            return ChainTry.Done();
        }

        if (await _where(unit, ct).ConfigureAwait(false) is not { } repository) return ChainTry.Refused;

        // A refusal settles the repository (FABRIC-END4a review): while its units, their tips, what kept them, which trees sessions hold
        // and those trees' own state are as they were, a wake tries nothing, so a burst of wakes over the three unblocking topics is one
        // try, and an unchanged blocker starts no git. 🔴 Never for good (second review): what clears a blocker need not show in what it
        // reads (a file reverted in place, a lock let go), so the settle lasts at most the `refs` deadline from the last real try's start,
        // the deadline's own wake always tries, and a settled answer is no try, so it never moves the chain's deadline.
        var busy = await _inUse(ct).ConfigureAwait(false);
        var signature = Signature(units, busy);
        lock (_settled)
        {
            if (!due && _settled.TryGetValue(unit, out var settled) && settled.Signature == signature && began - settled.At < Settle) return ChainTry.Settled;
        }

        var held = units.Where(each => each.Tree is { } tree && Holds(busy, tree)).ToList();
        if (held.Count == units.Count)
        {
            Block(held, SweepKind.InUse, "a session running or waiting holds its tree");
            Settled(unit, busy, began);
            return ChainTry.Refused;
        }

        var pass = await _tidy(repository, _inUse, ct).ConfigureAwait(false);
        var lines = Saying?.Said([pass], Log, whole: false) ?? [];
        if (lines.Count > 0) Said?.Invoke(lines);
        Kept(units, pass);
        if (pass.Results.Any(result => result.Removed) || pass.Landed.Any(result => result.Removed))
        {
            lock (_settled) _settled.Remove(unit);
            return ChainTry.Done(new ChainEvent(ChainEvents.WorkTidied, repository.Repository, ChangeTopics.Refs));
        }

        Settled(unit, busy, began);
        return ChainTry.Refused;
    }

    /// <summary>How long a refused repository stays settled while nothing it rests on changed, from its last real try's start: the <c>refs</c> deadline.</summary>
    public TimeSpan Settle { get; init; } = RefWatch.Deadline;

    // What each refused repository rested on, and when its try began: in memory, as the chain's own tries are; a restart tries each once.
    private readonly Dictionary<string, (string Signature, DateTimeOffset At)> _settled = new(StringComparer.OrdinalIgnoreCase);

    private void Settled(string repository, IReadOnlySet<string> busy, DateTimeOffset began)
    {
        // Read after the try's own writes (a `blocked` written), so the next wake finds the same.
        var signature = Signature(Pending(_home, Clock()).Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)).ToList(), busy);
        lock (_settled) _settled[repository] = (signature, began);
    }

    /// <summary>What a refusal rests on, with no git: each unit's branches, its kept tip, what kept it, whether a session holds its tree, and the tree's own last writes.</summary>
    private string Signature(IReadOnlyList<TidyUnit> units, IReadOnlySet<string> busy)
    {
        var grown = new SessionBranches(_home).All();
        return string.Join("\n", units.OrderBy(each => each.Unit, StringComparer.Ordinal).Select(each =>
        {
            var branch = grown.LastOrDefault(entry => entry.Unit == each.Unit || each.Branches.Contains(entry.Branch, StringComparer.Ordinal));
            return string.Join("|",
                each.Unit, string.Join(",", each.Branches), branch?.Held?.Tip, branch?.Held?.Line, branch?.Blocked?.Why, branch?.Blocked?.Reason,
                each.Tree is { } tree && Holds(busy, tree), each.Tree is { } at ? TreeState(at) : "");
        }));
    }

    /// <summary>A tree's own state without git: when its folder and its index were last written.</summary>
    private static string TreeState(string tree)
    {
        try
        {
            var dot = Path.Combine(tree, ".git");
            var index = File.Exists(dot) && File.ReadAllText(dot).Trim() is var text && text.StartsWith("gitdir:", StringComparison.Ordinal)
                ? Path.Combine(Path.GetFullPath(text["gitdir:".Length..].Trim(), tree), "index")
                : Path.Combine(dot, "index");
            return $"{Directory.GetLastWriteTimeUtc(tree).Ticks}:{(File.Exists(index) ? File.GetLastWriteTimeUtc(index).Ticks : 0)}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "unread";
        }
    }

    private static bool Holds(IReadOnlySet<string> busy, string tree) =>
        busy.Any(path => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), Path.TrimEndingDirectorySeparator(tree), StringComparison.OrdinalIgnoreCase));

    /// <summary>The guard that kept each held session branch the tidy judged, written on its entry; a whole repository held, on each unit.</summary>
    private void Kept(IReadOnlyList<TidyUnit> units, TidyPass pass)
    {
        if (pass.Held is { } whole)
        {
            Block(units, whole, pass.Detail);
            return;
        }

        foreach (var result in pass.Results.Where(result => !result.Removed))
        {
            var why = result.Kept ?? (result.Item.Unread ? TidyKept.Unread : result.Item.Kind);
            // The sentence the tidy kept it with, beside its code (second review), so What needs you says why, not only which guard.
            Block([.. units.Where(each => each.Branches.Contains(result.Item.Branch, StringComparer.Ordinal))], why, result.Message, result.Item.Branch);
        }
    }

    private void Block(IReadOnlyList<TidyUnit> units, string why, string? reason, string? only = null)
    {
        var records = new SessionBranches(_home);
        foreach (var each in units)
        {
            foreach (var branch in each.Branches.Where(branch => branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal) && (only is null || branch == only)))
            {
                records.Block(each.Repository, branch, why, reason, Clock());
            }
        }
    }
}
