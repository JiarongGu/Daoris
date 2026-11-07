using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// The follow-up steps a quest's close, yes or submission published, rewritten as the rebase's own conflicts where the parent's move is lost,
/// so a step whose parent went another way is never left standing and synced on its own.
/// </summary>
internal sealed class QuestFollowUps(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// A follow-up published only by a close that has now lost: it is the chain moving on,
    /// and the chain does not move. Forgotten only while every operation of it is this machine's and none is
    /// pushed; one another machine holds, or published itself on its own close, stays.
    /// </summary>
    /// <remarks>
    /// What this machine did on it goes with it, as a lost submission's step's does (<see cref="ForgetStepsAsync"/>): a
    /// take of it becomes a conflict naming the close that went, so its claim reads lost and its session is stopped.
    /// A remote holds no such follow-up while its parent
    /// is not done there (<see cref="AwaitsItsSourceAsync"/>), so the raced order reaches this too.
    /// </remarks>
    /// <param name="published">What published it, as its take's conflict names it: the parent's close, or the yes or verdict that let it go.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    internal Task<IReadOnlyList<QuestOperation>> ForgetFollowUpsAsync(
        string parent, bool takeLost, string published, bool reopened, SqliteTransaction transaction, CancellationToken ct) =>
        ForgetStepsAsync(parent, takeLost, reopened, transaction, ct, published);

    /// <summary>
    /// The steps a lost submission published, with what this machine did on them since, and the steps they
    /// published in turn: each grew from a submission that no longer stands, so none is asked. A step is forgotten only while
    /// every operation of it is this machine's and none is pushed; one a remote holds is every machine's, and stays. A lost close's
    /// follow-ups go the same way (<see cref="ForgetFollowUpsAsync"/>).
    /// </summary>
    /// <remarks>
    /// Where this machine took a step, its take is not forgotten but rewritten as a conflict, which the claim reads as lost
    /// (<see cref="QuestLog.Claim"/>), so the driver stops the step's session as it stops any session whose take lost. Its
    /// publication gone, the conflict is on a quest no list shows and no push carries, as one on a quest deleted first is,
    /// and its note says why: the parent's submission went. Should the winner's chain publish the same step later (its id comes
    /// from the parent and the step), the conflict stands on it and goes up, as any take that lost is shown on the winner's quest.
    /// <para>Where the parent's take lost to the person's reopen (<paramref name="reopened"/>), the conflict says so in
    /// its words and in <see cref="QuestOperation.LostToTheReopen"/>: the step's history holds only the conflict, so the claim's answer
    /// (<see cref="LostToTheReopenAsync"/>) could not read the reopen from it, and the driver would stop the session in another
    /// machine's words.</para>
    /// </remarks>
    /// <param name="published">What of the parent published it, as its take's conflict names it: its submission, or its close.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    private async Task<IReadOnlyList<QuestOperation>> ForgetStepsAsync(
        string parent, bool takeLost, bool reopened, SqliteTransaction transaction, CancellationToken ct, string published = "submission")
    {
        var children = new List<string>();
        await using (var command = _db.Command())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM quests WHERE parent = $parent";
            command.Parameters.AddWithValue("$parent", parent);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) children.Add(reader.GetString(0));
        }

        var told = new List<QuestOperation>();
        foreach (var child in children)
        {
            var history = await store.HistoryAsync(child, transaction, ct).ConfigureAwait(false);
            if (history is not [{ Kind: QuestOperationKind.Published }, ..]
                || history.Any(operation => operation.Number is not null || operation.Machine != store.Machine))
            {
                continue;
            }

            told.AddRange(await ForgetStepsAsync(child, takeLost, reopened, transaction, ct, published).ConfigureAwait(false));
            foreach (var operation in history)
            {
                var position = (await store.Sync.PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct).ConfigureAwait(false))!.Value;
                if (operation.Kind != QuestOperationKind.Taken)
                {
                    await store.Sync.ForgetOperationAsync(position, transaction, ct).ConfigureAwait(false);
                    continue;
                }

                var lost = operation with
                {
                    Kind = QuestOperationKind.Conflict, Attempted = QuestStatus.Taken,
                    Note = StepWentWith(parent, takeLost, operation.Note, reopened, published),
                    NoteParts = null, WhileOpen = false, Close = null, Submission = null, LostToTheReopen = reopened,
                };
                await store.Sync.RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
                told.Add(lost);
            }

            await store.Sync.DropCacheAsync(child, transaction, ct).ConfigureAwait(false);
        }

        return told;
    }

    /// <summary>
    /// A lost step's take's note as given, then why the step went: the parent's submission that published
    /// it no longer stands, with the parent's take or to another machine's move on the parent. A parent's take lost to the person's
    /// reopen (<paramref name="reopened"/>) has no winner, so it says the reopen, as the take's refusals do
    /// (<see cref="QuestExchange.LostTake"/>). A step a close published names that close instead (<paramref name="published"/>).
    /// </summary>
    internal static string StepWentWith(string parent, bool takeLost, string? note, bool reopened = false, string published = "submission")
    {
        var said = $"This quest was the next step of #{parent}'s {published}, which "
                   + (!takeLost ? $"lost to another machine's move on #{parent}"
                      : reopened ? $"went with this machine's take of #{parent}: the person's reopen of the work it grew from reached the "
                                   + "remote first"
                      : $"went with this machine's take of #{parent}: another machine's take reached the remote first")
                   + ". The step went with it, so its work is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>
    /// A lost step's take's note as given, then why it lost: the step is published anew on other work,
    /// from the submission another machine made of <paramref name="parent"/>, so a take made on the work that went is not wanted.
    /// </summary>
    internal static string StepPublishedAnew(string parent, string? note)
    {
        var said = $"This quest was the next step of #{parent}'s submission, and was published anew on other work, from the submission "
                   + $"another machine made of #{parent}. This machine's take of it, made on the work that went, is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }
}
