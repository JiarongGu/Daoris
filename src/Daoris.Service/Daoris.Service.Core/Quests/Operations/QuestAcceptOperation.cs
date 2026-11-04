using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's yes to a done as it stands (DRIFT1d, D133 §4; EVID1a, D144 §6): its departure, or its evidence unread or missing. It
/// lets go what the done held and moves no status; a yes that loses a rebase takes the step it published with it.
/// </summary>
[QuestOperation("accepted", QuestOperationPhase.Log)]
internal sealed class QuestAcceptOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Accepted;

    /// <summary>A yes carries nothing but a note a row from another machine may say.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestStore.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>
    /// A yes that lost takes the step it published with it, as a lost close's does (DRIFT1d), while the done stands unaccepted; one that
    /// lost to another machine's yes leaves the step the winner published, which is the same one.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> LostAsync(QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost) =>
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
    internal async Task<QuestMove> AcceptAsync(QuestTx tx, string id, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        // A departure or its evidence takes a yes, and a review's hold does not (REVIEWENV1b).
        var judged = new QuestOperation(id, QuestOperationKind.Accepted, tx.Machine, 0, now);
        if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);

        var accepted = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(accepted).ConfigureAwait(false);
        // A set-up step accepted for its departure still waits for its review, and what follows it waits with it.
        return new QuestMove(accepted, Moved: true, accepted.Held ? null : await tx.PublishNextAsync(accepted, now).ConfigureAwait(false));
    }
}
