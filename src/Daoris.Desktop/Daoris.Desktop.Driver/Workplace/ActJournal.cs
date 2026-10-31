using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>An act's phases (the git-writes design §5): each written before what it names begins, the record closed last.</summary>
public static class ActPhases
{
    /// <summary>The fresh reads equal the plan: written with the record, before anything else.</summary>
    public const string Judged = "judged";

    /// <summary>The recovery written and the act's job made: no effect step has begun.</summary>
    public const string Prepared = "prepared";

    /// <summary>A step's start written, with its deadline, before its process.</summary>
    public const string Started = "started";

    /// <summary>A step's end written: its exit, or <i>unended</i>.</summary>
    public const string Ended = "ended";

    /// <summary>The postconditions read; the outcome is <see cref="ActOutcomes"/>'.</summary>
    public const string Reconciled = "reconciled";

    /// <summary>Nothing more is written but the act ref's expiry; the checkout lock goes.</summary>
    public const string Closed = "closed";

    /// <summary>Every phase this build writes and reads: a record at another was written by a newer build, and is held.</summary>
    public static IReadOnlyList<string> All { get; } = [Judged, Prepared, Started, Ended, Reconciled, Closed];
}

/// <summary>What an act's postconditions said, read after its effect and never from an exit code (§5).</summary>
public static class ActOutcomes
{
    /// <summary>Every step's postconditions hold.</summary>
    public const string Done = "done";

    /// <summary>No step's effect stands: every one began reads as it was before, or none began.</summary>
    public const string NotDone = "not-done";

    /// <summary>Some steps' effects stand and others do not, each read: the case of a rename stopped between its ref and its section.</summary>
    public const string Partial = "partial";

    /// <summary>A step's postconditions read as neither, or its process is unended: nothing was retried.</summary>
    public const string Unknown = "unknown";
}

/// <summary>The effect steps an act takes, each with its own postconditions (§5, §6).</summary>
public static class ActStepKinds
{
    /// <summary>One <c>update-ref --stdin</c> transaction, its git run in the act's job.</summary>
    public const string Ref = "ref";

    /// <summary>A configuration section moved or removed under git's own <c>config.lock</c>.</summary>
    public const string Config = "config";

    /// <summary>Index entries replaced under git's own <c>index.lock</c>, the replacement built on a copy.</summary>
    public const string Index = "index";

    /// <summary>One <c>git commit</c>, its hooks and their children in the act's job (GITAPP2i).</summary>
    public const string Commit = "commit";

    /// <summary>A worktree file's bytes replaced where they still equal the plan's, the bytes it replaced kept as a file beside the record (GITAPP2j).</summary>
    public const string Bytes = "file";

    /// <summary>One <c>git switch</c> of the checkout to a local branch, its <c>post-checkout</c> hook in the act's job (GITAPP2k).</summary>
    public const string Switch = "switch";

    /// <summary>One <c>git stash push</c>, which writes the stash shared by every linked tree (GITAPP2k).</summary>
    public const string Stash = "stash";

    /// <summary>Every kind this build runs and reads.</summary>
    public static IReadOnlyList<string> All { get; } = [Ref, Config, Index, Commit, Bytes, Switch, Stash];
}

/// <summary>One step of an act's plan, as the journal keeps it: its kind, and its arguments, postconditions and rollback as JSON.</summary>
public sealed record ActStepPlan(string Kind, string Arguments, string Postconditions, string? Rollback = null);

/// <summary>
/// An act as it is judged, before anything of it is written (§5's fields): its id and kind, the press and its Confirm, the repository and the
/// checkout (its top and its canonical common directory, the checkout lock's key), the plan it showed, its steps, and its recovery.
/// </summary>
public sealed record ActOpening(
    string Id, string Kind, string Press, string Repository, string Checkout, string CommonDir, string Plan, IReadOnlyList<ActStepPlan> Steps)
{
    /// <summary>The press's Confirm, as its door names it; null for a press that needed none.</summary>
    public string? Confirmed { get; init; }

    /// <summary>The recovery as JSON (refs, sections, entries, the act ref and its id), written before the first effect.</summary>
    public string? Recovery { get; init; }
}

/// <summary>The claim an act is written under: its run and the generation the last take raised.</summary>
public sealed record ActClaim(string Act, string Run, long Generation);

/// <summary>An act's record as the journal holds it.</summary>
public sealed record ActRecord(
    string Id, int Format, string Kind, string Press, string Repository, string Checkout, string CommonDir, string Plan, string Job,
    string ClaimRun, long ClaimGeneration, string Phase, DateTimeOffset At)
{
    public string? Confirmed { get; init; }

    public string? Recovery { get; init; }

    public string? Outcome { get; init; }

    public string? Said { get; init; }

    public DateTimeOffset? ReconciledAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? ExpiredAt { get; init; }

    /// <summary>Whether the record is not closed: its checkout is held for it until it is.</summary>
    public bool Open => ClosedAt is null;
}

/// <summary>One step of an act as the journal holds it: its plan, and what its start, its process and its end wrote.</summary>
public sealed record ActStep(string Act, int Ordinal, string Kind, string Arguments, string Postconditions, string? Rollback)
{
    public DateTimeOffset? BeganAt { get; init; }

    /// <summary>The step's start plus its bound: past it, a reconciler ends what still runs.</summary>
    public DateTimeOffset? Deadline { get; init; }

    public int? Pid { get; init; }

    /// <summary>The step's process's start, UTC ticks, which tells it from a later process of the same id.</summary>
    public long? PidStarted { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public int? ExitCode { get; init; }

    /// <summary>Whether its process was not seen to end: its effect is then unknown, never failed.</summary>
    public bool Unended { get; init; }

    public string? Outcome { get; init; }

    public string? Said { get; init; }

    /// <summary>Begun and not ended: a step a crash cut, or one that runs.</summary>
    public bool Open => BeganAt is not null && EndedAt is null;
}

/// <summary>What a take answered: the record and the claim taken, or why it was refused, nothing written.</summary>
public sealed record ActTaken(ActRecord? Act, ActClaim? Claim, string? Refused);

/// <summary>An act's write refused before anything was written: another act stands open in the checkout, or the record is not the claim's.</summary>
public sealed class ActRefusedException(string act, string why) : Exception($"act {act}: {why}")
{
    public string Act { get; } = act;

    public string Why { get; } = why;
}

/// <summary>
/// The act journal (GITAPP2a, D204; the git-writes design §5): a desktop-store family, Workplace's, in <c>git_acts</c> and <c>git_act_steps</c>,
/// machine-local and never synced (D212 point 17).
/// </summary>
/// <remarks>
/// <para><b>The integration store's claim and step shape</b> (<see cref="IntegrationStore"/>): a record is claimed by the run that judged it and
/// a generation; it is taken only from a run dead by the lease's own test, in one update that raises the generation; every write checks the
/// claim inside its transaction and names it in its update's condition, so a run that lost it writes nothing more. A step's start is written
/// before its effect and its end after, each its own transaction.</para>
/// <para><b>One open act per checkout</b>, by a partial unique index on the checkout's common directory: a second open is refused under the
/// same lock, nothing written. A record carries its <c>format</c>; a build reads one past its own, or a phase or a step kind it does not know,
/// as held: never taken, never reconciled.</para>
/// <para>Opened for each use and closed, under the process's run, as the integration's store is; a read never creates the store.</para>
/// </remarks>
public sealed class ActJournal(
    string home, Func<StoreSettled>? settled = null, Func<StoreRun, bool>? alive = null, string? run = null, string? kind = null, TimeProvider? clock = null)
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "acts";

    /// <summary>The record's shape this build writes and reads.</summary>
    public const int Format = 1;

    /// <summary>The store's step that made the family: a store before it holds no act.</summary>
    internal const int Since = 13;

    private const int UniqueRefused = 2067;

    private const string ActColumns =
        "id, format, kind, press, confirmed, repository, checkout, common_dir, plan, job, claim_run, claim_generation, phase, recovery, outcome, said, at, "
        + "reconciled_at, closed_at, expires_at, expired_at";

    private const string StepColumns =
        "act, ordinal, kind, arguments, postconditions, rollback, began_at, deadline, pid, pid_started, ended_at, exit_code, unended, outcome, said";

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    private readonly Func<StoreRun, bool> _alive = alive ?? IntegrationStore.Lives;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The home whose store this is.</summary>
    public string Home => home;

    /// <summary>The run this journal writes and claims under.</summary>
    public string Run { get; } = run ?? StoreProcess.Run;

    /// <summary>The clock its moments are written by.</summary>
    public TimeProvider Clock => _clock;

    /// <summary>The job an act's steps run in, named for the act, so a reconciler finds a live step without its pid.</summary>
    public static string JobOf(string act) => $"daoris-act-{act}";

    // ——— Reads.

    /// <summary>An act by its id; null where the store holds none.</summary>
    public ActRecord? Act(string id) =>
        Reading(reader => reader.Query($"SELECT {ActColumns} FROM git_acts WHERE id = $id", MapAct, ("$id", id)).FirstOrDefault(), null);

    /// <summary>An act's steps, in their order.</summary>
    public IReadOnlyList<ActStep> Steps(string act) =>
        Reading(reader => reader.Query($"SELECT {StepColumns} FROM git_act_steps WHERE act = $act ORDER BY ordinal", MapStep, ("$act", act)), []);

    /// <summary>Every act not closed, oldest first: what the driver's start reconciles.</summary>
    public IReadOnlyList<ActRecord> Unclosed() =>
        Reading(reader => reader.Query($"SELECT {ActColumns} FROM git_acts WHERE closed_at IS NULL ORDER BY at, id", MapAct), []);

    /// <summary>The acts not closed in the checkout whose common directory is <paramref name="commonDir"/> (the checkout lock's key).</summary>
    public IReadOnlyList<ActRecord> UnclosedIn(string commonDir) =>
        Reading(reader => reader.Query(
            $"SELECT {ActColumns} FROM git_acts WHERE common_dir = $dir AND closed_at IS NULL ORDER BY at, id", MapAct, ("$dir", commonDir)), []);

    /// <summary>The closed acts whose recovery expired by <paramref name="now"/> and is not yet let go of.</summary>
    public IReadOnlyList<ActRecord> Expiring(DateTimeOffset now) =>
        Reading(reader => reader.Query(
                $"SELECT {ActColumns} FROM git_acts WHERE closed_at IS NOT NULL AND expired_at IS NULL AND expires_at IS NOT NULL ORDER BY expires_at, id", MapAct)
            .Where(each => each.ExpiresAt <= now).ToList(), []);

    // ——— Writes, each one transaction under the claim's check.

    /// <summary>
    /// The record and its steps written at <see cref="ActPhases.Judged"/>, claimed by this run at generation 1: refused, nothing written,
    /// where another act stands open in the checkout.
    /// </summary>
    /// <exception cref="ActRefusedException">Another act is open in the checkout, or the opening names no step or a kind this build does not run.</exception>
    public ActClaim Open(ActOpening opening)
    {
        if (opening.Steps.Count == 0) throw new ActRefusedException(opening.Id, "an act takes one step or more");
        foreach (var step in opening.Steps)
        {
            if (!ActStepKinds.All.Contains(step.Kind, StringComparer.Ordinal)) throw new ActRefusedException(opening.Id, $"`{step.Kind}` is no step this build runs");
        }

        using var store = Writer();
        return store.Write(Family, write =>
        {
            try
            {
                write.Execute(
                    $"""
                    INSERT INTO git_acts ({ActColumns})
                    VALUES ($id, $format, $kind, $press, $confirmed, $repository, $checkout, $dir, $plan, $job, $run, 1, $phase, $recovery, NULL, NULL, $at,
                            NULL, NULL, NULL, NULL)
                    """,
                    ("$id", opening.Id), ("$format", Format), ("$kind", opening.Kind), ("$press", opening.Press), ("$confirmed", opening.Confirmed),
                    ("$repository", opening.Repository), ("$checkout", opening.Checkout), ("$dir", opening.CommonDir), ("$plan", opening.Plan),
                    ("$job", JobOf(opening.Id)), ("$run", Run), ("$phase", ActPhases.Judged), ("$recovery", opening.Recovery), ("$at", Now()));
            }
            catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
            {
                var standing = write.Query($"SELECT {ActColumns} FROM git_acts WHERE common_dir = $dir AND closed_at IS NULL", MapAct, ("$dir", opening.CommonDir))
                    .FirstOrDefault();
                throw new ActRefusedException(opening.Id, standing is null
                    ? "the journal refused it as written twice"
                    : $"act {standing.Id} ({standing.Kind}) is still open in `{opening.Repository}`'s checkout, and one act at a time is open there");
            }

            for (var at = 0; at < opening.Steps.Count; at++)
            {
                var step = opening.Steps[at];
                write.Execute(
                    "INSERT INTO git_act_steps (act, ordinal, kind, arguments, postconditions, rollback) VALUES ($act, $ordinal, $kind, $arguments, $post, $rollback)",
                    ("$act", opening.Id), ("$ordinal", at), ("$kind", step.Kind), ("$arguments", step.Arguments), ("$post", step.Postconditions),
                    ("$rollback", step.Rollback));
            }

            return new ActClaim(opening.Id, Run, 1);
        });
    }

    /// <summary>The recovery written and the job made: <see cref="ActPhases.Prepared"/>. A recovery given replaces the opening's.</summary>
    public void Prepared(ActClaim claim, string? recovery = null) => Fenced(claim, write =>
        Changed(write, claim, "UPDATE git_acts SET phase = $phase, recovery = COALESCE($recovery, recovery) WHERE id = $act",
            ("$phase", ActPhases.Prepared), ("$recovery", recovery)));

    /// <summary>A step's start, before its process: its moment and deadline, the act <see cref="ActPhases.Started"/>.</summary>
    /// <exception cref="ActRefusedException">The step began already, or another step is open.</exception>
    public ActStep Begin(ActClaim claim, int ordinal, DateTimeOffset deadline) => Fenced(claim, write =>
    {
        if (StepsIn(write, claim.Act).FirstOrDefault(each => each.Open) is { } open)
        {
            throw new ActRefusedException(claim.Act, $"its step {open.Ordinal} began at {Moments.Write(open.BeganAt)} and has no end: a step a crash cut is the reconciler's");
        }

        var begun = write.Execute(
            "UPDATE git_act_steps SET began_at = $at, deadline = $deadline WHERE act = $act AND ordinal = $ordinal AND began_at IS NULL",
            ("$at", Now()), ("$deadline", Moments.Write(deadline)), ("$act", claim.Act), ("$ordinal", ordinal));
        if (begun != 1) throw new ActRefusedException(claim.Act, $"its step {ordinal} is not one waiting to begin");
        Changed(write, claim, "UPDATE git_acts SET phase = $phase WHERE id = $act", ("$phase", ActPhases.Started));
        return StepsIn(write, claim.Act).Single(each => each.Ordinal == ordinal);
    });

    /// <summary>The step's process, once it is held in the act's job and before it runs: its id and its start.</summary>
    public void Launched(ActClaim claim, int ordinal, int pid, long started) => Fenced(claim, write =>
    {
        var set = write.Execute(
            "UPDATE git_act_steps SET pid = $pid, pid_started = $started WHERE act = $act AND ordinal = $ordinal AND began_at IS NOT NULL AND ended_at IS NULL",
            ("$pid", pid), ("$started", started), ("$act", claim.Act), ("$ordinal", ordinal));
        if (set != 1) throw new ActRefusedException(claim.Act, $"its step {ordinal} is not open");
    });

    /// <summary>
    /// A step's end, after its effect: its exit, or <paramref name="unended"/>, and what it said; the act <see cref="ActPhases.Ended"/>. An end
    /// written again is the first's, and changes nothing.
    /// </summary>
    public ActStep End(ActClaim claim, int ordinal, int? exit, bool unended, string said) => Fenced(claim, write =>
    {
        write.Execute(
            "UPDATE git_act_steps SET ended_at = $at, exit_code = $exit, unended = $unended, said = $said WHERE act = $act AND ordinal = $ordinal AND began_at IS NOT NULL AND ended_at IS NULL",
            ("$at", Now()), ("$exit", exit), ("$unended", unended ? 1 : 0), ("$said", said), ("$act", claim.Act), ("$ordinal", ordinal));
        Changed(write, claim, "UPDATE git_acts SET phase = $phase WHERE id = $act", ("$phase", ActPhases.Ended));
        return StepsIn(write, claim.Act).Single(each => each.Ordinal == ordinal);
    });

    /// <summary>The postconditions read: each begun step's outcome and the act's, <see cref="ActPhases.Reconciled"/>.</summary>
    public void Reconciled(ActClaim claim, string outcome, string said, IReadOnlyDictionary<int, string> steps) => Fenced(claim, write =>
    {
        foreach (var (ordinal, each) in steps)
        {
            write.Execute("UPDATE git_act_steps SET outcome = $outcome WHERE act = $act AND ordinal = $ordinal", ("$outcome", each), ("$act", claim.Act), ("$ordinal", ordinal));
        }

        Changed(write, claim, "UPDATE git_acts SET phase = $phase, outcome = $outcome, said = $said, reconciled_at = $at WHERE id = $act",
            ("$phase", ActPhases.Reconciled), ("$outcome", outcome), ("$said", said), ("$at", Now()));
    });

    /// <summary>The record closed, reconciled first: nothing more is written of it but its recovery's expiry at <paramref name="expires"/>.</summary>
    /// <exception cref="ActRefusedException">It is not reconciled.</exception>
    public void Close(ActClaim claim, DateTimeOffset? expires) => Fenced(claim, write =>
    {
        var phase = write.Scalar<string>("SELECT phase FROM git_acts WHERE id = $act", ("$act", claim.Act));
        if (phase == ActPhases.Closed) return;
        if (phase != ActPhases.Reconciled) throw new ActRefusedException(claim.Act, $"it is {phase}, and an act closes once its postconditions are read");
        Changed(write, claim, "UPDATE git_acts SET phase = $phase, closed_at = $at, expires_at = $expires WHERE id = $act",
            ("$phase", ActPhases.Closed), ("$at", Now()), ("$expires", Moments.Write(expires)));
    });

    /// <summary>A closed act's recovery let go of: its act ref deleted at its id, or found gone.</summary>
    public void Expired(string act)
    {
        using var store = Writer();
        store.Write(Family, write => write.Execute(
            "UPDATE git_acts SET expired_at = $at WHERE id = $act AND closed_at IS NOT NULL AND expired_at IS NULL", ("$at", Now()), ("$act", act)));
    }

    /// <summary>
    /// Take up an act whose claim's run is dead by the lease's test, in one guarded update that writes this run and raises the generation:
    /// refused, nothing written, where the act is gone or closed, its claim is this run's own, the generation moved since
    /// <paramref name="generation"/> was read, or its run lives or cannot be told dead.
    /// </summary>
    public ActTaken Take(string act, long generation)
    {
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var found = ActIn(write, act) ?? throw Refused(null, "the journal holds no such act");
                if (!found.Open) throw Refused(found, "it is closed");
                if (string.Equals(found.ClaimRun, Run, StringComparison.Ordinal)) throw Refused(found, "it is this run's own");
                if (found.ClaimGeneration != generation) throw Refused(found, "another reconciler took it first");
                if (RunIn(write, found.ClaimRun) is not { } holder || _alive(holder)) throw Refused(found, "the Daoris process that runs it still runs");
                var taken = write.Execute(
                    """
                    UPDATE git_acts SET claim_run = $mine, claim_generation = claim_generation + 1
                    WHERE id = $act AND claim_run = $run AND claim_generation = $generation AND closed_at IS NULL
                    """,
                    ("$mine", Run), ("$act", act), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, "another reconciler took it first");
                var now = ActIn(write, act)!;
                return new ActTaken(now, new ActClaim(act, Run, now.ClaimGeneration), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return unwritten.Answer;
        }

        static Unwritten Refused(ActRecord? found, string why) => new(new ActTaken(found, null, why));
    }

    // ——— The writes' own parts.

    /// <summary>One write under the claim's check: the act is there, open, and its run and generation are the claim's, which is this journal's run.</summary>
    private T Fenced<T>(ActClaim claim, Func<DesktopWrite, T> work)
    {
        using var store = Writer();
        return store.Write(Family, write =>
        {
            if (!string.Equals(claim.Run, Run, StringComparison.Ordinal)) throw new ActRefusedException(claim.Act, $"this journal writes under run {Run}, not {claim.Run}");
            var act = ActIn(write, claim.Act) ?? throw new ActRefusedException(claim.Act, "the journal holds no such act");
            if (!string.Equals(act.ClaimRun, claim.Run, StringComparison.Ordinal) || act.ClaimGeneration != claim.Generation)
            {
                throw new ActRefusedException(claim.Act, $"run {act.ClaimRun} holds it at generation {act.ClaimGeneration}");
            }

            return work(write);
        });
    }

    private void Fenced(ActClaim claim, Action<DesktopWrite> work) => Fenced<bool>(claim, write =>
    {
        work(write);
        return true;
    });

    /// <summary>An update of the act's row under its claim, named in its own condition, which must change exactly that row.</summary>
    private static void Changed(DesktopWrite write, ActClaim claim, string update, params (string Name, object? Value)[] values)
    {
        var changed = write.Execute(
            update + " AND claim_run = $claimRun AND claim_generation = $claimGeneration",
            [.. values, ("$act", claim.Act), ("$claimRun", claim.Run), ("$claimGeneration", claim.Generation)]);
        if (changed != 1) throw new ActRefusedException(claim.Act, "its row changed under the write");
    }

    private static ActRecord? ActIn(DesktopWrite write, string id) =>
        write.Query($"SELECT {ActColumns} FROM git_acts WHERE id = $id", MapAct, ("$id", id)).FirstOrDefault();

    private static IReadOnlyList<ActStep> StepsIn(DesktopWrite write, string act) =>
        write.Query($"SELECT {StepColumns} FROM git_act_steps WHERE act = $act ORDER BY ordinal", MapStep, ("$act", act));

    private static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    // ——— The rows read.

    private static string? Text(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetString(at);
    }

    private static long? Number(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetInt64(at);
    }

    private static ActRecord MapAct(SqliteDataReader row) =>
        new(Text(row, "id")!, (int)Number(row, "format")!.Value, Text(row, "kind")!, Text(row, "press")!, Text(row, "repository")!, Text(row, "checkout")!,
            Text(row, "common_dir")!, Text(row, "plan")!, Text(row, "job")!, Text(row, "claim_run")!, Number(row, "claim_generation")!.Value, Text(row, "phase")!,
            Moments.Read(Text(row, "at")) ?? throw new DesktopStoreException("an act's `at` does not read as a moment"))
        {
            Confirmed = Text(row, "confirmed"),
            Recovery = Text(row, "recovery"),
            Outcome = Text(row, "outcome"),
            Said = Text(row, "said"),
            ReconciledAt = Moments.Read(Text(row, "reconciled_at")),
            ClosedAt = Moments.Read(Text(row, "closed_at")),
            ExpiresAt = Moments.Read(Text(row, "expires_at")),
            ExpiredAt = Moments.Read(Text(row, "expired_at")),
        };

    private static ActStep MapStep(SqliteDataReader row) =>
        new(Text(row, "act")!, (int)Number(row, "ordinal")!.Value, Text(row, "kind")!, Text(row, "arguments")!, Text(row, "postconditions")!, Text(row, "rollback"))
        {
            BeganAt = Moments.Read(Text(row, "began_at")),
            Deadline = Moments.Read(Text(row, "deadline")),
            Pid = (int?)Number(row, "pid"),
            PidStarted = Number(row, "pid_started"),
            EndedAt = Moments.Read(Text(row, "ended_at")),
            ExitCode = (int?)Number(row, "exit_code"),
            Unended = Number(row, "unended") == 1,
            Outcome = Text(row, "outcome"),
            Said = Text(row, "said"),
        };

    /// <summary>A read of a store that holds the family, or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none)
    {
        using var reader = DesktopStoreReader.Open(Location);
        return reader is not null && reader.Version >= Since ? read(reader) : none;
    }

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>The store opened for one write under this journal's run; a wait on its write lock is a leaf one (<see cref="LockWaits.Store"/>).</summary>
    private DesktopStore Writer()
    {
        LockWaits.Waits(LockWaits.Store);
        return DesktopStore.Open(Location, StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });
    }

    private string Now() => Moments.Write(_clock.GetUtcNow());

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back and no revision moves.</summary>
    private sealed class Unwritten(ActTaken answer) : Exception
    {
        public ActTaken Answer { get; } = answer;
    }
}
