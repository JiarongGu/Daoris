using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's kind and workflow for a quest before it starts (WORKFLOW1e2, the workflow design §4.1 row 1, §4.3). It applies only
/// while the quest is open, since a run is bound at its first start; the latest stands, and it moves no status.
/// </summary>
[QuestOperation("chosen", QuestOperationPhase.Log)]
internal sealed class QuestChoiceOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Chosen;

    /// <summary>The choice made; its when is the operation's own.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestStore.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Choice is { } choice)
        {
            writer.WritePropertyName("choice");
            WorkflowChoices.Write(writer, choice);
        }

        writer.WriteEndObject();
    }

    /// <summary>Written only after the exchange judged it, and read through the same judge.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Choice = payload.TryGetProperty("choice", out var choice) ? WorkflowChoices.Judged(choice) : null,
    };

    /// <summary>
    /// The person's choice, judged by the replay's rule inside the write, so one that meets a quest already taken is no move. It
    /// releases nothing.
    /// </summary>
    /// <returns>The quest as it now stands and whether this call kept the choice; a null quest means no such id.</returns>
    internal async Task<QuestMove> ChooseAsync(QuestTx tx, string id, QuestWorkflowChoice choice, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        var judged = new QuestOperation(id, QuestOperationKind.Chosen, tx.Machine, 0, now, Choice: choice with { At = null });
        if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);

        var kept = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(kept).ConfigureAwait(false);
        return new QuestMove(kept, Moved: true);
    }
}
