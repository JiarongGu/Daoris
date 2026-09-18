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
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS registrations (
              repository TEXT PRIMARY KEY,
              summary    TEXT NULL,
              owns       TEXT NOT NULL,
              accepts    TEXT NOT NULL,
              packs      TEXT NOT NULL,
              updated    TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Record what a repository declared. Re-registering replaces: the repository is the identity.</summary>
    public async Task UpsertAsync(Registration registration, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO registrations (repository, summary, owns, accepts, packs, updated)
            VALUES ($repository, $summary, $owns, $accepts, $packs, $updated)
            ON CONFLICT (repository) DO UPDATE SET
              summary = $summary, owns = $owns, accepts = $accepts, packs = $packs, updated = $updated
            """;
        command.Parameters.AddWithValue("$repository", registration.Repository);
        command.Parameters.AddWithValue("$summary", (object?)registration.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("$owns", ToJson(registration.Owns));
        command.Parameters.AddWithValue("$accepts", ToJson(registration.Accepts));
        command.Parameters.AddWithValue("$packs", ToJson(registration.Packs));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Every declaration ever pushed. Entry counts are the index's to add, not ours to store.</summary>
    public async Task<IReadOnlyList<Registration>> AllAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT repository, summary, owns, accepts, packs FROM registrations";

        var registrations = new List<Registration>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            registrations.Add(new Registration(
                reader.GetString(0),
                Adopted: true,
                reader.IsDBNull(1) ? null : reader.GetString(1),
                FromJson(reader.GetString(2)),
                FromJson(reader.GetString(3)),
                FromJson(reader.GetString(4)),
                Entries: 0));
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
