using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A taken quest bound to the closure graph that closes it (FABRIC-CLOSE1a, §E5): its note names the graph's digest. It moves no status.
/// The take's own bind, named for it before it and consumed by it (FABRIC-CLOSE1o), is kept in tables beside the log and lives here too.
/// </summary>
[QuestOperation("bound", QuestOperationPhase.Log)]
internal sealed class QuestBindOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Bound;

    /// <summary>The graph's digest, as its note.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>
    /// Bind a taken quest to the graph that closes it: a <see cref="QuestOperationKind.Bound"/> operation naming the graph's digest,
    /// judged against the replayed history inside the write.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call bound it; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take lost, and unmoved where this machine holds no take on it.
    /// </returns>
    internal Task<QuestMove> BindAsync(QuestTx tx, string id, string graph, DateTimeOffset now) =>
        TakersMoveAsync(tx, id, new QuestOperation(id, QuestOperationKind.Bound, tx.Machine, 0, now, graph), now);

    /// <summary>
    /// Name the closure graph an open quest binds at its take (FABRIC-CLOSE1l, folding CLOSE1f; §E5's cutover): kept in this store
    /// alone, so the take on this machine appends the bind in its own write, and no agent's `done` can land between the take and the
    /// bind. The same graph again is the same; another replaces it, since nothing is bound yet.
    /// FABRIC-CLOSE1o: owned by <paramref name="session"/>, the session it is named for, and by the quest's publication as it stands,
    /// and lapsing <see cref="QuestStore.TakeBindLife"/> after <paramref name="now"/>; naming it again names it anew, for the session
    /// named then.
    /// </summary>
    /// <returns>The quest as it stands and whether the graph is named now; unmoved where it is not open, a null quest for no such id.</returns>
    internal async Task<QuestMove> BindAtTakeAsync(QuestTx tx, string id, string graph, string session, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestMove(null, Moved: false);
        // CLOSETAKE1: a store a newer build stamped takes no bind from this one (the exchange says why).
        if (tx.NewerVersion is not null) return new QuestMove(held, Moved: false);
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        // A row the log never saw is given its history now, as a move gives it: its publication is what the bind is owned by.
        if (history.Count == 0) history = await tx.GiveHistoryAsync(held).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        if (standing is not { Status: QuestStatus.Open } || PublicationOf(history) is not { } publication)
        {
            return new QuestMove(held, Moved: false);
        }

        // FABRIC-CLOSE1w: in the table no build before this one knows, so none can replace the graph and keep the owner.
        await using var command = tx.Command();
        command.CommandText = """
            INSERT INTO quest_owned_take_binds (id, graph, at, session, published_machine, published_sequence, expires)
            VALUES ($id, $graph, $at, $session, $machine, $sequence, $expires)
            ON CONFLICT(id) DO UPDATE SET graph = excluded.graph, at = excluded.at, session = excluded.session,
              published_machine = excluded.published_machine, published_sequence = excluded.published_sequence,
              expires = excluded.expires
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$graph", graph);
        command.Parameters.AddWithValue("$at", Moments.Write(now));
        command.Parameters.AddWithValue("$session", session);
        command.Parameters.AddWithValue("$machine", publication.Machine);
        command.Parameters.AddWithValue("$sequence", publication.Sequence);
        command.Parameters.AddWithValue("$expires", Moments.Write(now + QuestStore.TakeBindLife));
        await command.ExecuteNonQueryAsync(tx.Ct).ConfigureAwait(false);
        return new QuestMove(held, Moved: true);
    }

    /// <summary>
    /// A closure graph's bind or submission (FABRIC-CLOSE1a), made by the machine holding the take, judged by the replay's rule
    /// inside the write as every move is. A submission nothing holds publishes the chain's next step in the same transaction,
    /// as an unheld done does, and is sealed by it (§E5).
    /// </summary>
    internal async Task<QuestMove> TakersMoveAsync(QuestTx tx, string id, QuestOperation judged, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestMove(null, Moved: false);

        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (history.Count == 0) history = await tx.GiveHistoryAsync(held).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        if (!QuestLog.Applies(standing, judged)) return new QuestMove(held, Moved: false);
        if (QuestLog.OnALostTake(standing, history, judged)) return new QuestMove(held, Moved: false) { ClaimLost = true };

        // Only the machine holding the take binds or submits (§E5): another machine's take is its own to finish.
        if (QuestLog.Claim(history, tx.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed)) return new QuestMove(held, Moved: false);

        // Sealed (§E5): a submission whose successor stands takes no later one, so the step never stands on work replaced
        // under it. Only the take's machine submits, so this machine's store is where the seal is judged.
        if (judged.Kind == QuestOperationKind.Submitted && await tx.SealingAsync(standing!).ConfigureAwait(false) is { } successor)
        {
            return new QuestMove(held, Moved: false) { SealedBy = successor.Id };
        }

        // FABRIC-CLOSE1c: a step its source's reopen holds goes no further; the person chooses whether it goes on.
        if (judged.Kind == QuestOperationKind.Submitted && standing!.SourceReopened is not null)
        {
            return new QuestMove(held, Moved: false) { SourceReopened = true };
        }

        var operation = await tx.AppendAsync(judged).ConfigureAwait(false);
        var moved = QuestLog.Step(standing, operation)!;
        await tx.WriteCacheAsync(moved).ConfigureAwait(false);

        var followUp = judged.Kind == QuestOperationKind.Submitted && !moved.Held
            ? await tx.PublishNextAsync(moved, now).ConfigureAwait(false)
            : null;
        return new QuestMove(moved, Moved: true, followUp);
    }

    /// <summary>
    /// The operation that published the quest as it now stands (FABRIC-CLOSE1o): the latest Published in its history, since a quest
    /// deleted while open and asked again under the same id is published anew. Null where the history holds none.
    /// </summary>
    internal static QuestOperationRef? PublicationOf(IReadOnlyList<QuestOperation> history) =>
        history.LastOrDefault(operation => operation.Kind == QuestOperationKind.Published) is { } published
            ? new QuestOperationRef(published.Machine, published.Sequence)
            : null;

    /// <summary>
    /// The bind named for <paramref name="id"/>'s take, taken from the store in the take's transaction, owned or not; null where none
    /// was. Consumed by the take whatever the take makes of it (FABRIC-CLOSE1o), so no row outlives the take it was named for.
    /// FABRIC-CLOSE1w: the owned bind is this build's, in <c>quest_owned_take_binds</c>; a row in <c>quest_take_binds</c> is an older
    /// build's, whose owner cannot be shown, so it is consumed too and read as owning nothing.
    /// </summary>
    internal async Task<PendingTakeBind?> TakeBindAsync(QuestTx tx, string id)
    {
        string? older;
        await using (var discard = tx.Command())
        {
            discard.CommandText = "DELETE FROM quest_take_binds WHERE id = $id RETURNING graph";
            discard.Parameters.AddWithValue("$id", id);
            older = await discard.ExecuteScalarAsync(tx.Ct).ConfigureAwait(false) as string;
        }

        await using var command = tx.Command();
        command.CommandText = """
            DELETE FROM quest_owned_take_binds WHERE id = $id
            RETURNING graph, session, published_machine, published_sequence, expires
            """;
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(tx.Ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(tx.Ct).ConfigureAwait(false))
        {
            return older is null ? null : new PendingTakeBind(older, Session: null, Publication: null, Expires: null);
        }

        return new PendingTakeBind(
            reader.GetString(0),
            reader.GetString(1),
            new QuestOperationRef(reader.GetString(2), reader.GetInt64(3)),
            Moments.Read(reader.GetString(4)));
    }

    /// <summary>
    /// A bind named for a take, as the take reads it (FABRIC-CLOSE1o): its graph, and its owner where one was kept: the session it was
    /// named for, the publication it was named on and when it lapses.
    /// </summary>
    internal sealed record PendingTakeBind(string Graph, string? Session, QuestOperationRef? Publication, DateTimeOffset? Expires)
    {
        /// <summary>
        /// What the take makes of it, inside the take's write: bound only for the session it was named for, on the publication it was
        /// named on, before it lapses at <paramref name="now"/>, the moment inside that write (FABRIC-CLOSE1w), and where the circle
        /// syncs through no remote.
        /// </summary>
        public QuestTakeBind Judge(QuestTaker? taker, IReadOnlyList<QuestOperation> history, string workspace, DateTimeOffset now)
        {
            if (Session is null || Publication is null || Expires is null) return QuestTakeBind.Unowned;
            if (!string.Equals(taker?.Session, Session, StringComparison.Ordinal)) return QuestTakeBind.OtherSession;
            if (PublicationOf(history) != Publication) return QuestTakeBind.Replaced;
            if (now >= Expires) return QuestTakeBind.Expired;
            if (taker?.Remote?.Invoke(workspace) == true) return QuestTakeBind.Remote;
            return QuestTakeBind.Bound;
        }
    }
}
