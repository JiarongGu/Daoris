using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// Where histories meet: a machine rebases what it has not pushed on top of what a remote accepted, and a remote accepts a
/// quest's operations only when nothing reached that quest since the push is rebased. Both halves act on the one log table.
/// </summary>
internal sealed class QuestLogSync(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Keep an operation another machine made, under ITS machine and sequence — what a remote receives
    /// and what a machine fetches. <paramref name="number"/> is where a remote placed it; a remote
    /// itself keeps none, because its own position is the number.
    /// </summary>
    /// <returns>The position it is kept at — on a remote, the number it is given.</returns>
    internal async Task<long> KeepAsync(
        QuestOperation operation, long? number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload, remote)
            VALUES ($quest, $kind, $machine, $sequence, $at, $payload, $remote)
            RETURNING position
            """;
        command.Parameters.AddWithValue("$quest", operation.Quest);
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue("$machine", operation.Machine);
        command.Parameters.AddWithValue("$sequence", operation.Sequence);
        command.Parameters.AddWithValue("$at", Moments.Write(operation.At));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        command.Parameters.AddWithValue("$remote", (object?)number ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Where this store keeps an operation, by the machine and sequence that name it anywhere.</summary>
    internal async Task<long?> PositionOfAsync(
        string machine, long sequence, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "SELECT position FROM quest_log WHERE machine = $machine AND sequence = $sequence";
        command.Parameters.AddWithValue("$machine", machine);
        command.Parameters.AddWithValue("$sequence", sequence);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } position and not DBNull
            ? Convert.ToInt64(position)
            : null;
    }

    /// <summary>
    /// Replay one quest — accepted first, pending on top — and rewrite what did not survive, each kind by
    /// <see cref="QuestLog.Lost"/>: a move becomes a conflict, a publish of an ask already held is the same
    /// ask again, and a follow-up that only a lost close published goes with it. A wait made on this machine's
    /// take that lost goes with the take, and the take's conflict names it; one made after the take is
    /// rewritten is refused by the store, and one an older build made then is lost here as the claim reads.
    /// Answers the conflicts made.
    /// </summary>
    internal async Task<IReadOnlyList<QuestOperation>> RebaseAsync(
        string id, SqliteTransaction transaction, CancellationToken ct)
    {
        var conflicts = new List<QuestOperation>();
        Quest? quest = null;

        // Once this machine's take has lost, its later moves on the quest are made on a claim it never
        // held: an offline session that finished would otherwise close the quest over the winner's
        // take, because the table allows done from taken. Every pending operation is this machine's.
        // A wait made on that take is part of its move: applied to the winner's quest, it would
        // hold the winner's session on a question it never asked. It goes with the take, and the take's
        // conflict, the loss this machine reports, names the question, so whoever asked learns the quest
        // no longer waits on it. A move or a wait made after the pass that rewrote the take never meets this
        // rule: the store's verbs refuse it as the claim reads, so it is never pending here.
        var claimLost = false;
        (long Position, int Reported)? lostTake = null;
        // The history as this rebase leaves it so far: what QuestLog.OnALostTake reads this machine's claim from.
        var replayed = new List<QuestOperation>();
        var losses = new QuestRebaseLosses(replayed, store.Machine);
        var history = await store.HistoryAsync(id, transaction, ct).ConfigureAwait(false);
        // A take this machine made to bring its lost take forward stands only with the dismissal marked with
        // it, written in the same write: a conflict another machine dismissed first authorizes no take, so where the mark would land
        // nowhere, the take loses too, kept as a conflict like any lost take, and the mark goes with it as the take's own.
        var bringing = history
            .Where(operation => operation is { Number: null, Kind: QuestOperationKind.Dismissed, Forward: not null })
            .DistinctBy(operation => operation.Forward)
            .ToDictionary(operation => operation.Forward!);
        foreach (var operation in history)
        {
            var lostClaim = claimLost && operation.Number is null && QuestLog.TakersOwn(operation);
            var unbrought = !lostClaim && operation is { Kind: QuestOperationKind.Taken, Number: null }
                            && bringing.TryGetValue(new QuestOperationRef(operation.Machine, operation.Sequence), out var mark)
                            && QuestLog.Applies(quest, operation) && !QuestLog.Brings(QuestLog.Step(quest, operation), mark);

            // A stray: a done, a decline or a wait an older build made after the take's conflict
            // is written, which the remote refuses at every pass, so the circle never pushed clean. Judged by
            // the rule the store's verbs and the remote judge by, and lost by its kind's rule as one made on a take this
            // rebase finds lost is. A forgotten wait is named on no conflict: the take's is already written, a remote may
            // have numbered it, and no machine rewrites a numbered operation. The remote never kept the wait, so no other
            // machine's copy of the quest waits on the question.
            var stray = !lostClaim && operation.Number is null && QuestLog.OnALostTake(quest, replayed, operation);
            if (!lostClaim && !stray && !unbrought && (operation.Number is not null || QuestLog.Applies(quest, operation)))
            {
                quest = QuestLog.Applies(quest, operation) ? QuestLog.Step(quest, operation) : quest;
                replayed.Add(operation);
                continue;
            }

            var position = (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct)
                .ConfigureAwait(false))!.Value;

            // Each kind by its rule (QuestLog.Lost), which says why each is forgotten, kept or made a conflict.
            var loss = QuestLog.Lost(operation.Kind);
            if (loss == QuestLoss.Kept)
            {
                replayed.Add(operation);
                continue;
            }

            if (loss == QuestLoss.Forgotten)
            {
                await ForgetOperationAsync(position, transaction, ct).ConfigureAwait(false);
                conflicts.AddRange(await LostAsync(operation, quest, lostClaim, losses, transaction, ct).ConfigureAwait(false));
                continue;
            }

            // A move. On a quest another machine deleted first it is kept as a conflict on a quest no list shows,
            // so this machine's claim reads lost and its driver stops the session. A decline made while open that lost
            // to a take is kept like any losing move, with its reason: the conflict says it attempted a
            // decline, and the flag, a decline's, goes with the kind. So does a take's source: a conflict carries none,
            // and is reported as the log keeps it.
            var lost = operation with
            {
                Kind = QuestOperationKind.Conflict, Attempted = QuestTransitions.Target(operation.Kind), WhileOpen = false, Close = null,
                Submission = null,
            };

            // A step's take made on a submission the step no longer grew from: another machine's chain published
            // the step anew on other work, so the take's own words alone would not say why it lost. Read from the stamp and the step
            // alone, as the source's own history may not be rewritten yet. A take that lost to the person's reopen of the source
            // (its rebind moves the step's GrewFrom too) keeps the take's words.
            if (operation.Kind == QuestOperationKind.Taken && operation.Submission is { } stamped
                && quest is { Status: QuestStatus.Open, SourceReopened: null, Parent: { Length: > 0 } source } && stamped != quest.GrewFrom
                && !QuestLog.LostToTheReopen(replayed.Append(lost), store.Machine))
            {
                lost = lost with { Note = QuestFollowUps.StepPublishedAnew(source, operation.Note), NoteParts = null };
            }

            await RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
            quest = quest is null ? null : QuestLog.Step(quest, lost);
            replayed.Add(lost);
            conflicts.Add(lost);
            if (operation.Kind == QuestOperationKind.Taken)
            {
                claimLost = true;
                lostTake = (position, conflicts.Count - 1);
            }

            conflicts.AddRange(await LostAsync(operation, quest, lostClaim, losses, transaction, ct).ConfigureAwait(false));
        }

        // The take's conflict names what went with it, after its own note. It is rewritten before
        // it is pushed, so every machine keeps the one note.
        if (lostTake is { } take && losses.Any)
        {
            // A note rewritten here has no parts of its own, so it keeps none: a stale lead would word a note it no longer is.
            var named = conflicts[take.Reported] with { Note = losses.Named(conflicts[take.Reported].Note), NoteParts = null };
            await RewriteAsync(take.Position, named, transaction, ct).ConfigureAwait(false);
            conflicts[take.Reported] = named;
            quest = quest is null ? null : quest with
            {
                Conflicts = quest.Conflicts
                    .Select(c => c.Machine == named.Machine && c.Sequence == named.Sequence ? c with { Note = named.Note } : c)
                    .ToList(),
            };
        }

        // A quest a fetched delete ended leaves the cache, as it left every other machine's.
        if (quest is not null) await store.WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
        else await DropCacheAsync(id, transaction, ct).ConfigureAwait(false);
        return conflicts;
    }

    /// <summary>What else a lost operation's kind takes with it in the rebase's write (<see cref="IQuestOperation.LostAsync"/>).</summary>
    private async Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses, SqliteTransaction transaction,
        CancellationToken ct) =>
        await QuestOperations.Registered.For(operation.Kind)!.LostAsync(new QuestTx(store, transaction, ct), operation, standing, takeLost, losses)
            .ConfigureAwait(false);

    /// <summary>
    /// The circle's quests where this machine holds a done, a decline, a wait or a set-up not yet pushed beside a take of its
    /// own that is a conflict: where a stray can be. <see cref="RebaseAsync"/> judges each operation by
    /// <see cref="QuestLog.OnALostTake"/>, so a quest this finds and nothing strays on is replayed as it stands.
    /// </summary>
    internal async Task<IReadOnlyList<string>> StrayQuestsAsync(string circle, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT DISTINCT stray.quest FROM quest_log AS stray
            JOIN quest_log AS asked ON asked.position = (
              SELECT MIN(first.position) FROM quest_log AS first
              WHERE first.quest = stray.quest AND first.kind = 'published')
            WHERE stray.remote IS NULL AND stray.machine = $machine AND stray.kind IN ('done', 'declined', 'waited', 'setup')
              AND json_extract(asked.payload, '$.workspace') = $workspace COLLATE NOCASE
              AND EXISTS (
                SELECT 1 FROM quest_log AS lost
                WHERE lost.quest = stray.quest AND lost.machine = $machine AND lost.kind = 'conflict'
                  AND json_extract(lost.payload, '$.attempted') = 'Taken' COLLATE NOCASE)
            ORDER BY stray.quest
            """;
        command.Parameters.AddWithValue("$machine", store.Machine);
        command.Parameters.AddWithValue("$workspace", circle);

        var quests = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(reader.GetString(0));
        return quests;
    }

    /// <summary>Take a quest's row out of the cache — the replay says there is no quest.</summary>
    internal async Task DropCacheAsync(string id, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What this machine has not pushed for a workspace's quests, in the order it is made — only for
    /// quests whose receiver <paramref name="shared"/> says may leave the machine.
    /// </summary>
    /// <param name="shared">
    /// Whether a receiver is joined in this workspace — here or on a teammate's machine. Silence means
    /// local: a quest to anything else never appears here, however long it waits.
    /// </param>
    /// <remarks>
    /// The receiver and the circle are read from the quest's own first publish in the log, not from the
    /// cache: a deleted quest has no row, and its tombstone must travel all the same. The two
    /// agree for every quest that has one, since the row is the publish replayed.
    /// </remarks>
    public Task<IReadOnlyList<QuestOperation>> PendingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<QuestOperation>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {QuestStore.OperationColumns}, json_extract(asked.payload, '$.to') FROM quest_log
                JOIN quest_log AS asked ON asked.position = (
                  SELECT MIN(first.position) FROM quest_log AS first
                  WHERE first.quest = quest_log.quest AND first.kind = 'published')
                WHERE quest_log.remote IS NULL
                  AND json_extract(asked.payload, '$.workspace') = $workspace COLLATE NOCASE
                ORDER BY quest_log.position
                """;
            command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));

            var pending = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (shared(reader.GetString(7))) pending.Add(ReadOperation(reader));
            }

            return pending;
        }, ct);

    /// <summary>Record the numbers a push is given — the operations stop being pending.</summary>
    public Task AcceptedAsync(IReadOnlyList<QuestAcceptance> accepted, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            foreach (var acceptance in accepted)
            {
                if (await PositionOfAsync(acceptance.Machine, acceptance.Sequence, transaction, inside).ConfigureAwait(false)
                    is { } position)
                {
                    await NumberAsync(position, acceptance.Number, transaction, inside).ConfigureAwait(false);
                }
            }

            return accepted.Count;
        }, ct);

    /// <summary>
    /// Where a circle stands: what this machine has not pushed, the quests carrying a conflict,
    /// and how its last pass ended.
    /// </summary>
    /// <param name="shared">Whether a receiver may leave the machine — what <see cref="PendingAsync"/> reads.
    /// What never leaves is not ahead of anything.</param>
    public async Task<QuestStanding> StandingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var ahead = (await PendingAsync(circle, shared, ct).ConfigureAwait(false)).Count;
        return await _db.RunAsync(async () =>
        {
            var conflicts = new List<string>();
            await using (var command = _db.Command())
            {
                command.CommandText =
                    "SELECT id FROM quests WHERE workspace = $workspace COLLATE NOCASE AND conflicts <> '[]' ORDER BY updated DESC";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) conflicts.Add(reader.GetString(0));
            }

            await using (var command = _db.Command())
            {
                command.CommandText = "SELECT synced, tried, behind, problem FROM quest_passes WHERE workspace = $workspace";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return new QuestStanding(
                        ahead,
                        ReadLinks(reader.GetString(2)),
                        conflicts,
                        reader.IsDBNull(0) ? null : Moments.ReadRequired(reader.GetString(0)),
                        Moments.ReadRequired(reader.GetString(1)),
                        reader.IsDBNull(3) ? null : reader.GetString(3));
                }
            }

            return new QuestStanding(ahead, [], conflicts, Synced: null, Tried: null, Problem: null);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>What this store accepted after <paramref name="since"/>, in order — numbered by position.</summary>
    /// <param name="limit">A page; <see cref="QuestFetch.More"/> says whether another follows.</param>
    public Task<QuestFetch> OperationsSinceAsync(long since, int limit = 500, CancellationToken ct = default) =>
        _db.RunAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {QuestStore.OperationColumns}, position FROM quest_log
                WHERE position > $since ORDER BY position LIMIT $take
                """;
            command.Parameters.AddWithValue("$since", since);
            command.Parameters.AddWithValue("$take", limit + 1);

            var operations = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                operations.Add(ReadOperation(reader) with { Number = reader.GetInt64(7) });
            }

            var more = operations.Count > limit;
            var page = more ? operations[..limit] : operations;
            return new QuestFetch(page, page.Count == 0 ? since : page[^1].Number!.Value, more);
        }, ct);

    /// <summary>
    /// Judge a push, quest by quest: <b>behind</b> when anything reached the quest after
    /// <paramref name="base"/> that this push did not carry, or it is a step whose source holds no submission it grew from here
    /// (<see cref="AwaitsItsSourceAsync"/>), or, naming none, whose parent is not done here;
    /// <b>refused</b> when an operation does not
    /// apply through the table, is made on a take its machine lost (<see cref="QuestLog.OnALostTake"/>),
    /// or is a publish that fails <paramref name="judge"/>; otherwise every operation is
    /// kept under the machine that made it and numbered.
    /// </summary>
    /// <param name="base">The number the pushing machine rebased on — its cursor.</param>
    /// <param name="judge">The exchange's say over a publish, null when it is fit.</param>
    /// <param name="workspaceOf">Where a publish is filed HERE — the receiving side's wiring, never the push's.</param>
    public Task<QuestPush> ReceiveAsync(
        long @base, IReadOnlyList<QuestOperation> pushed, Func<Quest, string?> judge, Func<Quest, string> workspaceOf,
        CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            var accepted = new List<QuestAcceptance>();
            var behind = new List<string>();
            var refused = new List<QuestPushRefusal>();
            var carried = pushed.Select(o => (o.Machine, o.Sequence)).ToHashSet();

            foreach (var group in pushed.GroupBy(o => o.Quest, StringComparer.Ordinal))
            {
                var fresh = new List<QuestOperation>();
                foreach (var operation in group)
                {
                    // Already here — a push retried after its answer is lost: the number it is given.
                    if (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, inside).ConfigureAwait(false)
                        is { } held)
                    {
                        accepted.Add(new(operation.Machine, operation.Sequence, held));
                    }
                    else
                    {
                        fresh.Add(operation);
                    }
                }

                if (fresh.Count == 0) continue;
                if (await MovedSinceAsync(group.Key, @base, carried, transaction, inside).ConfigureAwait(false)
                    || await AwaitsItsSourceAsync(fresh[0], transaction, inside).ConfigureAwait(false))
                {
                    behind.Add(group.Key);
                    continue;
                }

                var history = await store.HistoryAsync(group.Key, transaction, inside).ConfigureAwait(false);
                var quest = QuestLog.Replay(history);
                // Whether this deployment has held the quest at all — a delete leaves a history and no quest.
                var known = history.Count > 0;
                string? why = null;
                var staged = new List<QuestOperation>();
                foreach (var operation in fresh)
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = workspaceOf(operation.Published) } };
                    why = filed.Published is { } asked ? judge(asked) : null;

                    var onDeleted = quest is null && known && QuestOperations.Registered.For(filed.Kind) is { KeptOnADeletedQuest: true };
                    if (why is null && !onDeleted && !QuestLog.Applies(quest, filed))
                    {
                        why = quest is null
                            ? known
                                ? $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which was deleted here."
                                : $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which this deployment has never had published."
                            : filed.WhileOpen
                                // Named as the decline it is, so the refusal its machine relays says why it did not land; a
                                // publication's rebind likewise.
                                ? $"`{KindText(filed.Kind)}` applies to quest `#{group.Key}` only while it is open (`whileOpen`), "
                                  + $"and it is {quest.Status} here, so the quest stays {quest.Status}."
                                : filed is { Kind: QuestOperationKind.Taken, Submission: { } found } && quest.Status == QuestStatus.Open
                                    // A take made on the step as it grew from work the person reopened since.
                                    ? $"`taken` on quest `#{group.Key}` was made on the step as it grew from submission "
                                      + $"`{found.Machine}:{found.Sequence}`, and the person's reopen has moved the step from that work since, "
                                      + "so the take does not apply and the step stays Open."
                                    : $"`{KindText(filed.Kind)}` does not apply to quest `#{group.Key}`, which is {quest.Status}.";
                    }

                    // A done, a decline or a wait from a machine whose take on the quest lost: made after the
                    // pass that found the take lost, by a build whose store does not refuse it. The table
                    // allows it, and applied it would close or hold the winner's quest. Refused by the rule the store
                    // judges by, in the words the store's refusal is answered in, so the pushing machine's pass relays the
                    // lost take and its stand-down.
                    // On an open step the take is lost to the person's reopen, and is said so; or it lost with the
                    // work it rests on, the step published anew on another submission, said as that.
                    if (why is null && QuestLog.OnALostTake(quest, [.. history, .. staged], filed))
                    {
                        var open = quest is { Status: QuestStatus.Open };
                        var reopened = open && QuestLog.LostToTheReopen([.. history, .. staged], filed.Machine);
                        why = QuestExchange.LostTake(group.Key, filed.Kind == QuestOperationKind.Waited
                            ? $"so it does not wait on `#{filed.Note}`. The remote kept nothing this push carried for the quest; "
                              + $"`#{filed.Note}` stays a quest of its own."
                            : $"so this `{KindText(filed.Kind)}` is not this machine's to make. The remote kept nothing this push "
                              + "carried for the quest.", reopened, outgrown: open && !reopened);
                    }

                    if (why is not null) break;
                    if (!onDeleted) quest = QuestLog.Step(quest, filed);
                    known = true;
                    staged.Add(filed);
                }

                if (why is not null)
                {
                    refused.Add(new(group.Key, why));
                    continue;
                }

                foreach (var operation in staged)
                {
                    var number = await KeepAsync(operation, number: null, transaction, inside).ConfigureAwait(false);
                    accepted.Add(new(operation.Machine, operation.Sequence, number));
                }

                if (quest is not null) await store.WriteCacheAsync(quest, transaction, inside).ConfigureAwait(false);
                else await DropCacheAsync(group.Key, transaction, inside).ConfigureAwait(false);
            }

            return new QuestPush(accepted, behind, refused);
        }, ct);

    /// <summary>
    /// Whether a pushed step's publication waits on the submission it grew from: this store holds the step's
    /// source quest but not that submission, so the source's own operations did not land in this push (behind, or refused). Kept alone,
    /// the step would stand here on work its machine's rebase may yet lose with a take, as a take lost to the person's reopen loses its
    /// submission and the step it published (the parent behind). Answered behind, so the machine goes round:
    /// its rebase forgets the step with the submission, or pushes it again beside it. The source's group comes first in a push, since its
    /// submission is written before the step's publication, so a submission kept by this push is held here by then. A source this store
    /// never held (its receiver stays on its machine) waits on nothing.
    /// <para>A step naming no source is published by its parent's close, in the close's own write, so it waits until this
    /// store holds the parent done; a done is terminal here, so once held it is held for good. Kept alone, it would stand on a close the
    /// machine's rebase loses, as for a lost done's follow-up; answered behind, the machine goes round and its
    /// rebase forgets the follow-up with the done (<see cref="ForgetFollowUpsAsync"/>), or pushes the done again beside it. A parent deleted
    /// here is never done, since a delete takes only an open quest nobody moved, so its follow-up waits too, and goes as the done does.</para>
    /// </summary>
    private async Task<bool> AwaitsItsSourceAsync(QuestOperation first, SqliteTransaction transaction, CancellationToken ct) =>
        first is { Kind: QuestOperationKind.Published, Published: { Parent: { Length: > 0 } parent } step }
        && await store.HistoryAsync(parent, transaction, ct).ConfigureAwait(false) is { Count: > 0 } history
        && (step.GrewFrom is { } source
            ? await PositionOfAsync(source.Machine, source.Sequence, transaction, ct).ConfigureAwait(false) is null
            // A step naming no source is published by its parent's close (a chain's next step, or a set-up step, which
            // follows a done one), so it waits until this store holds the parent done: a close this push did not land here.
            : QuestLog.Replay(history) is not { Status: QuestStatus.Done });

    /// <summary>Whether anything this push did not carry reached a quest after <paramref name="base"/>.</summary>
    private async Task<bool> MovedSinceAsync(
        string quest, long @base, IReadOnlySet<(string Machine, long Sequence)> carried,
        SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "SELECT machine, sequence FROM quest_log WHERE quest = $quest AND position > $base";
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$base", @base);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!carried.Contains((reader.GetString(0), reader.GetInt64(1)))) return true;
        }

        return false;
    }

    internal async Task NumberAsync(long position, long number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "UPDATE quest_log SET remote = $number WHERE position = $position";
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Drop one pending operation a rebase found is nobody's decision (a quest's clear is <see cref="QuestStore.ForgetAsync"/>).</summary>
    internal async Task ForgetOperationAsync(long position, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM quest_log WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Rewrite a pending operation in place — a rebase rewriting a commit nobody else has seen.</summary>
    internal async Task RewriteAsync(
        long position, QuestOperation operation, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE quest_log SET kind = $kind, payload = $payload WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
