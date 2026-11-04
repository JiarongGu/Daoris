using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>Reads the <c>follows</c> chain a quest is on, from the quests table.</summary>
internal sealed class QuestChain(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Every quest of the <c>follows</c> chain <paramref name="id"/> is on (D149 point 1; REVIEWENV1b3): its first quest, found by
    /// walking <c>parent</c> up, and every quest that follows it at any depth, a chain step its close published or a set-up step
    /// a door published after it. Empty for an unknown id. The late set-up door judges a repository's part of the chain on it.
    /// </summary>
    public Task<IReadOnlyList<Quest>> ChainAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<Quest>>(async () =>
        {
            async Task<Quest?> One(string named)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE id = $id";
                command.Parameters.AddWithValue("$id", named);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
            }

            async Task<List<Quest>> Following(string parent)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE parent = $parent ORDER BY filed";
                command.Parameters.AddWithValue("$parent", parent);
                var steps = new List<Quest>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) steps.Add(Read(reader));
                return steps;
            }

            // Up to the first quest; a parent seen twice, which no chain makes, ends the walk rather than looping.
            if (await One(id).ConfigureAwait(false) is not { } first) return [];
            var seen = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            while (first.Parent is { } parent && seen.Add(parent) && await One(parent).ConfigureAwait(false) is { } above) first = above;

            var chain = new List<Quest> { first };
            var visited = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            for (var next = 0; next < chain.Count; next++)
            {
                foreach (var step in await Following(chain[next].Id).ConfigureAwait(false))
                {
                    if (visited.Add(step.Id)) chain.Add(step);
                }
            }

            return chain;
        }, ct);
}
