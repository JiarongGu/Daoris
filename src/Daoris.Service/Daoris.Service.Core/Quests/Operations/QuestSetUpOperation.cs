using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A set-up step's word on where it showed the work for the person's review (REVIEWENV1b, D154 point 9). It applies only to a set-up
/// step, a second post of the same set-up is no move, and a newer one holds the step for the person again; it moves no status.
/// </summary>
[QuestOperation("setup", QuestOperationPhase.Log)]
internal sealed class QuestSetUpOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.SetUp;

    /// <summary>The set-up said; its when and machine are the operation's own columns.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.SetUp is { } setUp)
        {
            writer.WritePropertyName("setUp");
            Reviews.Write(writer, setUp);
        }

        writer.WriteEndObject();
    }

    /// <summary>Written only after the exchange judged it, and read through the same judge.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        SetUp = payload.TryGetProperty("setUp", out var shown) ? Reviews.JudgedSetUp(shown) : null,
    };

    /// <summary>
    /// A set-up step's set-up: judged by the replay's rule inside the write, and refused where this machine's take on the step lost
    /// (<see cref="QuestLog.OnALostTake"/>). It releases nothing.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call kept the set-up; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take on the quest lost.
    /// </returns>
    internal async Task<QuestMove> SetUpAsync(QuestTx tx, string id, QuestSetUp setUp, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        // Its identity is made here, where its local address is still known, and crosses with it (REVIEWENV1b3).
        var said = setUp with { At = null, Machine = null, Sequence = null };
        var shown = said with { Id = said.Id ?? Reviews.IdentityOf(said) };
        var judged = new QuestOperation(id, QuestOperationKind.SetUp, tx.Machine, 0, now, SetUp: shown);
        if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);
        if (QuestLog.OnALostTake(quest, history, judged))
        {
            return new QuestMove(quest, Moved: false) { ClaimLost = true, LostToTheReopen = QuestLog.LostToTheReopen(history, tx.Machine) };
        }

        var kept = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(kept).ConfigureAwait(false);
        return new QuestMove(kept, Moved: true);
    }
}
