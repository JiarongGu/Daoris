using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A move another machine's reached the remote before (D68 §5), kept rather than dropped and shown on the quest until a person acts.
/// A rebase writes it in place of the move that lost (<see cref="QuestLog.Lost"/>), under that move's machine and sequence.
/// </summary>
[QuestOperation("conflict", QuestOperationPhase.Log)]
internal sealed class QuestConflictOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Conflict;

    /// <summary>
    /// A take that lost to a delete arrives as a conflict on a quest that is gone (D95), and is kept like any conflict, so its machine's
    /// claim reads lost on every side.
    /// </summary>
    public bool KeptOnADeletedQuest => true;

    /// <summary>The move it attempted, and the note that move carried, beside its parts.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Attempted is { } attempted) writer.WriteString("attempted", attempted.ToString());
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestStore.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        // Only when set (FABRIC-CLOSE1c22), so every other conflict reads as it did.
        if (operation.LostToTheReopen) writer.WriteBoolean("lostToTheReopen", true);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Attempted = payload.TryGetProperty("attempted", out var tried) ? Enum.Parse<QuestStatus>(tried.GetString()!, ignoreCase: true) : null,
        // Absent from every conflict but a step's take that went with a take lost to the person's reopen.
        LostToTheReopen = payload.TryGetProperty("lostToTheReopen", out var reopened) && reopened.ValueKind == JsonValueKind.True,
    };
}
