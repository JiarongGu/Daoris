using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A plan revision's rewording or rescoping of the part a quest publishes: the quest's title, body, weight and claimed paths become the
/// revision's, while it is open or taken. The door is <c>team_plan</c>, so no host has one of its own; the latest stands, and it moves no status.
/// </summary>
[QuestOperation("revised", QuestOperationPhase.Log)]
internal sealed class QuestReviseOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Revised;

    /// <summary>The revision made, in its one shape.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Revision is { } revision)
        {
            writer.WritePropertyName("revision");
            revision.Write(writer);
        }

        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Revision = payload.TryGetProperty("revision", out var revision) ? QuestRevision.Judged(revision) : null,
    };

    /// <summary>
    /// The revision, judged by the replay's rule inside the write, so one that meets a closed quest is no move, and one that says what
    /// the quest already says is none either.
    /// </summary>
    /// <returns>The quest as it now stands and whether this call kept the revision; a null quest means no such id.</returns>
    internal async Task<QuestMove> ReviseAsync(QuestTx tx, string id, QuestRevision revision, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        var judged = new QuestOperation(id, QuestOperationKind.Revised, tx.Machine, 0, now, Revision: revision);
        if (!QuestLog.Applies(quest, judged) || revision.Says(quest)) return new QuestMove(quest, Moved: false);

        var kept = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(kept).ConfigureAwait(false);
        return new QuestMove(kept, Moved: true);
    }
}
