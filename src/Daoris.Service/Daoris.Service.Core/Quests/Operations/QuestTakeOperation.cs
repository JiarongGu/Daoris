using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A repository's agent accepting a quest. It also holds the one move verb the transition table gives a take, a done and a decline,
/// since the three are judged, written and followed up by the same rule; a take is the one that binds the graph named for it.
/// </summary>
[QuestOperation("taken", QuestOperationPhase.Log)]
internal sealed class QuestTakeOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Taken;

    /// <summary>Its note beside its parts, and the source it found its step on (FABRIC-CLOSE1c18), where it found one.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Submission is { } submission) QuestRecordCodec.WriteRef(writer, "submission", submission);
        writer.WriteEndObject();
    }

    /// <summary>The source is absent from every take before it and from one of a quest with none: a take judged as before.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Submission = payload.TryGetProperty("submission", out var submission) ? QuestRecordCodec.ReadRef(submission) : null,
    };

    /// <summary>
    /// Move a quest to a new status, atomically, honouring the transition table. Declining without a reason is refused by the caller;
    /// an illegal move is refused HERE, because judging the replayed history inside the write is what makes "the quest state machine
    /// is the only lock" true when two hosts' callers race over one file (D47 §5).
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call moved it; a null quest means no such id. A refused move returns the row
    /// unchanged, and says <see cref="QuestMove.ClaimLost"/> where the state allowed it but this machine's take on the quest lost
    /// (WAITCLAIM2).
    /// </returns>
    /// <param name="asked">The clock's moment the caller asked at, before it queued for the write, so a take measures that wait (FABRIC-CLOSE1w).</param>
    /// <param name="answers">How a done answers the quest's requirements (DRIFT1d); a departure among them holds the chain's next step.</param>
    /// <param name="whileOpen">A decline that applies only while the quest is open (PAUSE1c); ignored on any other move.</param>
    /// <param name="noteParts">The note's parts beside it (QUESTCLOSE1b1); only with a note.</param>
    /// <param name="byAgent">An agent's done, refused on a quest a closure graph binds or a step its source's reopen holds (FABRIC-CLOSE1a).</param>
    /// <param name="taker">Who takes (FABRIC-CLOSE1o), which a take judges the bind named for it by; null names no session.</param>
    /// <param name="session">The session closing it through its connector (HANDBACK1), kept on a done or a decline.</param>
    internal async Task<QuestMove> MoveAsync(
        QuestTx tx, string id, QuestStatus status, string? note, DateTimeOffset now, long asked, IReadOnlyList<QuestAnswer>? answers,
        bool whileOpen, string? noteParts, bool byAgent, QuestTaker? taker, string? session)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);

        // Every verb commits here, whichever machines share the quest (D68 §1) — and nothing moves TO open.
        if (held is null || QuestTransitions.KindFor(status) is not { } kind) return new QuestMove(held, Moved: false);

        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        // A row the log never saw is given its history now, as the open would have, and keeps it even if the move is refused.
        if (history.Count == 0) history = await tx.GiveHistoryAsync(held).ConfigureAwait(false);

        // Judged by the replay's own rule, so the store refuses here exactly what a rebase or a remote would not apply.
        var open = whileOpen && status == QuestStatus.Declined;
        var standing = QuestLog.Replay(history);
        // FABRIC-CLOSE1c18: a take carries the step's source as this write finds it, so wherever it is judged later (a rebase, a
        // remote), a reopen and its rebind that reached there first are seen to have moved the step from under it.
        var found = status == QuestStatus.Taken ? standing?.GrewFrom : null;
        var judged = new QuestOperation(id, kind, tx.Machine, 0, now, note, WhileOpen: open, Submission: found);
        if (!QuestLog.Applies(standing, judged))
        {
            // FABRIC-CLOSE1c: a step its source's reopen withdrew is taken by nobody, said apart so the answer can say why.
            return new QuestMove(held, Moved: false)
            {
                SourceReopened = status == QuestStatus.Taken && standing is { Status: QuestStatus.Open, SourceReopened: not null },
            };
        }

        // A close on the winner's take, made here after the pass that found this machine's take lost (WAITCLAIM2); on an open step,
        // whether the take lost to the person's reopen or with the work it was made on, for the refusal's words (FABRIC-CLOSE1c24).
        if (QuestLog.OnALostTake(standing, history, judged))
        {
            return new QuestMove(held, Moved: false) { ClaimLost = true, LostToTheReopen = QuestLog.LostToTheReopen(history, tx.Machine) };
        }

        // FABRIC-CLOSE1c: a step its source's reopen held goes no further by its agent's done; the person chooses whether it goes
        // on. Their own done and any decline stay theirs.
        if (byAgent && status == QuestStatus.Done && standing is { SourceReopened: not null })
        {
            return new QuestMove(held, Moved: false) { SourceReopened = true };
        }

        // An agent's done where the graph closes the quest (FABRIC-CLOSE1a): its result is a submission, so no done slips past.
        if (byAgent && status == QuestStatus.Done && standing is { Closure: not null })
        {
            return new QuestMove(held, Moved: false) { ClosedByGraph = true };
        }

        var operation = await tx.AppendAsync(
            new QuestOperation(
                id, kind, tx.Machine, 0, now, note, Answers: status == QuestStatus.Done ? answers : null, WhileOpen: open,
                NoteParts: noteParts, Submission: found, Session: session)).ConfigureAwait(false);
        var moved = QuestLog.Replay([.. history, operation])!;

        // FABRIC-CLOSE1l (CLOSE1f): a take whose graph the driver named binds in this same write, judged by the replay's rule as
        // every bind is, so no agent's done can land between the take and its bind. FABRIC-CLOSE1o: the row is consumed here
        // whatever it makes of it, and binds only for its owner, judged at the take's own moment, never by a look before it.
        // FABRIC-CLOSE1w: that moment is the one inside this write, the caller's advanced by the wait for it, so a take queued past
        // the expiry binds nothing. The operations keep the caller's moment, as every verb's do.
        var takeBind = QuestTakeBind.None;
        if (status == QuestStatus.Taken && await tx.Of<QuestBindOperation>().TakeBindAsync(tx, id).ConfigureAwait(false) is { } named)
        {
            takeBind = named.Judge(taker, history, moved.Workspace, now + tx.Time.GetElapsedTime(asked));
            var bind = new QuestOperation(id, QuestOperationKind.Bound, tx.Machine, 0, now, named.Graph);
            if (takeBind == QuestTakeBind.Bound && QuestLog.Applies(moved, bind))
            {
                var bound = await tx.AppendAsync(bind).ConfigureAwait(false);
                moved = QuestLog.Replay([.. history, operation, bound])!;
            }
            else if (takeBind == QuestTakeBind.Bound)
            {
                takeBind = QuestTakeBind.None;
            }
        }

        await tx.WriteCacheAsync(moved).ConfigureAwait(false);

        // A close that finishes a chain's step publishes the next one IN THE SAME TRANSACTION (D65 §4): there is no moment at which the
        // work is done and the chain lost, and a close another host wins publishes nothing here. A step already published is the one
        // that stands — the id is the ask. A departure holds it for the person's yes (DRIFT1d).
        Quest? followUp = null;
        if (status == QuestStatus.Done && !moved.Held) followUp = await tx.PublishNextAsync(moved, now).ConfigureAwait(false);

        return new QuestMove(moved, Moved: true, followUp) { TakeBind = takeBind };
    }
}
