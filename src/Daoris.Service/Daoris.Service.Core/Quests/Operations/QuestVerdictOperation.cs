using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's verdict on a set-up step's review, or their skip of it (REVIEWENV1b, D154 point 8). A <c>reviewed</c> or a skip lets go
/// what the step's review held and publishes the chain's next step; a <c>not-yet</c> keeps it held. It moves no status.
/// </summary>
[QuestOperation("verdict", QuestOperationPhase.Log)]
internal sealed class QuestVerdictOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Verdict;

    /// <summary>The verdict given; its when and machine are the operation's own columns.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Verdict is { } review)
        {
            writer.WritePropertyName("verdict");
            Reviews.Write(writer, review);
        }

        writer.WriteEndObject();
    }

    /// <summary>Written only after the exchange judged it, and read through the same judge.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Verdict = payload.TryGetProperty("verdict", out var given) ? Reviews.JudgedVerdict(given) : null,
    };

    /// <summary>
    /// A verdict that lost while the step is still held takes the step it published with it, as a lost yes does; one that lost to another
    /// machine's leaves the step the winner published.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses) =>
        standing is { Held: true }
            ? tx.ForgetFollowUpsAsync(operation.Quest, takeLost, "review verdict")
            : Task.FromResult<IReadOnlyList<QuestOperation>>([]);

    /// <summary>
    /// The person's verdict, judged by the replay's rule inside the write. One that lets the step's review go publishes the chain's next
    /// step in the same write, as the yes publishes one.
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call kept the verdict, and the step it published; a null quest means no such id.
    /// </returns>
    internal async Task<QuestMove> VerdictAsync(QuestTx tx, string id, QuestReviewVerdict verdict, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        var judged = new QuestOperation(id, QuestOperationKind.Verdict, tx.Machine, 0, now, Verdict: verdict with { At = null, Machine = null });
        if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);

        var given = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(given).ConfigureAwait(false);
        var released = quest.Held && !given.Held;
        return new QuestMove(given, Moved: true, released ? await tx.PublishNextAsync(given, now).ConfigureAwait(false) : null);
    }
}
