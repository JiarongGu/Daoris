using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Registrations a client pushed with `daoris connect`, persisted beside the index.
/// </summary>
/// <remarks>
/// <para><b>A registration must outlive the process that received it.</b> The first version held pushed
/// registrations in memory, which meant every restart silently dropped every repository that had ever
/// connected — and for a remote service, pushed registrations are the only registrations there are,
/// because it cannot see anyone's disk. A repository that "registered" and then vanished on the next
/// restart looks exactly like one that never registered, which is the failure `connect` exists to
/// remove.</para>
///
/// <para>Stored in the same database as the index and the quests: one file to back up, and one answer
/// to "which repositories exist" (the same reasoning that put quests here — two files can disagree).</para>
///
/// <para>Only what the repository DECLARED is stored. `Adopted` is true by construction — pushing a
/// registration is what adoption looks like from a remote service — and entry counts belong to the
/// index, which recomputes them on every read.</para>
/// </remarks>
public sealed class RegistrationStore
{
    private readonly SqliteConnection _connection;

    private RegistrationStore(SqliteConnection connection) => _connection = connection;

    public static async Task<RegistrationStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new RegistrationStore(connection);
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS registrations (
                  repository TEXT PRIMARY KEY,
                  summary    TEXT NULL,
                  owns       TEXT NOT NULL,
                  accepts    TEXT NOT NULL,
                  packs      TEXT NOT NULL,
                  updated    TEXT NOT NULL,
                  root       TEXT NULL,
                  workspace  TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  adopted    INTEGER NOT NULL DEFAULT 1
                );

                -- Facts about the registry itself rather than about any repository — today exactly one:
                -- whether this store has ever been managed, which is what keeps the bootstrap import
                -- from running twice and resurrecting what someone retired (D48 §3).
                CREATE TABLE IF NOT EXISTS registry_meta (
                  key   TEXT PRIMARY KEY,
                  value TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store created before the driver existed has no root column — and one created before the
        // remote existed has no declaration columns. Registrations must survive the upgrade: a schema
        // that only works on a fresh database silently drops every repository that ever connected,
        // which is exactly the failure this store was built to remove.
        foreach (var (column, definition) in new[]
        {
            ("root", "root TEXT NULL"),
            ("joined", "joined INTEGER NOT NULL DEFAULT 0"),
            ("shares_knowledge", "shares_knowledge INTEGER NOT NULL DEFAULT 0"),
            // Workspaces arrived last (D48), and every registration that predates them belongs to the
            // one group there was — which is what the default spells.
            ("workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            // Adoption became a stored fact when the registry became the authority (D48 §3): before
            // that it was derived from a scan, and every row here arrived by being pushed — which is
            // what adoption looks like from a service. Hence the default.
            ("adopted", "adopted INTEGER NOT NULL DEFAULT 1"),
        })
        {
            await using var probe = _connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('registrations') WHERE name = $name";
            probe.Parameters.AddWithValue("$name", column);
            var present = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (present == 0)
            {
                await using var alter = _connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE registrations ADD COLUMN {definition}";
                await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Record what a repository declared. Re-registering replaces: the repository is the identity.
    /// </summary>
    /// <remarks>
    /// The workspace is the one field a re-registration may leave alone. It is WIRING — set once when
    /// the person adds the repository to a circle, and carried by no manifest (D48 §2) — while an
    /// ordinary `connect` runs on every sync tick and says nothing about it. So the statement wins when
    /// there is one, the existing row wins when there is not, and `default` closes it: exactly the
    /// COALESCE below, decided here in one statement rather than as a read-modify-write two callers
    /// would race on.
    /// </remarks>
    /// <returns>The row as it now stands, so the caller never has to guess which workspace took.</returns>
    public async Task<Registration> UpsertAsync(
        Registration registration, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO registrations (repository, summary, owns, accepts, packs, updated, root, joined, shares_knowledge, workspace, adopted)
            VALUES ($repository, $summary, $owns, $accepts, $packs, $updated, $root, $joined, $shares,
                    COALESCE($workspace, '{Workspaces.Default}'), $adopted)
            ON CONFLICT (repository) DO UPDATE SET
              summary = $summary, owns = $owns, accepts = $accepts, packs = $packs, updated = $updated,
              root = $root, joined = $joined, shares_knowledge = $shares, adopted = $adopted,
              workspace = COALESCE($workspace, workspace, '{Workspaces.Default}')
            RETURNING workspace
            """;
        command.Parameters.AddWithValue("$adopted", registration.Adopted ? 1 : 0);
        command.Parameters.AddWithValue(
            "$workspace",
            registration.Workspace is null ? DBNull.Value : Workspaces.Normalize(registration.Workspace));
        command.Parameters.AddWithValue("$repository", registration.Repository);
        command.Parameters.AddWithValue("$summary", (object?)registration.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("$owns", ToJson(registration.Owns));
        command.Parameters.AddWithValue("$accepts", ToJson(registration.Accepts));
        command.Parameters.AddWithValue("$packs", ToJson(registration.Packs));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        command.Parameters.AddWithValue("$root", (object?)registration.Root ?? DBNull.Value);
        command.Parameters.AddWithValue("$joined", registration.Joined ? 1 : 0);
        command.Parameters.AddWithValue("$shares", registration.SharesKnowledge ? 1 : 0);
        var effective = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;

        return registration with { Workspace = Workspaces.Normalize(effective) };
    }

    /// <summary>
    /// Take a repository off the map. <b>Nothing on disk is touched</b>: this ends a registration, and
    /// the repository's files are its own (D48 §3/§7).
    /// </summary>
    /// <returns>Whether there was a row to retire; false is an answer, not a failure.</returns>
    public async Task<bool> DeleteAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM registrations WHERE repository = $repository COLLATE NOCASE";
        command.Parameters.AddWithValue("$repository", repository);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// Whether this store has ever been managed — and marking that it now has.
    /// </summary>
    /// <remarks>
    /// The bootstrap import runs exactly once (D48 §3). Running it again on every start would
    /// resurrect every repository the person deliberately retired, which would make "remove" mean
    /// "until the next restart". The marker is set whether or not the import found anything: an empty
    /// root is still an answer, and asking again next time would re-open the same hole.
    /// </remarks>
    public async Task<bool> WasImportedAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM registry_meta WHERE key = 'bootstrap-import'";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
    }

    /// <summary>Record that the bootstrap import has happened, and from where.</summary>
    public async Task MarkImportedAsync(string folder, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            "INSERT OR REPLACE INTO registry_meta (key, value) VALUES ('bootstrap-import', $folder)";
        command.Parameters.AddWithValue("$folder", folder);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Every registration this machine holds. Entry counts are the index's to add, not ours to store.</summary>
    public async Task<IReadOnlyList<Registration>> AllAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT repository, summary, owns, accepts, packs, root, joined, shares_knowledge, workspace, adopted "
            + "FROM registrations";

        var registrations = new List<Registration>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            registrations.Add(new Registration(
                reader.GetString(0),
                Adopted: reader.GetInt32(9) != 0,
                reader.IsDBNull(1) ? null : reader.GetString(1),
                FromJson(reader.GetString(2)),
                FromJson(reader.GetString(3)),
                FromJson(reader.GetString(4)),
                Entries: 0,
                Root: reader.IsDBNull(5) ? null : reader.GetString(5),
                Joined: reader.GetInt32(6) != 0,
                SharesKnowledge: reader.GetInt32(7) != 0,
                Workspace: Workspaces.Normalize(reader.IsDBNull(8) ? null : reader.GetString(8))));
        }

        return registrations;
    }

    // Written and read by hand rather than through the reflection serializer, for the same reason the
    // HTTP host uses source-generated JSON: nothing here may quietly stop working under AOT.
    private static string ToJson(IReadOnlyList<string> items)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var item in items) writer.WriteStringValue(item);
            writer.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IReadOnlyList<string> FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = new List<string>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
