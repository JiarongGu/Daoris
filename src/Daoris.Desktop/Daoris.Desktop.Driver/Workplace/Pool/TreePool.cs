namespace Daoris.Driver;

/// <summary>
/// The kept slots' record in the desktop store (the family <i>Kept trees</i>): which slots a repository's pool has and which line each
/// serves, the one claim in flight on a slot, the terminals and served builds in it, and each let-go of a line.
/// </summary>
/// <remarks>
/// <para><b>One claim per slot and one per line.</b> The claim is inserted in one <c>BEGIN IMMEDIATE</c> transaction that reads that the
/// slot has none, and for a claim that switches it no live visit; the primary key refuses a second claim on the slot and the unique index a
/// second for the same line, from any process.</para>
/// <para><b>The claim is the process run that makes it</b> (<c>claim_run</c> and <c>claim_generation</c>): every phase is one transaction that
/// checks both first. A claim is taken over only from a run dead by the lease's own test, by one guarded update that raises the generation, so
/// of two lookers that read it dead only one acts.</para>
/// <para>A record names its line and never a slot, so the folder is read here alone: <see cref="WhereIs"/> is the one answer every reader that
/// opens a record's tree as a folder asks. Opened for each use and closed, under the process's one run; a read never creates the store.</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
/// <param name="alive">Whether a run that wrote the store still lives; null reads the lease's own test. A test hands one, since one process is one run.</param>
/// <param name="run">The run this pool writes and claims under; null for the process's own.</param>
/// <param name="kind">The run's kind; null for the process's own.</param>
/// <param name="clock">The clock of every moment written.</param>
public sealed class TreePool(
    string home, Func<StoreSettled>? settled = null, Func<StoreRun, bool>? alive = null, string? run = null, string? kind = null, TimeProvider? clock = null)
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "kept-trees";

    /// <summary>The store's step that made the tables: a store before it holds none.</summary>
    public const int Since = 23;

    public const int ExpectSince = DesktopStore.GenerationVersion + 1;

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The run this pool writes and claims under.</summary>
    public string Run { get; } = run ?? StoreProcess.Run;

    internal Func<StoreRun, bool> Alive { get; } = alive ?? IntegrationStore.Lives;

    // ——— Reads.

    /// <summary>The slots of a repository, by number.</summary>
    public IReadOnlyList<KeptTree> Slots(string workspace, string repository) =>
        Reading(reader => reader.Query(
            $"SELECT {TreePoolRows.KeptColumns} FROM kept_trees WHERE workspace = $workspace AND repository = $repository ORDER BY number", TreePoolRows.Kept,
            ("$workspace", workspace), ("$repository", repository)), []);

    /// <summary>Slot <paramref name="number"/>, or null where the pool has none.</summary>
    public KeptTree? Slot(string workspace, string repository, int number) =>
        Slots(workspace, repository).FirstOrDefault(each => each.Number == number);

    /// <summary>The slot <paramref name="line"/> is in, or null where it holds none: a record names a line, and this is where its folder is.</summary>
    public KeptTree? WhereIs(string workspace, string repository, string line) =>
        line.Length == 0
            ? null
            : Reading(reader => reader.Query(
                $"SELECT {TreePoolRows.KeptColumns} FROM kept_trees WHERE workspace = $workspace AND repository = $repository AND line = $line",
                TreePoolRows.Kept, ("$workspace", workspace), ("$repository", repository), ("$line", line)).FirstOrDefault(), null);

    /// <summary>The claim in flight on a slot, or null.</summary>
    public TreeClaim? ClaimOn(string workspace, string repository, int number) =>
        Reading(reader => reader.Query(
            $"SELECT {TreePoolRows.ClaimColumns} FROM tree_claims WHERE workspace = $workspace AND repository = $repository AND number = $number",
            TreePoolRows.Claim, ("$workspace", workspace), ("$repository", repository), ("$number", number)).FirstOrDefault(), null, ExpectSince);

    /// <summary>The claim in flight for a line, or the one whose undo puts a slot back on it; null where none names it.</summary>
    public TreeClaim? ClaimOf(string workspace, string repository, string line) =>
        line.Length == 0
            ? null
            : Reading(reader => reader.Query(
                $"SELECT {TreePoolRows.ClaimColumns} FROM tree_claims WHERE workspace = $workspace AND repository = $repository AND {TreePoolRows.NamesLine} ORDER BY line <> $line",
                TreePoolRows.Claim, ("$workspace", workspace), ("$repository", repository), ("$line", line)).FirstOrDefault(), null, ExpectSince);

    /// <summary>Every claim in flight in a repository, by slot.</summary>
    public IReadOnlyList<TreeClaim> Claims(string workspace, string repository) =>
        Reading(reader => reader.Query(
            $"SELECT {TreePoolRows.ClaimColumns} FROM tree_claims WHERE workspace = $workspace AND repository = $repository ORDER BY number", TreePoolRows.Claim,
            ("$workspace", workspace), ("$repository", repository)), [], ExpectSince);

    /// <summary>The terminals and served builds in a slot.</summary>
    public IReadOnlyList<SlotVisit> Visits(string workspace, string repository, int number) =>
        Reading(reader => reader.Query(
            $"SELECT {TreePoolRows.VisitColumns} FROM tree_visits WHERE workspace = $workspace AND repository = $repository AND number = $number ORDER BY at, visitor",
            TreePoolRows.Visit, ("$workspace", workspace), ("$repository", repository), ("$number", number)), []);

    /// <summary>Every let-go of a line, oldest first.</summary>
    public IReadOnlyList<LineEnd> Ends(string workspace, string repository, string line) =>
        Reading(reader => reader.Query(
            $"SELECT {TreePoolRows.EndColumns} FROM line_ends WHERE workspace = $workspace AND repository = $repository AND line = $line ORDER BY at",
            TreePoolRows.End, ("$workspace", workspace), ("$repository", repository), ("$line", line)), []);

    /// <summary>The latest let-go of a line, or null where it has none.</summary>
    public LineEnd? LastEnd(string workspace, string repository, string line) => Ends(workspace, repository, line).LastOrDefault();

    // ——— Writes.

    /// <summary>Claim a slot with the claim's whole intent, or answer why it cannot be claimed and write nothing.</summary>
    public TreeClaimed Claim(TreeClaimOpening opening) => new TreePoolClaims(this).Claim(opening);

    /// <summary>Take up a claim whose run is dead by the lease's test, in one guarded update that raises the generation.</summary>
    public TreeClaimed TakeOver(string workspace, string repository, int number, long generation) =>
        new TreePoolClaims(this).TakeOver(workspace, repository, number, generation);

    /// <summary>Write the phase the claim's next effect begins or the last one reached, with the open's key or the record it made.</summary>
    public void Advance(ClaimFence fence, string phase, string? opens = null, string? record = null) =>
        new TreePoolClaims(this).Advance(fence, phase, opens, record);

    /// <summary>Write the claim's undo begun and the slot's row naming no line, in one transaction.</summary>
    public void Undoing(ClaimFence fence) => new TreePoolClaims(this).Undoing(fence);

    /// <summary>End the claim, leaving the slot's row as <paramref name="settled"/> says (or untouched, for null), in one transaction.</summary>
    public void Complete(ClaimFence fence, SlotSettled? settled) => new TreePoolClaims(this).Complete(fence, settled);

    /// <summary>End a retire claim: the slot's row is removed with the claim.</summary>
    public void Retired(ClaimFence fence) => new TreePoolClaims(this).Retired(fence);

    /// <summary>Admit a terminal or a served build into a slot; null where admitted, else why not. A claim in flight refuses it.</summary>
    public string? Visit(string workspace, string repository, int number, string visitKind, string visitor) =>
        new TreePoolClaims(this).Visit(workspace, repository, number, visitKind, visitor);

    /// <summary>Let a visit go.</summary>
    public void LetGo(string workspace, string repository, int number, string visitor)
    {
        using var store = Open();
        store.Write(Family, write => write.Execute(
            "DELETE FROM tree_visits WHERE workspace = $workspace AND repository = $repository AND number = $number AND visitor = $visitor",
            ("$workspace", workspace), ("$repository", repository), ("$number", number), ("$visitor", visitor)));
    }

    /// <summary>Record a let-go of a line.</summary>
    public void RecordEnd(LineEnd end)
    {
        using var store = Open();
        store.Write(Family, write => write.Execute(
            $"INSERT INTO line_ends ({TreePoolRows.EndColumns}) VALUES ($workspace, $repository, $line, $branch, $session, $number, $kind, $tip, $read, $left, $toolchain, $at)",
            ("$workspace", end.Workspace), ("$repository", end.Repository), ("$line", end.Line), ("$branch", end.Branch), ("$session", end.Session),
            ("$number", end.Number), ("$kind", end.Kind), ("$tip", end.Tip), ("$read", Moments.Write(end.Read)), ("$left", end.Left),
            ("$toolchain", end.Toolchain), ("$at", Moments.Write(end.At))));
    }

    // ——— Over git. Each act is a claim's phases in a unit of its own; nothing outside tests calls them yet.

    /// <summary>The seams the acts below call out through; a test sets its own.</summary>
    internal PoolSeams Seams { get; set; } = new();

    internal Task<PoolDone> MakeAsync(PoolMake make, CancellationToken ct = default) => new TreePoolMakes(this, Seams).MakeAsync(make, ct);

    internal Task<PoolDone> TakeAsync(PoolTake take, CancellationToken ct = default) => new TreePoolTakes(this, Seams).TakeAsync(take, ct);

    /// <summary>A returning line's slot: a take with <see cref="PoolTake.Returns"/>.</summary>
    internal Task<PoolDone> ReturnAsync(PoolTake take, CancellationToken ct = default) => TakeAsync(take with { Returns = true }, ct);

    internal Task<PoolDone> FinishAsync(PoolFinish finish, CancellationToken ct = default) => new TreePoolFinishes(this, Seams).FinishAsync(finish, ct);

    internal Task<PoolDone> RetireAsync(PoolSite site, int number, string holder, CancellationToken ct = default) => new TreePoolRetires(this, Seams).RetireAsync(site, number, holder, ct);

    internal Task<PoolDone> EndAsync(PoolEnd end, CancellationToken ct = default) => new TreePoolEnds(this, Seams).EndAsync(end, ct);

    /// <summary>The look's takeover of a dead run's claim on a slot, settled by what the disk shows.</summary>
    internal Task<PoolDone> RecoverAsync(PoolSite site, int number, CancellationToken ct = default) => new PoolTakeovers(this, Seams).RecoverAsync(site, number, ct);

    /// <summary>Admit a terminal or a served build; the visitor is let go with <see cref="LetGo"/>.</summary>
    internal Task<string?> VisitAsync(string workspace, string repository, int number, string visitKind, string visitor) =>
        Task.FromResult(Visit(workspace, repository, number, visitKind, visitor));

    /// <summary>The judgement of a slot as the choice reads it before any claim: whether it may be taken for another line.</summary>
    internal async Task<SlotVerdict> JudgeAsync(PoolSite site, int number, CancellationToken ct = default)
    {
        if (Slot(site.Workspace, site.Repository, number) is not { } slot) return new(1, SlotJudgement.Held, "the pool has no such slot");
        var seed = Seams.Holds is { } holds ? await holds(slot, ct).ConfigureAwait(false) : new SlotJudgementFacts { Claimed = ClaimOn(site.Workspace, site.Repository, number) is not null };
        var expected = slot.Line.Length > 0 ? LastEnd(site.Workspace, site.Repository, slot.Line)?.Tip ?? "" : slot.Tip;
        return (await PoolJudging.JudgeAsync(site, slot, seed, expected, ct).ConfigureAwait(false)).Verdict;
    }

    // ——— The store.

    internal DesktopStore Open() =>
        DesktopStore.Open(DesktopStore.LocationIn(home), StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });

    internal string Now() => Moments.Write(_clock.GetUtcNow());

    /// <summary>A read of a store past <paramref name="since"/>, or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none, int since = Since)
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
        return reader is not null && reader.Version >= since ? read(reader) : none;
    }
}
