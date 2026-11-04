using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Daoris's reading of a done's evidence (EVID1a, D144 point 3): the commit read, and what each item it waits on was found to be. It
/// applies only to a done waiting on exactly what it reads; found, it lets go what the done held, and it moves no status.
/// </summary>
[QuestOperation("evidenced", QuestOperationPhase.Log)]
internal sealed class QuestEvidenceOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Evidenced;

    /// <summary>What was read; its when and machine are the operation's own columns.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Evidence is { } evidence)
        {
            writer.WritePropertyName("evidence");
            evidence.Write(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>Written only after the exchange judged it, and read through the same judge.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Evidence = payload.TryGetProperty("evidence", out var found) ? QuestEvidenceVerdict.Judged(found) : null,
    };

    /// <summary>
    /// Daoris read a done's evidence: judged by the replay's rule inside the write. Found, and nothing else holding the done, the chain's
    /// next step is published in the same write, as the yes publishes one; missing, the done stays held, now for that. A commit read
    /// alone keeps the commit of a submission that waits on no evidence, once, and publishes nothing
    /// (FABRIC-CLOSE1c6, <see cref="QuestEvidenceVerdict.KeepsCommitOf"/>).
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call kept the verdict (false for a quest not waiting on what it reads), and the step
    /// it published; a null quest means no such id.
    /// </returns>
    internal async Task<QuestMove> EvidenceAsync(QuestTx tx, string id, QuestEvidenceVerdict verdict, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        var read = verdict with { At = null, Machine = null };
        var judged = new QuestOperation(id, QuestOperationKind.Evidenced, tx.Machine, 0, now, Evidence: read);
        if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);

        var evidenced = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(evidenced).ConfigureAwait(false);
        // A commit read alone kept the commit of a submission nothing held for it, so it lets nothing go.
        return new QuestMove(
            evidenced, Moved: true,
            evidenced.Held || read.Items.Count == 0 ? null : await tx.PublishNextAsync(evidenced, now).ConfigureAwait(false));
    }
}
