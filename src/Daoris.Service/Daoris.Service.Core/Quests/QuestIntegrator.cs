using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// A machine's half of a fetch: keeps what a remote accepted under the machine that made it, then replays every quest the fetch touched,
/// accepted operations by number and pending ones after, so a pending move that no longer applies becomes a conflict and is never dropped.
/// </summary>
internal sealed class QuestIntegrator(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Take what a workspace's remote accepted, in its order, and rebase what this machine has not
    /// pushed on top — through the one table. Answers the cursor it now stands at and every move that
    /// became a conflict on the way.
    /// </summary>
    /// <param name="workspace">The circle whose remote these came from: what a publish among them is filed under here.</param>
    /// <param name="fetched">The remote's operations, each carrying its number.</param>
    /// <param name="through">The last number the fetch covered; the cursor moves there.</param>
    /// <remarks>
    /// An operation this machine already holds — its own, pushed and fetched back — takes the number it
    /// is given; any other is kept under the machine that made it. Then every quest the fetch touched
    /// is replayed: accepted operations by number, then pending ones. A pending move that no longer
    /// applies becomes a <see cref="QuestOperationKind.Conflict"/> and is never dropped.
    /// <para>An operation on a quest this machine forgot (<see cref="QuestStore.ForgetAsync"/>) is passed over, and the cursor still
    /// moves past it: a closed quest still takes a conflict or a dismissal from another machine, and kept, either
    /// would make half a quest here; a cursor back at zero would bring the whole of it.</para>
    /// </remarks>
    public Task<QuestIntegration> IntegrateAsync(
        string workspace, IReadOnlyList<QuestOperation> fetched, long through, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            var forgotten = await store.ForgottenAsync(transaction, inside).ConfigureAwait(false);
            var touched = new List<string>();
            foreach (var operation in fetched
                         .Where(o => o.Number is not null && !forgotten.Contains(o.Quest))
                         .OrderBy(o => o.Number))
            {
                if (await store.Sync.PositionOfAsync(operation.Machine, operation.Sequence, transaction, inside).ConfigureAwait(false)
                    is { } held)
                {
                    await store.Sync.NumberAsync(held, operation.Number!.Value, transaction, inside).ConfigureAwait(false);
                }
                else
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = circle } };
                    await store.Sync.KeepAsync(filed, operation.Number, transaction, inside).ConfigureAwait(false);
                }

                if (!touched.Contains(operation.Quest, StringComparer.Ordinal)) touched.Add(operation.Quest);
            }

            // A stray an older build left pending on a lost take sits on a quest no fetch touches once the
            // take's conflict has come back, so it is rebased whether or not anything arrived for it.
            foreach (var quest in await store.Sync.StrayQuestsAsync(circle, transaction, inside).ConfigureAwait(false))
            {
                if (!forgotten.Contains(quest) && !touched.Contains(quest, StringComparer.Ordinal)) touched.Add(quest);
            }

            var conflicts = new List<QuestOperation>();
            foreach (var quest in touched)
            {
                conflicts.AddRange(await store.Sync.RebaseAsync(quest, transaction, inside).ConfigureAwait(false));
            }

            var cursor = Math.Max(await store.CursorAsync(circle, transaction, inside).ConfigureAwait(false), through);
            await using (var command = _db.Command())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO quest_cursor (workspace, number) VALUES ($workspace, $number)
                    ON CONFLICT (workspace) DO UPDATE SET number = excluded.number
                    """;
                command.Parameters.AddWithValue("$workspace", circle);
                command.Parameters.AddWithValue("$number", cursor);
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return new QuestIntegration(cursor, conflicts);
        }, ct);
}
