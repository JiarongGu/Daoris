using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A closure graph's work submitted (FABRIC-CLOSE1a, §E5): a done's note and answers, and the chain's next step it publishes when
/// nothing holds it, in the same transaction. A submission seals that step, and a lost one takes it with it.
/// </summary>
[QuestOperation("submitted", QuestOperationPhase.Log)]
internal sealed class QuestSubmitOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Submitted;

    /// <summary>Its note beside its parts, its answers and the session that said its <c>ready</c>, each only when set.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Answers is { Count: > 0 } answers)
        {
            writer.WritePropertyName("answers");
            QuestRecordCodec.WriteAnswers(writer, answers);
        }

        if (operation.Session is { } session) writer.WriteString("session", session);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with { Session = QuestRecordCodec.ReadSession(payload) };

    /// <summary>
    /// A submission that lost takes the step it published with it, its publication pending on the same take (FABRIC-CLOSE1a, §E5), and
    /// the take's conflict names it. One that lost to another machine's done leaves the step that done published, which is the same one.
    /// The step goes even once this machine took it, and its take reads lost, so its session is stopped (FABRIC-CLOSE1e); where the take
    /// this submission was made on lost to the person's reopen, the step's take says that (FABRIC-CLOSE1c22), read here because the step
    /// keeps no history to.
    /// </summary>
    public async Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses)
    {
        if (standing is { Status: QuestStatus.Done }) return [];
        var told = await tx.ForgetFollowUpsAsync(operation.Quest, takeLost, "submission", reopened: takeLost && losses.LostToTheReopen)
            .ConfigureAwait(false);
        losses.Submission |= takeLost;
        return told;
    }

    /// <summary>
    /// Submit a closure graph's work: a <see cref="QuestOperationKind.Submitted"/> carrying a done's note and answers, and the chain's
    /// next step it publishes when nothing holds it, in the same transaction.
    /// </summary>
    /// <returns>
    /// As <see cref="QuestBindOperation.BindAsync"/>, and refused with <see cref="QuestMove.SealedBy"/> where the current submission
    /// already published its successor, which stands: the step grew from that submission, and only the person reopens it.
    /// </returns>
    internal Task<QuestMove> SubmitAsync(
        QuestTx tx, string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, string? session) =>
        tx.Of<QuestBindOperation>().TakersMoveAsync(
            tx,
            id,
            new QuestOperation(
                id, QuestOperationKind.Submitted, tx.Machine, 0, now, note, Answers: answers.Count > 0 ? answers : null,
                Session: session is { Length: > 0 } ? session : null),
            now);

    /// <summary>
    /// The step that seals <paramref name="quest"/>'s current submission (FABRIC-CLOSE1a, FABRIC-CLOSE1c; §E5): its chain's next step,
    /// standing on that submission, withdrawn by no reopen. Null where none does, so a later <c>ready</c> replaces it, and the close
    /// door reads it unsealed. A step from before names no submission, and seals whichever is current, as it always did.
    /// </summary>
    internal async Task<Quest?> SealingAsync(QuestTx tx, Quest quest) =>
        quest.Submission is { } current
        && await tx.SuccessorAsync(quest).ConfigureAwait(false) is { } step
        && Grows(step, new QuestOperationRef(current.Machine, current.Sequence))
            ? step
            : null;

    /// <summary>Whether <paramref name="step"/> stands on <paramref name="submission"/> (FABRIC-CLOSE1c): grown from it, no reopen since.</summary>
    internal static bool Grows(Quest step, QuestOperationRef submission) =>
        step.SourceReopened is null && (step.GrewFrom is not { } from || from == submission);

    /// <summary>
    /// A lost take's note, then the submission that went with it (FABRIC-CLOSE1a, §E5), said on the conflict every machine
    /// shows: the winner's work is what the quest's graph now closes. A take lost to the person's reopen (FABRIC-CLOSE1c21,
    /// <see cref="QuestLog.LostToTheReopen"/>) has no winner, so it says the reopen, as the take's refusals do.
    /// </summary>
    internal static string SubmissionWentWith(string? note, bool reopened = false)
    {
        var said = reopened
            ? "This take's submission went with it, and so did any step it published; the person's reopen of the work this step grew from "
              + "reached the remote first, so the step is open for the work as it stands now."
            : "This take's submission went with it, and so did any step it published; the quest's close is the winner's.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }
}
