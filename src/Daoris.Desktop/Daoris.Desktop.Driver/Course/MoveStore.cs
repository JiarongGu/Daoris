using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The course's move record in the desktop store (FOUNDATION1f-i part 1, D212; the foundation design §3.3, §13.3's family <i>The course</i>,
/// §13.4, §19.1): every move's one durable row in <c>moves</c>, its words in transfer in <c>move_words</c>. Read by nothing yet: rows f-ii to
/// f-vi, h and i adopt it, and until then the one production path that reaches it is the delete of a record, which takes its moves.
/// </summary>
/// <remarks>
/// <para><b>One standing move per record left.</b> The partial unique index <c>moves_standing</c> holds the rows not <c>delivered</c>,
/// <c>fell-back</c> or <c>closed</c>, so of two executors deciding a move of one line, two shells on one home among them, the second's single
/// insert is refused under the write lock its <c>BEGIN IMMEDIATE</c> holds, and it reads the first's row and answers that it stands, writing
/// nothing. A move that ends frees its record, its row kept as the move's evidence, and the record's next move is a new row. No upsert and no
/// read before the insert decides it: the index does.</para>
/// <para><b>The claim is the process run that makes the move</b> (<c>claim_run</c>, a row of <c>store_runs</c>, and
/// <c>claim_generation</c>): every phase, effect mark and outcome is one store transaction that checks the claim first, inside the
/// transaction, so a read, a check and a write never interleave across processes. <b>A move is taken over only from a run dead by the
/// lease's own test</b> (<see cref="ProcessLiveness"/>): its process gone, or alive under another start time, a start time that cannot be read
/// counting as alive; never from the taker's own run, and never from a live one however long it pauses, so the claim holds through an effect
/// its run marked. A take is one guarded update that raises the generation, refused where the generation moved since the taker read it.</para>
/// <para><b>Every external effect is fenced by a mark</b> written under the claim's check before it is made, and its outcome after: a mark
/// with no outcome is the evidence a taker holds, never made again by itself, so no second mark goes over it.</para>
/// <para>Opened for each use and closed, like the bindings' store, under the process's one run (<see cref="StoreProcess"/>); a read never
/// creates the store. It never holds a word's text, a path or a credential, and writes no box beside the row: an older build recovers a move
/// the store carries from the ledger alone (§13.5).</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
/// <param name="alive">Whether a run that wrote the store still lives; null reads <see cref="Lives"/>, the lease's own test. A test hands one, since one process is one run.</param>
/// <param name="run">The run this store writes and claims under; null for the process's own.</param>
/// <param name="kind">The run's kind; null for the process's own.</param>
/// <param name="clock">The clock of every moment written.</param>
public sealed class MoveStore(
    string home, Func<StoreSettled>? settled = null, Func<StoreRun, bool>? alive = null, string? run = null, string? kind = null, TimeProvider? clock = null)
    : IWorkplaceMoveRecords
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "moves";

    /// <summary>The store's step that made the family: a store before it holds no move.</summary>
    private const int Since = 4;

    /// <summary>SQLite's refusal of a row a unique index already holds.</summary>
    private const int UniqueRefused = 2067;

    private static readonly (MoveRecordPhase Phase, string Column)[] Moments =
    [
        (MoveRecordPhase.Decided, "decided_at"), (MoveRecordPhase.Quiescing, "quiescing_at"), (MoveRecordPhase.OldEnded, "old_ended_at"),
        (MoveRecordPhase.Opening, "opening_at"), (MoveRecordPhase.Opened, "opened_at"), (MoveRecordPhase.Copied, "copied_at"),
        (MoveRecordPhase.Verified, "verified_at"), (MoveRecordPhase.Delivering, "delivering_at"), (MoveRecordPhase.Delivered, "delivered_at"),
        (MoveRecordPhase.FellBack, "fell_back_at"), (MoveRecordPhase.Held, "held_at"), (MoveRecordPhase.Closed, "closed_at"),
    ];

    private static readonly string Columns =
        "id, record_left, executor, phase, claim_run, claim_generation, how, from_why, from_by, account, recipe_why, recipe_bound, opens, destination, "
        + "effect, effect_begun, effect_outcome, effect_ended, fell_back_code, held_why, closed_why, "
        + string.Join(", ", Moments.Select(each => each.Column));

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    private readonly Func<StoreRun, bool> _alive = alive ?? Lives;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>The run this store writes and claims under.</summary>
    public string Run { get; } = run ?? StoreProcess.Run;

    string IWorkplaceMoveRecords.StoreLocation => Location;

    /// <summary>
    /// The lease's own test of a run (§13.4): alive while its process runs under the start time it recorded; a start it could not read when it
    /// began counts as alive, as the lease's <i>cannot tell</i> holds.
    /// </summary>
    public static bool Lives(StoreRun run) => run.Started is not { } started || ProcessLiveness.Alive(run.Pid, started);

    // ——— The decision and its claim.

    /// <summary>
    /// Write a move the course decided, claimed by this store's run at generation 1, in one transaction (§13.4 step 1); or, where the record
    /// left already has a standing move, answer that move and write nothing, which the decider reads as the course's <c>Never</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The decision names no record, an executor or a <c>how</c> no move has, or no why or by.</exception>
    public MoveDecided Decide(MoveDecision decision)
    {
        Required(decision.Left, "the record it leaves");
        Known(decision.Executor, MoveExecutors.All, "executor");
        Known(decision.How, MoveHows.All, "how");
        Required(decision.Why, "its why");
        Required(decision.By, "who asked for it");
        try
        {
            using var store = Open();
            return store.Write(Family, write =>
            {
                var at = Now();
                try
                {
                    write.Execute(
                        """
                        INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, account, recipe_why, recipe_bound, decided_at, old_ended_at)
                        VALUES ($left, $executor, $how, $why, $by, $phase, $run, 1, $account, $recipeWhy, $recipeBound, $at, $oldEnded)
                        """,
                        ("$left", decision.Left), ("$executor", decision.Executor), ("$how", decision.How), ("$why", decision.Why), ("$by", decision.By),
                        ("$phase", MovePhases.Word(decision.FromEnded ? MoveRecordPhase.OldEnded : MoveRecordPhase.Decided)), ("$run", Run),
                        ("$account", decision.Account), ("$recipeWhy", decision.RecipeWhy), ("$recipeBound", decision.RecipeBound), ("$at", at),
                        ("$oldEnded", decision.FromEnded ? at : null));
                }
                catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
                {
                    // The partial index refused a second standing move of the record: the standing one is the answer, read under the same
                    // lock, and the transaction is rolled back so nothing is written and no revision moves.
                    var standing = StandingIn(write, decision.Left);
                    if (standing is null) throw;
                    throw new Unwritten(new MoveDecided(standing, null));
                }

                var move = ReadIn(write, write.Scalar<long>("SELECT last_insert_rowid()"))!;
                return new MoveDecided(move, new MoveClaim(move.Id, Run, move.ClaimGeneration));
            });
        }
        catch (Unwritten unwritten)
        {
            return (MoveDecided)unwritten.Answer;
        }
    }

    /// <summary>
    /// Take up a move whose claim's run is dead by the lease's test (§13.4, §19.1), in one guarded update that writes this store's run and
    /// raises the generation: refused, nothing written, where the move is gone or ended, where its claim is this run's own, where the generation
    /// moved since <paramref name="generation"/> was read, or where its run lives or cannot be told dead.
    /// </summary>
    public MoveTaken Take(long move, long generation)
    {
        try
        {
            using var store = Open();
            return store.Write(Family, write =>
            {
                var found = ReadIn(write, move) ?? throw Refused(null, MoveTakeRefusals.Gone);
                if (!found.Stands) throw Refused(found, MoveTakeRefusals.Ended);
                if (string.Equals(found.ClaimRun, Run, StringComparison.Ordinal)) throw Refused(found, MoveTakeRefusals.Own);
                if (found.ClaimGeneration != generation) throw Refused(found, MoveTakeRefusals.Moved);

                // A run the store holds no row of cannot be told dead, so it holds, as a start that cannot be read does.
                if (RunIn(write, found.ClaimRun) is not { } holder || _alive(holder)) throw Refused(found, MoveTakeRefusals.Live);
                var taken = write.Execute(
                    "UPDATE moves SET claim_run = $mine, claim_generation = claim_generation + 1 WHERE id = $id AND claim_run = $run AND claim_generation = $generation",
                    ("$mine", Run), ("$id", move), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, MoveTakeRefusals.Moved);
                var now = ReadIn(write, move)!;
                return new MoveTaken(now, new MoveClaim(move, Run, now.ClaimGeneration), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (MoveTaken)unwritten.Answer;
        }

        static Unwritten Refused(MoveRecord? found, string why) => new(new MoveTaken(found, null, why));
    }

    // ——— The phases, under the claim.

    /// <summary>
    /// Move to a phase that carries nothing of its own (<c>quiescing</c>, <c>old-ended</c>, <c>copied</c>, <c>verified</c>, <c>delivering</c>,
    /// <c>delivered</c>), under the claim's check, forward only (<see cref="MovePhases.Follows"/>). A phase written again changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The phase carries something, and has its own write.</exception>
    /// <exception cref="MoveClaimLostException">The claim no longer holds the move.</exception>
    /// <exception cref="MoveRefusedException">The move does not go to that phase from its own.</exception>
    public MoveRecord Advance(MoveClaim claim, MoveRecordPhase phase)
    {
        if (phase is MoveRecordPhase.Decided or MoveRecordPhase.Opening or MoveRecordPhase.Opened or MoveRecordPhase.FellBack or MoveRecordPhase.Held
            or MoveRecordPhase.Closed)
        {
            throw new ArgumentException($"`{MovePhases.Word(phase)}` is written with what it carries, by its own write", nameof(phase));
        }

        return Step(claim, phase, null, null);
    }

    /// <summary>The open asked under its key, which a lost answer is asked again by (§3.3, §13.4 step 3).</summary>
    public MoveRecord Opening(MoveClaim claim, string opens) => Step(claim, MoveRecordPhase.Opening, ("opens", Required(opens, "the open's key")), null);

    /// <summary>The open's answer: the record it made and every word it moved, written with the phase in one transaction (§13.4 step 3).</summary>
    public MoveRecord Opened(MoveClaim claim, string destination, IReadOnlyList<MoveRecordWord> words) =>
        Step(claim, MoveRecordPhase.Opened, ("destination", Required(destination, "the record its open made")), write =>
        {
            foreach (var word in words)
            {
                write.Execute(
                    "INSERT INTO move_words (move, transfer, word, producer, position) VALUES ($move, $transfer, $word, $producer, $position)",
                    ("$move", claim.Move), ("$transfer", Required(word.Transfer, "a word's transfer")), ("$word", Required(word.Word, "a word's id")),
                    ("$producer", word.Producer), ("$position", word.Position));
            }
        });

    /// <summary>The move ended on the old part, with its code.</summary>
    public MoveRecord FallBack(MoveClaim claim, string code) => Step(claim, MoveRecordPhase.FellBack, ("fell_back_code", Required(code, "its code")), null);

    /// <summary>Held for the person, with its reason: it stands until it closes <c>resolved</c> by §3.3's one rule.</summary>
    public MoveRecord Hold(MoveClaim claim, string why) => Step(claim, MoveRecordPhase.Held, ("held_why", Required(why, "its reason")), null);

    /// <summary>Closed, with its reason (<see cref="MoveCloseWhy"/>), freeing its record for the next move and kept as evidence.</summary>
    public MoveRecord Close(MoveClaim claim, string why) => Step(claim, MoveRecordPhase.Closed, ("closed_why", Known(why, MoveCloseWhy.All, "close's reason")), null);

    // ——— The effect mark.

    /// <summary>
    /// Mark an external effect before it is made (§13.4 step 2), under the claim's check: refused where the move has ended or is held, and
    /// where the last effect it marked has no outcome, which is a taker's hold and never made again by itself.
    /// </summary>
    public MoveRecord MarkEffect(MoveClaim claim, string effect)
    {
        Known(effect, MoveEffects.All, "effect");
        using var store = Open();
        return store.Write(Family, write =>
        {
            var move = Claimed(write, claim);
            if (!move.Stands || move.Phase is null or MoveRecordPhase.Held) throw new MoveRefusedException(claim.Move, $"a move at `{move.PhaseWord}` makes no effect");
            if (move.Effect is { Outcome: null } open)
            {
                throw new MoveRefusedException(claim.Move, $"its {open.What} begun at {open.Begun} has no outcome recorded, and an effect never recorded is held, never made again by itself");
            }

            Fenced(write, claim, "effect = $effect, effect_begun = $at, effect_outcome = NULL, effect_ended = NULL", ("$effect", effect), ("$at", Now()));
            return ReadIn(write, claim.Move)!;
        });
    }

    /// <summary>Record the outcome of the effect last marked, once it is known, under the claim's check.</summary>
    public MoveRecord EffectEnded(MoveClaim claim, string outcome)
    {
        Required(outcome, "the effect's outcome");
        using var store = Open();
        return store.Write(Family, write =>
        {
            var move = Claimed(write, claim);
            if (!move.Stands) throw new MoveRefusedException(claim.Move, $"a move at `{move.PhaseWord}` takes no write");
            if (move.Effect is not { Outcome: null }) throw new MoveRefusedException(claim.Move, "it has no effect begun whose outcome is not recorded");
            Fenced(write, claim, "effect_outcome = $outcome, effect_ended = $at", ("$outcome", outcome), ("$at", Now()));
            return ReadIn(write, claim.Move)!;
        });
    }

    // ——— The finds, read without writing or making anything.

    /// <summary>The record's one standing move, or null: none stands, or the home has no store, or one from before the family.</summary>
    public MoveRecord? Standing(string left) =>
        Reading(reader => reader.Query($"SELECT {Columns} FROM moves WHERE record_left = $left AND {DesktopStore.StandingMoves}", Map, ("$left", left)).FirstOrDefault(), null);

    /// <summary>A move by its id, or null.</summary>
    public MoveRecord? Read(long id) => Reading(reader => reader.Query($"SELECT {Columns} FROM moves WHERE id = $id", Map, ("$id", id)).FirstOrDefault(), null);

    /// <summary>Every move of a record left, oldest first: the standing one and the ended ones kept as evidence.</summary>
    public IReadOnlyList<MoveRecord> Of(string left) =>
        Reading(reader => reader.Query($"SELECT {Columns} FROM moves WHERE record_left = $left ORDER BY id", Map, ("$left", left)), []);

    /// <summary>The words a move's open moved, in their order.</summary>
    public IReadOnlyList<MoveRecordWord> WordsOf(long move) =>
        Reading(
            reader => reader.Query(
                "SELECT word, producer, transfer, position, disposition FROM move_words WHERE move = $move ORDER BY position, transfer",
                row => new MoveRecordWord(row.GetString(0), row.IsDBNull(1) ? null : row.GetString(1), row.GetString(2), row.GetInt32(3), row.IsDBNull(4) ? null : row.GetString(4)),
                ("$move", move)),
            []);

    // ——— Deleted with the record (§3.3, through the workplace's port).

    /// <summary>Which of these records left the store holds a move of; none for a home with no store, which this never makes.</summary>
    public IReadOnlyList<string> Holding(IReadOnlyCollection<string> sessions) =>
        sessions.Count == 0
            ? []
            : Reading(
                reader => (IReadOnlyList<string>)[.. sessions.Where(session =>
                    reader.Query("SELECT 1 FROM moves WHERE record_left = $left LIMIT 1", row => row.GetInt32(0), ("$left", session)).Count > 0)],
                []);

    /// <summary>
    /// The moves of these deleted records gone, with their words, in one transaction; the records that held one named. Nothing is made for a
    /// home with no store, and a store that will not take the delete throws, every move left.
    /// </summary>
    public IReadOnlyList<string> Forget(IReadOnlyCollection<string> sessions)
    {
        if (Holding(sessions).Count == 0) return [];
        using var store = Open();
        return store.Write(Family, write =>
        {
            var went = new List<string>();
            foreach (var session in sessions)
            {
                write.Execute("DELETE FROM move_words WHERE move IN (SELECT id FROM moves WHERE record_left = $left)", ("$left", session));
                if (write.Execute("DELETE FROM moves WHERE record_left = $left", ("$left", session)) > 0) went.Add(session);
            }

            return (IReadOnlyList<string>)went;
        });
    }

    IReadOnlyList<string> IWorkplaceMoveRecords.MovesOf(IReadOnlyCollection<string> sessions) => Holding(sessions);

    IReadOnlyList<string> IWorkplaceMoveRecords.ForgetMoves(IReadOnlyCollection<string> sessions) => Forget(sessions);

    // ——— The writes' own parts.

    /// <summary>One phase written under the claim's check, inside its own transaction, with what it carries; a phase written again is the first write's.</summary>
    private MoveRecord Step(MoveClaim claim, MoveRecordPhase to, (string Column, string Value)? carried, Action<DesktopWrite>? also)
    {
        try
        {
            using var store = Open();
            return store.Write(Family, write =>
            {
                var move = Claimed(write, claim);
                if (move.Phase == to) throw new Unwritten(move);
                if (move.Phase is not { } from) throw new MoveRefusedException(claim.Move, $"its phase `{move.PhaseWord}` is a newer Daoris's, which this build writes nothing over");
                if (!MovePhases.Follows(from, to)) throw new MoveRefusedException(claim.Move, $"a move at `{move.PhaseWord}` does not go to `{MovePhases.Word(to)}`");
                var column = Moments.First(each => each.Phase == to).Column;
                Fenced(
                    write, claim, $"phase = $phase, {column} = $at" + (carried is { } set ? $", {set.Column} = $carried" : ""),
                    ("$phase", MovePhases.Word(to)), ("$at", Now()), ("$carried", carried?.Value));
                also?.Invoke(write);
                return ReadIn(write, claim.Move)!;
            });
        }
        catch (Unwritten unwritten)
        {
            return (MoveRecord)unwritten.Answer;
        }
    }

    /// <summary>The claim's check, inside the write's transaction: the move is there and its run and generation are the claim's, which is this store's run.</summary>
    private MoveRecord Claimed(DesktopWrite write, MoveClaim claim)
    {
        if (!string.Equals(claim.Run, Run, StringComparison.Ordinal)) throw new MoveClaimLostException(claim, $"this store writes under run {Run}");
        var move = ReadIn(write, claim.Move) ?? throw new MoveClaimLostException(claim, "the move is gone, with its record");
        if (!string.Equals(move.ClaimRun, claim.Run, StringComparison.Ordinal) || move.ClaimGeneration != claim.Generation)
        {
            throw new MoveClaimLostException(claim, $"run {move.ClaimRun} holds it at generation {move.ClaimGeneration}");
        }

        return move;
    }

    /// <summary>An update of the claimed row alone, its claim in its own condition, which must change exactly that row.</summary>
    private static void Fenced(DesktopWrite write, MoveClaim claim, string set, params (string Name, object? Value)[] values)
    {
        var changed = write.Execute(
            $"UPDATE moves SET {set} WHERE id = $id AND claim_run = $claimRun AND claim_generation = $claimGeneration",
            [.. values, ("$id", claim.Move), ("$claimRun", claim.Run), ("$claimGeneration", claim.Generation)]);
        if (changed != 1) throw new MoveClaimLostException(claim, "its row changed under the write");
    }

    private static MoveRecord? ReadIn(DesktopWrite write, long id) =>
        write.Query($"SELECT {Columns} FROM moves WHERE id = $id", Map, ("$id", id)).FirstOrDefault();

    private static MoveRecord? StandingIn(DesktopWrite write, string left) =>
        write.Query($"SELECT {Columns} FROM moves WHERE record_left = $left AND {DesktopStore.StandingMoves}", Map, ("$left", left)).FirstOrDefault();

    private static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    private static MoveRecord Map(SqliteDataReader row)
    {
        string? Text(string column)
        {
            var at = row.GetOrdinal(column);
            return row.IsDBNull(at) ? null : row.GetString(at);
        }

        var moments = new Dictionary<MoveRecordPhase, string>();
        foreach (var (phase, column) in Moments)
        {
            if (Text(column) is { } moment) moments[phase] = moment;
        }

        var bound = row.GetOrdinal("recipe_bound");
        return new MoveRecord(
            row.GetInt64(row.GetOrdinal("id")), Text("record_left")!, Text("executor")!, Text("phase")!, Text("claim_run")!,
            row.GetInt64(row.GetOrdinal("claim_generation")), Text("how")!, Text("from_why")!, Text("from_by")!)
        {
            Moments = moments,
            Opens = Text("opens"),
            Destination = Text("destination"),
            Account = Text("account"),
            RecipeWhy = Text("recipe_why"),
            RecipeBound = row.IsDBNull(bound) ? null : row.GetInt32(bound),
            FellBackCode = Text("fell_back_code"),
            HeldWhy = Text("held_why"),
            ClosedWhy = Text("closed_why"),
            Effect = Text("effect") is { } what ? new MoveEffect(what, Text("effect_begun") ?? "", Text("effect_outcome"), Text("effect_ended")) : null,
        };
    }

    /// <summary>A read of a store that holds the family, or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none)
    {
        using var reader = DesktopStoreReader.Open(Location);
        return reader is { Version: >= Since } ? read(reader) : none;
    }

    private DesktopStore Open() => DesktopStore.Open(Location, StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });

    private string Now() => _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

    private static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"a move needs {what}") : value;

    private static string Known(string? value, IReadOnlyList<string> words, string what) =>
        value is not null && words.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new ArgumentException($"`{value}` is no move's {what}: one of {string.Join(", ", words)}");

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back, no revision moved and no wake touched.</summary>
    private sealed class Unwritten(object answer) : Exception
    {
        public object Answer { get; } = answer;
    }
}
