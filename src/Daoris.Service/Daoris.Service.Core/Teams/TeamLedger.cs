using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The rows that say who is on an ask's team and the choices taken about it, beside the plan revisions in the service's store. A member's row
/// moves to the newest revision it served, and a decision at a revision is kept as first written.
/// </summary>
public sealed class TeamLedger
{
    private readonly ConnectionGate _db;

    private TeamLedger(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static Task<TeamLedger> OpenAsync(SqliteConnection connection, CancellationToken ct = default) =>
        ConnectionGate.For(connection).RunAsync(() => new TeamLedger(connection).EnsureSchemaAsync(ct), ct);

    private async Task<TeamLedger> EnsureSchemaAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS team_members (
              ask      TEXT NOT NULL,
              part     TEXT NOT NULL,
              role     TEXT NOT NULL,
              session  TEXT NOT NULL,
              revision INTEGER NOT NULL,
              at       TEXT NOT NULL,
              PRIMARY KEY (ask, part, role, session)
            );
            CREATE TABLE IF NOT EXISTS team_decisions (
              ask      TEXT NOT NULL,
              scope    TEXT NOT NULL,
              revision INTEGER NOT NULL,
              subject  TEXT NOT NULL,
              decision TEXT NOT NULL,
              reason   TEXT NOT NULL,
              author   TEXT NOT NULL,
              act      TEXT NOT NULL,
              at       TEXT NOT NULL,
              PRIMARY KEY (ask, scope, revision, subject)
            );
            CREATE TABLE IF NOT EXISTS team_scouted (
              ask     TEXT NOT NULL,
              part    TEXT NOT NULL,
              session TEXT NOT NULL,
              pack    TEXT NOT NULL,
              at      TEXT NOT NULL,
              PRIMARY KEY (ask, part, session)
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>Keeps <paramref name="member"/>, or moves the row it names to the newer revision.</summary>
    public Task KeepAsync(TeamMember member, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO team_members (ask, part, role, session, revision, at) VALUES ($ask, $part, $role, $session, $revision, $at)
            ON CONFLICT (ask, part, role, session) DO UPDATE SET revision = MAX(revision, excluded.revision), at = excluded.at
            """;
        command.Parameters.AddWithValue("$ask", member.Ask);
        command.Parameters.AddWithValue("$part", member.Part ?? "");
        command.Parameters.AddWithValue("$role", member.Role);
        command.Parameters.AddWithValue("$session", member.Session);
        command.Parameters.AddWithValue("$revision", member.Revision);
        command.Parameters.AddWithValue("$at", member.At.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Keeps <paramref name="decision"/>; a decision already kept at its subject and revision stands.</summary>
    public Task KeepAsync(TeamDecision decision, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO team_decisions (ask, scope, revision, subject, decision, reason, author, act, at)
            VALUES ($ask, $scope, $revision, $subject, $decision, $reason, $author, $act, $at) ON CONFLICT DO NOTHING
            """;
        command.Parameters.AddWithValue("$ask", decision.Ask);
        command.Parameters.AddWithValue("$scope", decision.Scope ?? "");
        command.Parameters.AddWithValue("$revision", decision.Revision);
        command.Parameters.AddWithValue("$subject", decision.Subject);
        command.Parameters.AddWithValue("$decision", decision.Decision);
        command.Parameters.AddWithValue("$reason", decision.Reason);
        command.Parameters.AddWithValue("$author", decision.Author);
        command.Parameters.AddWithValue("$act", decision.Act);
        command.Parameters.AddWithValue("$at", decision.At.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Keeps what a scout answered for a part; a scout answers a part once, so the first answer stands.</summary>
    public Task KeepAsync(TeamScouted scouted, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "INSERT INTO team_scouted (ask, part, session, pack, at) VALUES ($ask, $part, $session, $pack, $at) ON CONFLICT DO NOTHING";
        command.Parameters.AddWithValue("$ask", scouted.Ask);
        command.Parameters.AddWithValue("$part", scouted.Part);
        command.Parameters.AddWithValue("$session", scouted.Session);
        command.Parameters.AddWithValue("$pack", scouted.Pack);
        command.Parameters.AddWithValue("$at", scouted.At.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>What the ask's scouts answered, oldest first.</summary>
    public Task<IReadOnlyList<TeamScouted>> ScoutedAsync(string ask, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<TeamScouted>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT part, session, pack, at FROM team_scouted WHERE ask = $ask ORDER BY rowid";
        command.Parameters.AddWithValue("$ask", ask);
        var rows = new List<TeamScouted>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new(ask, reader.GetString(0), reader.GetString(1), reader.GetString(2), At(reader, 3)));
        }

        return rows;
    }, ct);

    /// <summary>The ask's team, oldest first.</summary>
    public Task<IReadOnlyList<TeamMember>> MembersAsync(string ask, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<TeamMember>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT part, role, session, revision, at FROM team_members WHERE ask = $ask ORDER BY rowid";
        command.Parameters.AddWithValue("$ask", ask);
        var rows = new List<TeamMember>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new(ask, reader.GetString(0) is { Length: > 0 } part ? part : null, reader.GetString(1), reader.GetString(2), reader.GetInt32(3), At(reader, 4)));
        }

        return rows;
    }, ct);

    /// <summary>The choices taken about the ask's team, oldest first.</summary>
    public Task<IReadOnlyList<TeamDecision>> DecisionsAsync(string ask, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<TeamDecision>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT scope, revision, subject, decision, reason, author, act, at FROM team_decisions WHERE ask = $ask ORDER BY rowid";
        command.Parameters.AddWithValue("$ask", ask);
        var rows = new List<TeamDecision>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new(
                ask, reader.GetString(0) is { Length: > 0 } scope ? scope : null, reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), At(reader, 7)));
        }

        return rows;
    }, ct);

    private static DateTimeOffset At(SqliteDataReader reader, int column) =>
        DateTimeOffset.Parse(reader.GetString(column), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
