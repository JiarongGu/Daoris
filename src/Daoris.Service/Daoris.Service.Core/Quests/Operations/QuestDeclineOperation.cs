using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A quest turned down, with the reason as its note. One made while the quest is open is kept with that mark, so a rebase or a
/// remote judges it by the same rule. Its move is <see cref="QuestTakeOperation.MoveAsync"/>.
/// </summary>
[QuestOperation("declined", QuestOperationPhase.Log)]
internal sealed class QuestDeclineOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Declined;

    /// <summary>Its reason beside its parts, whether it applies only while open, and the session that wrote its handback, each only when set.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.WhileOpen) writer.WriteBoolean("whileOpen", true);
        if (operation.Session is { } session) writer.WriteString("session", session);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        WhileOpen = payload.TryGetProperty("whileOpen", out var open) && open.ValueKind == JsonValueKind.True,
        Session = QuestRecordCodec.ReadSession(payload),
    };
}
