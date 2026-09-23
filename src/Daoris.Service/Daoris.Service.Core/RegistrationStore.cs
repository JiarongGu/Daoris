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

                -- Which commit each repository's fed knowledge came from (D48 §6). A table of its own
                -- rather than columns on `registrations`, and structurally so: an ordinary
                -- re-registration runs on every sync tick and says nothing about provenance, so a
                -- column would have to be preserved by care on every write. A row nothing but the feed
                -- writes cannot be erased by a write that was not about it.
                CREATE TABLE IF NOT EXISTS feed_provenance (
                  repository   TEXT PRIMARY KEY,
                  commit_id    TEXT NOT NULL,
                  committed_at TEXT NOT NULL,
                  branch       TEXT NOT NULL,
                  origin       TEXT NULL
                );

                -- A repository's fed code map (MAP3b), at its own commit: the map and the knowledge are
                -- fed separately and either can be refused while the other is taken. A NULL body is the
                -- repository saying it keeps none at that commit — the row stays, so a checkout from
                -- before the deletion cannot bring the map back.
                CREATE TABLE IF NOT EXISTS fed_code_maps (
                  repository   TEXT PRIMARY KEY,
                  file         TEXT NULL,
                  body         TEXT NULL,
                  commit_id    TEXT NOT NULL,
                  committed_at TEXT NOT NULL,
                  branch       TEXT NOT NULL,
                  origin       TEXT NULL,
                  digest       TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // What the held knowledge says, hashed (SYNC5a). NULL on every row from before it, which the
        // ordering reads as "compare nothing, take the same commit once".
        await EnsureColumnAsync("feed_provenance", "digest", "digest TEXT NULL", ct).ConfigureAwait(false);

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
            // The canonical line, told by the checkout that registered (D48 §6). NULL for every row
            // that predates it, which is exactly what "nobody said" means — and what makes any branch
            // feedable for a repository that never declared one.
            ("default_branch", "default_branch TEXT NULL"),
        })
        {
            await EnsureColumnAsync("registrations", column, definition, ct).ConfigureAwait(false);
        }
    }

    private async Task EnsureColumnAsync(string table, string column, string definition, CancellationToken ct)
    {
        await using var probe = _connection.CreateCommand();
        probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name";
        probe.Parameters.AddWithValue("$name", column);
        var present = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (present == 0)
        {
            await using var alter = _connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition}";
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
            INSERT INTO registrations (repository, summary, owns, accepts, packs, updated, root, joined, shares_knowledge, workspace, adopted, default_branch)
            VALUES ($repository, $summary, $owns, $accepts, $packs, $updated, $root, $joined, $shares,
                    COALESCE($workspace, '{Workspaces.Default}'), $adopted, $default_branch)
            ON CONFLICT (repository) DO UPDATE SET
              summary = $summary, owns = $owns, accepts = $accepts, packs = $packs, updated = $updated,
              root = $root, joined = $joined, shares_knowledge = $shares, adopted = $adopted,
              workspace = COALESCE($workspace, workspace, '{Workspaces.Default}'),
              -- Unstated preserves, for the same reason the workspace does: `daoris connect` says
              -- nothing about branches and runs on every tick, and a null that overwrote would erase
              -- what the driver declared — after which every feed would be taken from any branch.
              default_branch = COALESCE($default_branch, default_branch)
            RETURNING workspace, default_branch
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
        command.Parameters.AddWithValue(
            "$default_branch",
            string.IsNullOrWhiteSpace(registration.DefaultBranch)
                ? DBNull.Value
                : registration.DefaultBranch.Trim());

        // Both preserved fields come back, for the same reason: what is SERVED is what the store
        // decided, never what arrived (D48 §2) — otherwise the row in memory and the row on disk
        // disagree until a restart.
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return registration with { Workspace = registration.InWorkspace };
        }

        return registration with
        {
            Workspace = Workspaces.Normalize(reader.IsDBNull(0) ? null : reader.GetString(0)),
            DefaultBranch = reader.IsDBNull(1) ? null : reader.GetString(1),
        };
    }

    /// <summary>What commit a repository's knowledge was last fed from, or null where nothing has fed.</summary>
    public async Task<FeedProvenance?> ProvenanceAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT commit_id, committed_at, branch, origin, digest FROM feed_provenance "
            + "WHERE repository = $repository COLLATE NOCASE";
        command.Parameters.AddWithValue("$repository", repository);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>What commit a repository's code map was last fed from, or null where none has fed (MAP3b).</summary>
    public async Task<FeedProvenance?> CodeMapProvenanceAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT commit_id, committed_at, branch, origin, digest FROM fed_code_maps "
            + "WHERE repository = $repository COLLATE NOCASE";
        command.Parameters.AddWithValue("$repository", repository);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>The fed code map as held: which file it was, and its canonical text — both null when none is.</summary>
    public async Task<(string? File, string? Body)?> FedCodeMapAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT file, body FROM fed_code_maps WHERE repository = $repository COLLATE NOCASE";
        command.Parameters.AddWithValue("$repository", repository);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    /// <summary>Hold a repository's code map at a commit — or, with no body, hold that it keeps none there.</summary>
    public async Task RecordCodeMapAsync(
        string repository, string? file, string? body, FeedProvenance provenance, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO fed_code_maps (repository, file, body, commit_id, committed_at, branch, origin, digest)
            VALUES ($repository, $file, $body, $commit, $committed_at, $branch, $origin, $digest)
            ON CONFLICT (repository) DO UPDATE SET
              file = $file, body = $body, commit_id = $commit, committed_at = $committed_at,
              branch = $branch, origin = $origin, digest = $digest
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$file", (object?)file ?? DBNull.Value);
        command.Parameters.AddWithValue("$body", (object?)body ?? DBNull.Value);
        command.Parameters.AddWithValue("$commit", provenance.Commit);
        command.Parameters.AddWithValue("$committed_at", provenance.CommittedAt.ToString("O"));
        command.Parameters.AddWithValue("$branch", provenance.Branch);
        command.Parameters.AddWithValue("$origin", (object?)provenance.Origin ?? DBNull.Value);
        command.Parameters.AddWithValue("$digest", provenance.Digest ?? "");
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Every repository's fed provenance, for the one read a summary needs.</summary>
    public async Task<IReadOnlyDictionary<string, FeedProvenance>> AllProvenanceAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT repository, commit_id, committed_at, branch, origin FROM feed_provenance";

        var all = new Dictionary<string, FeedProvenance>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            all[reader.GetString(0)] = new(
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        return all;
    }

    /// <summary>Record which commit this deployment's copy of a repository's knowledge now stands on.</summary>
    public async Task RecordProvenanceAsync(
        string repository, FeedProvenance provenance, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO feed_provenance (repository, commit_id, committed_at, branch, origin, digest)
            VALUES ($repository, $commit, $committed_at, $branch, $origin, $digest)
            ON CONFLICT (repository) DO UPDATE SET
              commit_id = $commit, committed_at = $committed_at, branch = $branch, origin = $origin,
              digest = $digest
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$commit", provenance.Commit);
        command.Parameters.AddWithValue("$committed_at", provenance.CommittedAt.ToString("O"));
        command.Parameters.AddWithValue("$branch", provenance.Branch);
        command.Parameters.AddWithValue("$origin", (object?)provenance.Origin ?? DBNull.Value);
        command.Parameters.AddWithValue("$digest", (object?)provenance.Digest ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A held commit, read as (commit, committed_at, branch, origin, digest).</summary>
    private static FeedProvenance Read(SqliteDataReader reader) => new(
        reader.GetString(0),
        DateTimeOffset.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3))
    {
        Digest = reader.IsDBNull(4) ? null : reader.GetString(4),
    };

    /// <summary>
    /// Take a repository off the map. <b>Nothing on disk is touched</b>: this ends a registration, and
    /// the repository's files are its own (D48 §3/§7).
    /// </summary>
    /// <returns>Whether there was a row to retire; false is an answer, not a failure.</returns>
    public async Task<bool> DeleteAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        // The provenance goes with the registration: a repository off the map holds no position in
        // anyone's history here, and a leftover row would refuse the first feed after it re-joined as
        // though this deployment still held a newer commit — which it would not.
        command.CommandText = """
            DELETE FROM feed_provenance WHERE repository = $repository COLLATE NOCASE;
            DELETE FROM fed_code_maps WHERE repository = $repository COLLATE NOCASE;
            DELETE FROM registrations WHERE repository = $repository COLLATE NOCASE;
            """;
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
            "SELECT repository, summary, owns, accepts, packs, root, joined, shares_knowledge, workspace, adopted, "
            + "default_branch FROM registrations";

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
                Workspace: Workspaces.Normalize(reader.IsDBNull(8) ? null : reader.GetString(8)),
                DefaultBranch: reader.IsDBNull(10) ? null : reader.GetString(10)));
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
