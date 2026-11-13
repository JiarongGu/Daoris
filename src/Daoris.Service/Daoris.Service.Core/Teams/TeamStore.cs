using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>A part's standing in the store: the title its first publish used and the quest that publish made, null until it did.</summary>
public sealed record TeamPartRow(string Ask, string Part, string Title, string? Quest);

/// <summary>
/// The plan revisions and the part-to-quest memory, beside the asks in the service's store: machine-local like them. A revision is only ever
/// inserted, and an insert at a taken number loses, so two writers reading one revision cannot both append the next.
/// </summary>
public sealed class TeamStore
{
    private readonly ConnectionGate _db;

    private TeamStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static Task<TeamStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default) =>
        ConnectionGate.For(connection).RunAsync(() => new TeamStore(connection).EnsureSchemaAsync(ct), ct);

    private async Task<TeamStore> EnsureSchemaAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS team_plans (
              ask      TEXT NOT NULL,
              scope    TEXT NOT NULL,
              revision INTEGER NOT NULL,
              body     TEXT NOT NULL,
              PRIMARY KEY (ask, scope, revision)
            );
            CREATE TABLE IF NOT EXISTS team_parts (
              ask   TEXT NOT NULL,
              part  TEXT NOT NULL,
              title TEXT NOT NULL,
              quest TEXT NULL,
              PRIMARY KEY (ask, part)
            );
            CREATE INDEX IF NOT EXISTS team_parts_quest ON team_parts (quest);
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>The newest revision of each plan the ask holds: its own, then each part's split.</summary>
    public Task<IReadOnlyList<TeamPlan>> LatestAsync(string ask, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<TeamPlan>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT body FROM team_plans p WHERE ask = $ask
              AND revision = (SELECT MAX(revision) FROM team_plans WHERE ask = p.ask AND scope = p.scope)
            ORDER BY scope
            """;
        command.Parameters.AddWithValue("$ask", ask);
        var plans = new List<TeamPlan>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (TeamPlanCodec.Read(reader.GetString(0)) is { } plan) plans.Add(plan);
        }

        return plans;
    }, ct);

    /// <summary>Keeps <paramref name="plan"/> as its scope's next revision; false when that number is already taken.</summary>
    public Task<bool> AppendAsync(TeamPlan plan, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "INSERT INTO team_plans (ask, scope, revision, body) VALUES ($ask, $scope, $revision, $body) ON CONFLICT DO NOTHING";
        command.Parameters.AddWithValue("$ask", plan.Ask);
        command.Parameters.AddWithValue("$scope", plan.Scope ?? "");
        command.Parameters.AddWithValue("$revision", plan.Revision);
        command.Parameters.AddWithValue("$body", TeamPlanCodec.Write(plan));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }, ct);

    /// <summary>The part's row, made with <paramref name="title"/> the first time: a later title never replaces the first.</summary>
    public Task<TeamPartRow> ReserveAsync(string ask, string part, string title, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using (var insert = _db.Command())
        {
            insert.CommandText = "INSERT INTO team_parts (ask, part, title) VALUES ($ask, $part, $title) ON CONFLICT DO NOTHING";
            insert.Parameters.AddWithValue("$ask", ask);
            insert.Parameters.AddWithValue("$part", part);
            insert.Parameters.AddWithValue("$title", title);
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return (await ReadRowsAsync("ask = $ask AND part = $part", ("$ask", ask), ("$part", part), ct).ConfigureAwait(false))[0];
    }, ct);

    /// <summary>Keeps the quest a part's publish made.</summary>
    public Task RecordQuestAsync(string ask, string part, string quest, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE team_parts SET quest = $quest WHERE ask = $ask AND part = $part";
        command.Parameters.AddWithValue("$ask", ask);
        command.Parameters.AddWithValue("$part", part);
        command.Parameters.AddWithValue("$quest", quest);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Every part the ask has reserved, with its quest where it has one.</summary>
    public Task<IReadOnlyList<TeamPartRow>> PartsAsync(string ask, CancellationToken ct = default) =>
        _db.RunAsync(() => ReadRowsAsync("ask = $ask", ("$ask", ask), null, ct), ct);

    /// <summary>The part a quest is, or null for a quest no plan published.</summary>
    public Task<TeamPartRow?> OfQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<TeamPartRow?>(async () =>
        (await ReadRowsAsync("quest = $quest", ("$quest", quest), null, ct).ConfigureAwait(false)).FirstOrDefault(), ct);

    private async Task<IReadOnlyList<TeamPartRow>> ReadRowsAsync(
        string where, (string, string) first, (string, string)? second, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT ask, part, title, quest FROM team_parts WHERE {where} ORDER BY rowid";
        command.Parameters.AddWithValue(first.Item1, first.Item2);
        if (second is { } other) command.Parameters.AddWithValue(other.Item1, other.Item2);
        var rows = new List<TeamPartRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return rows;
    }
}
