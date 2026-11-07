using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Its taker waits on a question it asked another repository: its note names the quest it waits on. It moves no status; the
/// quest stays taken, since its work in progress is in the taker's tree. A wait made on a take that lost goes with the take, which the
/// rebase names on the take's conflict.
/// </summary>
[QuestOperation("waited", QuestOperationPhase.Log)]
internal sealed class QuestWaitOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Waited;

    /// <summary>The question it waits on, as its note.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>
    /// A wait made on a take that lost goes with the take, and is named on the take's conflict: applied to the winner's quest it
    /// would hold the winner's session on a question it never asked. A wait that lost for any other reason is named on nothing: the take's
    /// conflict is already written, and a remote may have numbered it.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses)
    {
        if (takeLost && operation.Note is { } on && !losses.Waits.Contains(on)) losses.Waits.Add(on);
        return Task.FromResult<IReadOnlyList<QuestOperation>>([]);
    }

    /// <summary>
    /// A lost take's note as given, then the waits that went with it: said on the conflict, which every
    /// machine shows, so the words hold wherever they are read.
    /// </summary>
    internal static string WaitsWentWith(string? note, IReadOnlyList<string> questions)
    {
        var named = questions.Select(q => $"#{q}").ToList();
        var said = named.Count == 1
            ? $"This take's wait on {named[0]} went with it, so this quest does not wait on that question's answer."
            : $"This take's waits on {string.Join(", ", named.Take(named.Count - 1))} and {named[^1]} went with it, "
              + "so this quest does not wait on those questions' answers.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>A wait on <paramref name="on"/>, judged by the replay's rule inside the write. It moves no status.</summary>
    /// <returns>
    /// The quest as it now stands and whether this call marked it; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take on the quest lost.
    /// </returns>
    internal async Task<QuestMove> WaitAsync(QuestTx tx, string id, string on, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        // Only a taken quest waits (QuestLog.Applies), and a wait the log held but the quest did not show would be a move nobody could see.
        if (quest.Status != QuestStatus.Taken || string.IsNullOrWhiteSpace(on)) return new QuestMove(quest, Moved: false);

        // A wait on the winner's take, made here after the pass that found this machine's take lost.
        var judged = new QuestOperation(id, QuestOperationKind.Waited, tx.Machine, 0, now, on);
        if (QuestLog.OnALostTake(quest, history, judged)) return new QuestMove(quest, Moved: false) { ClaimLost = true };

        var waiting = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(waiting).ConfigureAwait(false);
        return new QuestMove(waiting, Moved: true);
    }
}
