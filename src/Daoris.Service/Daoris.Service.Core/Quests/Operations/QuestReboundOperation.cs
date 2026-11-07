using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A step its source's reopen withdrew, moved onto the submission that publishes it now, keeping its id and its history.
/// A publication's rebind is judged while the step is open; the person's go-on is not, since their word is what lets
/// a started step go.
/// </summary>
[QuestOperation("rebound", QuestOperationPhase.Log)]
internal sealed class QuestReboundOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Rebound;

    /// <summary>Its note beside its parts, whether it applies only while the step is open, and the submission it names.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        // Only when set, so the person's go-on and every rebind from before read as they did.
        if (operation.WhileOpen) writer.WriteBoolean("whileOpen", true);
        if (operation.Submission is { } submission) QuestRecordCodec.WriteRef(writer, "submission", submission);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        WhileOpen = payload.TryGetProperty("whileOpen", out var open) && open.ValueKind == JsonValueKind.True,
        Submission = payload.TryGetProperty("submission", out var submission) ? QuestRecordCodec.ReadRef(submission) : null,
    };

    /// <summary>
    /// A rebind of <paramref name="id"/> naming <paramref name="from"/>, judged by the replay's rule in the write's transaction; the step
    /// as it then stands, or null where the rule refused it.
    /// </summary>
    internal async Task<Quest?> ReboundInAsync(QuestTx tx, string id, QuestOperationRef from, bool whileOpen, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        var judged = new QuestOperation(id, QuestOperationKind.Rebound, tx.Machine, 0, now, Submission: from, WhileOpen: whileOpen);
        if (!QuestLog.Applies(standing, judged)) return null;
        var rebound = QuestLog.Step(standing, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(rebound).ConfigureAwait(false);
        return rebound;
    }
}
