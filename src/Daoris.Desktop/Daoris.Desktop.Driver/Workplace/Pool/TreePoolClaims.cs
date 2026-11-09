namespace Daoris.Driver;

/// <summary>
/// The pool's claim writes, each one store transaction: a claim inserted with its whole intent, its phases under its fence, its end, its
/// takeover from a dead run by one guarded update, and the visits a claim and a terminal contend over.
/// </summary>
internal sealed class TreePoolClaims(TreePool owner)
{
    private static string Family => TreePool.Family;

    public TreeClaimed Claim(TreeClaimOpening opening)
    {
        try
        {
            using var store = owner.Open();
            return store.Write(Family, write =>
            {
                var slot = SlotIn(write, opening.Workspace, opening.Repository, opening.Number);
                if (opening.Kind == TreeClaimKinds.Make && slot is not null) throw Refused("the slot is already made");
                if (opening.Kind != TreeClaimKinds.Make && slot is null) throw Refused("the pool has no such slot");
                if (ClaimIn(write, opening.Workspace, opening.Repository, opening.Number) is { } held)
                {
                    throw Refused($"the slot has a {held.Kind} claim in flight, held by run {held.ClaimRun}");
                }

                if (opening.Line.Length > 0 && ClaimOfLineIn(write, opening.Workspace, opening.Repository, opening.Line) is { } onLine)
                {
                    throw Refused(onLine.Line == opening.Line
                        ? $"the line has a {onLine.Kind} claim in flight on slot {onLine.Number}"
                        : $"the line waits for slot {onLine.Number} to be put back on it");
                }

                if (!opening.InPlace && TreeClaimKinds.Switching.Contains(opening.Kind) && LiveVisitIn(write, opening) is { } visit)
                {
                    throw Refused($"a {visit.Kind} visit holds the slot");
                }

                write.Execute(
                    $"""
                    INSERT INTO tree_claims ({TreePoolRows.ClaimColumns})
                    VALUES ($workspace, $repository, $number, $kind, $holder, $line, $branch, $start, $expect, '', $job, '', $phase, $run, 1, $at, $expectBranch, $expectLine)
                    """,
                    ("$workspace", opening.Workspace), ("$repository", opening.Repository), ("$number", opening.Number), ("$kind", opening.Kind),
                    ("$holder", opening.Holder), ("$line", opening.Line), ("$branch", opening.Branch), ("$start", opening.Start),
                    ("$expect", opening.Expect), ("$job", opening.Job), ("$phase", opening.Phase), ("$run", owner.Run), ("$at", owner.Now()),
                    ("$expectBranch", opening.ExpectBranch), ("$expectLine", opening.ExpectLine));
                var claim = ClaimIn(write, opening.Workspace, opening.Repository, opening.Number)!;
                return new TreeClaimed(claim, FenceOf(claim), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return unwritten.Answer;
        }
    }

    /// <summary>
    /// Take a claim whose run is dead, or this run's own whose undo waits, in one guarded update that raises the generation: refused, nothing
    /// written, where it is gone, this run's own step, moved past <paramref name="generation"/>, or its run lives or cannot be told dead.
    /// </summary>
    public TreeClaimed TakeOver(string workspace, string repository, int number, long generation)
    {
        try
        {
            using var store = owner.Open();
            return store.Write(Family, write =>
            {
                var found = ClaimIn(write, workspace, repository, number) ?? throw Refused("the pool holds no such claim");
                var own = string.Equals(found.ClaimRun, owner.Run, StringComparison.Ordinal);
                if (own && found.Phase != TreeClaimPhases.UndoWaiting && !PoolAbandoned.Holds(found.Job)) throw Refused("it is this run's own");
                if (found.ClaimGeneration != generation) throw Refused("another looker took it first");

                // A run the store holds no row of cannot be told dead, so it holds, as a start that cannot be read does.
                if (!own && (RunIn(write, found.ClaimRun) is not { } holder || owner.Alive(holder))) throw Refused("the Daoris process that runs it still runs");
                var taken = write.Execute(
                    """
                    UPDATE tree_claims SET claim_run = $mine, claim_generation = claim_generation + 1
                    WHERE workspace = $workspace AND repository = $repository AND number = $number AND claim_run = $run AND claim_generation = $generation
                    """,
                    ("$mine", owner.Run), ("$workspace", workspace), ("$repository", repository), ("$number", number), ("$run", found.ClaimRun),
                    ("$generation", generation));
                if (taken != 1) throw Refused("another looker took it first");
                var now = ClaimIn(write, workspace, repository, number)!;
                PoolAbandoned.Forget(now.Job);
                return new TreeClaimed(now, FenceOf(now), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return unwritten.Answer;
        }
    }

    public void Advance(ClaimFence fence, string phase, string? opens, string? record, Action<DesktopWrite>? also = null)
    {
        using var store = owner.Open();
        store.Write(Family, write =>
        {
            Fenced(write, fence);
            also?.Invoke(write);
            var changed = write.Execute(
                """
                UPDATE tree_claims SET phase = $phase, opens = COALESCE($opens, opens), record = COALESCE($record, record)
                WHERE workspace = $workspace AND repository = $repository AND number = $number AND claim_run = $run AND claim_generation = $generation
                """,
                ("$phase", phase), ("$opens", opens), ("$record", record), ("$workspace", fence.Workspace), ("$repository", fence.Repository),
                ("$number", fence.Number), ("$run", fence.Run), ("$generation", fence.Generation));
            if (changed != 1) throw new TreePoolRefusedException("the claim's row changed under the write");
        });
    }

    public void Undoing(ClaimFence fence)
    {
        Advance(fence, TreeClaimPhases.Undoing, null, null, write => write.Execute(
            "UPDATE kept_trees SET line = '' WHERE workspace = $workspace AND repository = $repository AND number = $number",
            ("$workspace", fence.Workspace), ("$repository", fence.Repository), ("$number", fence.Number)));
    }

    public void Complete(ClaimFence fence, SlotSettled? settled)
    {
        using var store = owner.Open();
        store.Write(Family, write =>
        {
            Fenced(write, fence);
            if (settled is not null) Settle(write, fence, settled);
            DeleteClaim(write, fence);
        });
    }

    public void Retired(ClaimFence fence)
    {
        using var store = owner.Open();
        store.Write(Family, write =>
        {
            Fenced(write, fence);
            write.Execute(
                "DELETE FROM kept_trees WHERE workspace = $workspace AND repository = $repository AND number = $number",
                ("$workspace", fence.Workspace), ("$repository", fence.Repository), ("$number", fence.Number));
            DeleteClaim(write, fence);
        });
    }

    public string? Visit(string workspace, string repository, int number, string visitKind, string visitor)
    {
        try
        {
            using var store = owner.Open();
            return store.Write(Family, write =>
            {
                if (SlotIn(write, workspace, repository, number) is null) throw new Unwritten(new TreeClaimed(null, null, "the pool has no such slot"));
                if (ClaimIn(write, workspace, repository, number) is { } held)
                {
                    throw new Unwritten(new TreeClaimed(null, null, $"a {held.Kind} claim is in flight on the slot"));
                }

                write.Execute(
                    $"INSERT OR REPLACE INTO tree_visits ({TreePoolRows.VisitColumns}) VALUES ($workspace, $repository, $number, $kind, $visitor, $run, $at)",
                    ("$workspace", workspace), ("$repository", repository), ("$number", number), ("$kind", visitKind), ("$visitor", visitor),
                    ("$run", owner.Run), ("$at", owner.Now()));
                return (string?)null;
            });
        }
        catch (Unwritten unwritten)
        {
            return unwritten.Answer.Refused;
        }
    }

    /// <summary>The slot's row written as a finished claim leaves it; its <c>since</c> moves only when the line it serves does.</summary>
    private void Settle(DesktopWrite write, ClaimFence fence, SlotSettled settled)
    {
        var existing = SlotIn(write, fence.Workspace, fence.Repository, fence.Number);
        if (settled.Line.Length > 0
            && write.Scalar<long>(
                "SELECT COUNT(*) FROM kept_trees WHERE workspace = $workspace AND repository = $repository AND line = $line AND number <> $number",
                ("$workspace", fence.Workspace), ("$repository", fence.Repository), ("$line", settled.Line), ("$number", fence.Number)) > 0)
        {
            throw new TreePoolRefusedException($"another slot already serves the line {settled.Line}");
        }

        var since = existing is not null && existing.Line == settled.Line ? Moments.Write(existing.Since) : owner.Now();
        write.Execute(
            $"""
            INSERT OR REPLACE INTO kept_trees ({TreePoolRows.KeptColumns})
            VALUES ($workspace, $repository, $number, $registration, $path, $line, $tip, $state, $said, $since, $bytes, $toolchain)
            """,
            ("$workspace", fence.Workspace), ("$repository", fence.Repository), ("$number", fence.Number), ("$registration", settled.Registration),
            ("$path", settled.Path), ("$line", settled.Line), ("$tip", settled.Tip), ("$state", settled.State), ("$said", settled.Said),
            ("$since", since), ("$bytes", settled.Bytes), ("$toolchain", settled.Toolchain));
    }

    /// <summary>The first visit to the claim's slot whose run lives or cannot be told dead; a dead run's visits are dropped.</summary>
    private SlotVisit? LiveVisitIn(DesktopWrite write, TreeClaimOpening opening)
    {
        SlotVisit? live = null;
        var visits = write.Query(
            $"SELECT {TreePoolRows.VisitColumns} FROM tree_visits WHERE workspace = $workspace AND repository = $repository AND number = $number ORDER BY at, visitor",
            TreePoolRows.Visit, ("$workspace", opening.Workspace), ("$repository", opening.Repository), ("$number", opening.Number));
        foreach (var visit in visits)
        {
            if (RunIn(write, visit.Run) is { } run && !owner.Alive(run))
            {
                write.Execute(
                    "DELETE FROM tree_visits WHERE workspace = $workspace AND repository = $repository AND number = $number AND visitor = $visitor",
                    ("$workspace", visit.Workspace), ("$repository", visit.Repository), ("$number", visit.Number), ("$visitor", visit.Visitor));
                continue;
            }

            live ??= visit;
        }

        return live;
    }

    private static void Fenced(DesktopWrite write, ClaimFence fence)
    {
        var claim = ClaimIn(write, fence.Workspace, fence.Repository, fence.Number)
            ?? throw new TreePoolRefusedException($"the pool holds no claim on slot {fence.Number}");
        if (!string.Equals(claim.ClaimRun, fence.Run, StringComparison.Ordinal) || claim.ClaimGeneration != fence.Generation)
        {
            throw new TreePoolRefusedException($"run {claim.ClaimRun} holds slot {fence.Number} at generation {claim.ClaimGeneration}");
        }
    }

    private static void DeleteClaim(DesktopWrite write, ClaimFence fence) => write.Execute(
        "DELETE FROM tree_claims WHERE workspace = $workspace AND repository = $repository AND number = $number AND claim_run = $run AND claim_generation = $generation",
        ("$workspace", fence.Workspace), ("$repository", fence.Repository), ("$number", fence.Number), ("$run", fence.Run), ("$generation", fence.Generation));

    private static ClaimFence FenceOf(TreeClaim claim) => new(claim.Workspace, claim.Repository, claim.Number, claim.ClaimRun, claim.ClaimGeneration);

    private static KeptTree? SlotIn(DesktopWrite write, string workspace, string repository, int number) =>
        write.Query(
            $"SELECT {TreePoolRows.KeptColumns} FROM kept_trees WHERE workspace = $workspace AND repository = $repository AND number = $number",
            TreePoolRows.Kept, ("$workspace", workspace), ("$repository", repository), ("$number", number)).FirstOrDefault();

    private static TreeClaim? ClaimIn(DesktopWrite write, string workspace, string repository, int number) =>
        write.Query(
            $"SELECT {TreePoolRows.ClaimColumns} FROM tree_claims WHERE workspace = $workspace AND repository = $repository AND number = $number",
            TreePoolRows.Claim, ("$workspace", workspace), ("$repository", repository), ("$number", number)).FirstOrDefault();

    private static TreeClaim? ClaimOfLineIn(DesktopWrite write, string workspace, string repository, string line) =>
        write.Query(
            $"SELECT {TreePoolRows.ClaimColumns} FROM tree_claims WHERE workspace = $workspace AND repository = $repository AND {TreePoolRows.NamesLine}",
            TreePoolRows.Claim, ("$workspace", workspace), ("$repository", repository), ("$line", line)).FirstOrDefault();

    private static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    private static Unwritten Refused(string why) => new(new TreeClaimed(null, null, why));

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back and no revision moves.</summary>
    private sealed class Unwritten(TreeClaimed answer) : Exception
    {
        public TreeClaimed Answer { get; } = answer;
    }
}
