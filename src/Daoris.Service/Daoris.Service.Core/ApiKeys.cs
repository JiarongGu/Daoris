using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>A minted key's public face: everything except the secret, which is shown once and never stored.</summary>
/// <param name="Prefix">Short and non-secret — how a key is named in an audit log or a revocation, so
/// the full key never has to be written anywhere to be talked about (service design §5b).</param>
/// <param name="Name">Whose key, on which machine — per person AND per machine, so revoking one
/// disrupts exactly one (D47 §7). A label, not an identity model.</param>
public sealed record ApiKeyRecord(
    string Prefix, string Name, DateTimeOffset Created, DateTimeOffset Expires, DateTimeOffset? Revoked);

/// <param name="Key">The full key. This record is the ONE moment it exists outside the caller's hands —
/// it is returned by the mint and can never be read back, because only its hash is stored.</param>
public sealed record MintedKey(string Key, ApiKeyRecord Record);

public enum KeyVerdict
{
    /// <summary>Present, matching, live.</summary>
    Valid,

    /// <summary>No such key — including a near-miss with a known prefix, which must look identical.</summary>
    Unknown,

    /// <summary>Past its expiry. Expiring by default is the answer to what a leaked key costs (§5b).</summary>
    Expired,

    /// <summary>Deliberately ended, by prefix, before its time.</summary>
    Revoked,
}

/// <param name="Key">The record for a key that exists (valid, expired or revoked) — the attribution a
/// keyed write carries. Null when the key is unknown, so nothing can be learned by guessing.</param>
public sealed record KeyValidation(KeyVerdict Verdict, ApiKeyRecord? Key);

/// <summary>
/// The shared deployment's machine credentials (D47 §7): per-person per-machine keys, minted at the
/// deployment, stored as a hash beside a short audit prefix, expiring by default.
/// </summary>
/// <remarks>
/// <para><b>The store cannot reproduce a key.</b> What is persisted is the SHA-256 of the full key and
/// the first characters after its marker — enough to name it, never enough to present it. A copied
/// database therefore leaks who has keys and when they expire, not the keys.</para>
///
/// <para><b>Comparison is fixed-time over the hashes</b>, so the lookup itself cannot say how much of
/// a guess matched.</para>
/// </remarks>
public sealed class ApiKeyStore
{
    private const string Marker = "dk_";
    private const int PrefixLength = 8;

    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
    private readonly ConnectionGate _db;

    private ApiKeyStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static Task<ApiKeyStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default) =>
        ConnectionGate.For(connection).RunAsync(async () =>
    {
        var store = new ApiKeyStore(connection);
        await using var command = store._db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS api_keys (
              prefix  TEXT PRIMARY KEY,
              hash    TEXT NOT NULL,
              name    TEXT NOT NULL,
              created TEXT NOT NULL,
              expires TEXT NOT NULL,
              revoked TEXT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return store;
    }, ct);

    /// <summary>Mint a key. The returned secret is shown once by the caller and never stored.</summary>
    public Task<MintedKey> MintAsync(
        string name, TimeSpan lifetime, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync<MintedKey>(async () =>
    {
        var key = Marker + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var record = new ApiKeyRecord(
            key.Substring(Marker.Length, PrefixLength), name, now, now.Add(lifetime), Revoked: null);

        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO api_keys (prefix, hash, name, created, expires, revoked)
            VALUES ($prefix, $hash, $name, $created, $expires, NULL)
            """;
        command.Parameters.AddWithValue("$prefix", record.Prefix);
        command.Parameters.AddWithValue("$hash", HashOf(key));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$created", record.Created.ToString("O"));
        command.Parameters.AddWithValue("$expires", record.Expires.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return new(key, record);
    }, ct);

    /// <summary>
    /// Judge a presented key. Unknown carries no record at all, so a caller learns nothing by guessing;
    /// expired and revoked carry theirs, because naming the state to the key's own holder is what lets
    /// them fix it.
    /// </summary>
    public Task<KeyValidation> ValidateAsync(
        string presented, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync<KeyValidation>(async () =>
    {
        presented = presented.Trim();
        if (!presented.StartsWith(Marker, StringComparison.Ordinal)
            || presented.Length < Marker.Length + PrefixLength)
        {
            return new(KeyVerdict.Unknown, Key: null);
        }

        await using var command = _db.Command();
        command.CommandText =
            "SELECT prefix, hash, name, created, expires, revoked FROM api_keys WHERE prefix = $prefix";
        command.Parameters.AddWithValue("$prefix", presented.Substring(Marker.Length, PrefixLength));

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return new(KeyVerdict.Unknown, Key: null);

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(HashOf(presented)), Encoding.UTF8.GetBytes(reader.GetString(1)));
        if (!matches) return new(KeyVerdict.Unknown, Key: null);

        var record = new ApiKeyRecord(
            reader.GetString(0), reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)));

        if (record.Revoked is not null) return new(KeyVerdict.Revoked, record);
        if (record.Expires <= now) return new(KeyVerdict.Expired, record);
        return new(KeyVerdict.Valid, record);
    }, ct);

    /// <summary>End a key by its prefix. True when the prefix named a key; idempotent on a re-revoke.</summary>
    public Task<bool> RevokeAsync(string prefix, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText =
            "UPDATE api_keys SET revoked = COALESCE(revoked, $now) WHERE prefix = $prefix";
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.Parameters.AddWithValue("$prefix", prefix);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>Every key ever minted, by its public face — the audit view.</summary>
    public Task<IReadOnlyList<ApiKeyRecord>> ListAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<ApiKeyRecord>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText =
            "SELECT prefix, name, created, expires, revoked FROM api_keys ORDER BY created, prefix";

        var records = new List<ApiKeyRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            records.Add(new ApiKeyRecord(
                reader.GetString(0), reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)), DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4))));
        }

        return records;
    }, ct);

    private static string HashOf(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
