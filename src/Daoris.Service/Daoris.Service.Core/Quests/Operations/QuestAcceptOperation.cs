using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's yes to a done as it stands: its departure, or its evidence unread or missing. It
/// lets go what the done held and moves no status; a yes that loses a rebase takes the step it published with it.
/// </summary>
[QuestOperation("accepted", QuestOperationPhase.Log)]
internal sealed class QuestAcceptOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Accepted;

    /// <summary>A yes carries a note a row from another machine may say, and the appointed role that said it; an older build reads neither.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.By is not null) writer.WriteString("by", operation.By);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("by", out var by) && by.ValueKind == JsonValueKind.String
            ? read with { By = by.GetString() }
            : read;

    /// <summary>
    /// A yes that lost takes the step it published with it, as a lost close's does, while the done stands unaccepted; one that
    /// lost to another machine's yes leaves the step the winner published, which is the same one.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses) =>
        standing is not { Accepted: not null }
            ? tx.ForgetFollowUpsAsync(operation.Quest, takeLost, "yes")
            : Task.FromResult<IReadOnlyList<QuestOperation>>([]);

    /// <summary>
    /// The person accepts a done as it stands: an <see cref="QuestOperationKind.Accepted"/> judged by the replay's rule inside the write,
    /// and the chain's next step it held published in the same write, as a close publishes one.
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call accepted it (false for a quest nothing holds), and the step it published; a null
    /// quest means no such id.
    /// </returns>
    /// <param name="by">The appointed role whose yes it is (<see cref="QuestOperation.By"/>); null for the person's own.</param>
    internal async Task<QuestMove> AcceptAsync(QuestTx tx, string id, DateTimeOffset now, string? by = null)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        // A departure or its evidence takes a yes, and a review's hold does not. A role's yes takes a departure alone: the hold reads
        // departed first while another requirement's evidence is awaited, and the one yes would lift that evidence with it.
        var judged = new QuestOperation(id, QuestOperationKind.Accepted, tx.Machine, 0, now, By: by);
        if (!QuestLog.Applies(quest, judged) || (by is not null && !RolesToAccept(quest))) return new QuestMove(quest, Moved: false);

        var accepted = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(accepted).ConfigureAwait(false);
        // A set-up step accepted for its departure still waits for its review, and what follows it waits with it.
        return new QuestMove(accepted, Moved: true, accepted.Held ? null : await tx.PublishNextAsync(accepted, now).ConfigureAwait(false));
    }

    /// <summary>Whether an appointed role's or the risk policy's yes may accept <paramref name="quest"/>: a done held by its departure alone, no evidence awaited beside it.</summary>
    internal static bool RolesToAccept(Quest quest) => quest.Hold == QuestHold.Departed && !quest.AwaitsEvidence;
}
