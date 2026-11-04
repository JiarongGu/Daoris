using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A person's dismissal of a conflict (SYNC6c), and their bring-forward of this machine's lost take, which writes a take and the
/// dismissal marked with it (SYNCMERGE1a). A dismissal travels like any operation, so the conflict goes on every machine; it moves no
/// status.
/// </summary>
[QuestOperation("dismissed", QuestOperationPhase.Log)]
internal sealed class QuestDismissOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Dismissed;

    /// <summary>
    /// The conflict it names, and nothing else but, where it brought that take forward, the take it made: written only when set, so a
    /// plain dismissal reads as it did before the mark.
    /// </summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Dismisses is { } dismisses) QuestStore.WriteRef(writer, "dismisses", dismisses);
        if (operation.Forward is { } forward) QuestStore.WriteRef(writer, "forward", forward);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Dismisses = payload.TryGetProperty("dismisses", out var named)
            ? new QuestOperationRef(named.GetProperty("machine").GetString() ?? "", named.GetProperty("sequence").GetInt64())
            : null,
        Forward = payload.TryGetProperty("forward", out var forward) ? QuestStore.ReadRef(forward) : null,
    };

    /// <summary>
    /// A person dismisses a conflict (SYNC6c): one named by its machine and sequence, or, naming none, every one the quest carries. Each
    /// is a <see cref="QuestOperationKind.Dismissed"/> operation, carried by the next pass like any other. It moves no status.
    /// </summary>
    /// <returns>The quest as it now stands (null: no such quest) and how many conflicts went.</returns>
    internal async Task<QuestDismissal> DismissAsync(QuestTx tx, string id, string? machine, long? sequence, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest)
        {
            // No such quest, or a row the log never saw, which raced nobody and carries none.
            return new QuestDismissal(await tx.FindAsync(id).ConfigureAwait(false), 0);
        }

        // A sequence without its machine names none: sequences are per machine, and reading it as "none named" dismissed every one (REV3).
        var named = quest.Conflicts
            .Where(conflict => (machine is null && sequence is null)
                || (conflict.Machine == machine && (sequence is null || conflict.Sequence == sequence)))
            .ToList();
        var written = new List<QuestOperation>(history);
        foreach (var conflict in named)
        {
            written.Add(await tx.AppendAsync(new QuestOperation(
                id, QuestOperationKind.Dismissed, tx.Machine, 0, now,
                Dismisses: new QuestOperationRef(conflict.Machine, conflict.Sequence))).ConfigureAwait(false));
        }

        var standing = QuestLog.Replay(written)!;
        if (named.Count > 0) await tx.WriteCacheAsync(standing).ConfigureAwait(false);
        return new QuestDismissal(standing, named.Count);
    }

    /// <summary>
    /// The person brings this machine's lost take forward (SYNCMERGE1a, D178 point 3): in one write, an ordinary take of the step stamped
    /// with the source it grows from as it stands (FABRIC-CLOSE1c18), and the dismissal of <paramref name="conflict"/> marked with that take
    /// (<see cref="QuestOperation.Forward"/>), so the conflict goes only where the take stands. Refused while another machine or this one
    /// holds the step, once it is closed, while the person's reopen holds its source, and where the conflict is not this machine's lost
    /// take still on the quest. Pushing the take is the exchange's (D69); a rebase loses both where another machine's take, or a
    /// dismissal of the same conflict, reached the remote first.
    /// </summary>
    /// <param name="running">
    /// That a session here still runs on the quest, as the exchange read the session store (SYNCMERGE1c4): refused
    /// <see cref="QuestForwardRefusal.StillRunning"/> once every other rule would let the press stand.
    /// </param>
    internal async Task<QuestBringingForward> BringForwardAsync(
        QuestTx tx, string id, QuestOperationRef conflict, DateTimeOffset now, bool running)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } standing) return new QuestBringingForward(QuestForwardRefusal.NotFound, null);

        // Gone from the quest as it stands, it was dismissed already or went with an earlier publication of the quest (D95), which the
        // history tells apart.
        bool Named(QuestOperationRef? named) => named == conflict;
        if (conflict.Machine != tx.Machine
            || !history.Any(o => o is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken }
                                 && Named(new QuestOperationRef(o.Machine, o.Sequence))))
        {
            return new QuestBringingForward(QuestForwardRefusal.NotOurs, standing);
        }

        if (!standing.Conflicts.Any(c => Named(new QuestOperationRef(c.Machine, c.Sequence))))
        {
            var settled = history.Any(o => o.Kind == QuestOperationKind.Dismissed && Named(o.Dismisses));
            return new QuestBringingForward(settled ? QuestForwardRefusal.Settled : QuestForwardRefusal.NotOurs, standing);
        }

        var refusal = standing switch
        {
            { Status: QuestStatus.Done or QuestStatus.Declined } => QuestForwardRefusal.Closed,
            { Status: QuestStatus.Taken } => QuestLog.Claim(history, tx.Machine) is QuestClaim.Held or QuestClaim.Unconfirmed
                ? QuestForwardRefusal.TakenHere
                : QuestForwardRefusal.TakenElsewhere,
            { SourceReopened: not null } => QuestForwardRefusal.SourceReopened,
            _ => QuestForwardRefusal.None,
        };

        // The take as a move makes one: stamped with the step's source as this write finds it, judged by the replay's rule.
        var found = standing.GrewFrom;
        if (refusal == QuestForwardRefusal.None
            && !QuestLog.Applies(standing, new QuestOperation(id, QuestOperationKind.Taken, tx.Machine, 0, now, Submission: found)))
        {
            refusal = QuestForwardRefusal.SourceReopened;
        }

        if (refusal == QuestForwardRefusal.None && running) refusal = QuestForwardRefusal.StillRunning;
        if (refusal != QuestForwardRefusal.None) return new QuestBringingForward(refusal, standing);

        var take = await tx.AppendAsync(new QuestOperation(id, QuestOperationKind.Taken, tx.Machine, 0, now, Submission: found))
            .ConfigureAwait(false);
        var brought = new QuestOperationRef(tx.Machine, take.Sequence);
        var mark = await tx.AppendAsync(new QuestOperation(
                id, QuestOperationKind.Dismissed, tx.Machine, 0, now, Dismisses: conflict, Forward: brought))
            .ConfigureAwait(false);
        var taken = QuestLog.Replay([.. history, take, mark])!;
        await tx.WriteCacheAsync(taken).ConfigureAwait(false);
        await KeepPressAsync(tx, id, new QuestForwardPress(conflict, brought, now)).ConfigureAwait(false);
        return new QuestBringingForward(QuestForwardRefusal.None, taken, brought);
    }

    /// <summary>
    /// Keeps this machine's own bring-forward (SYNCMERGE1b0, <see cref="QuestForwardPress"/>) in the write that made it, so a take the
    /// rebase later loses with its mark still says it was one.
    /// </summary>
    private static async Task KeepPressAsync(QuestTx tx, string id, QuestForwardPress press)
    {
        await using var command = tx.Command();
        command.CommandText = """
            INSERT INTO quest_forwards (quest, conflict_machine, conflict_sequence, take_machine, take_sequence, at)
            VALUES ($quest, $conflictMachine, $conflictSequence, $takeMachine, $takeSequence, $at)
            """;
        command.Parameters.AddWithValue("$quest", id);
        command.Parameters.AddWithValue("$conflictMachine", press.Conflict.Machine);
        command.Parameters.AddWithValue("$conflictSequence", press.Conflict.Sequence);
        command.Parameters.AddWithValue("$takeMachine", press.Take.Machine);
        command.Parameters.AddWithValue("$takeSequence", press.Take.Sequence);
        command.Parameters.AddWithValue("$at", Moments.Write(press.At));
        await command.ExecuteNonQueryAsync(tx.Ct).ConfigureAwait(false);
    }
}
