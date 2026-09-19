using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Per-person, per-machine keys for a shared deployment (D47 §7, service design §5b): minted once and
/// shown once, stored only as a hash beside a short non-secret prefix kept for audit and revocation,
/// expiring by default. What a leaked STORE costs is the design's own test — a copied database must
/// not contain anything a caller could present.
/// </summary>
public sealed class ApiKeyStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ApiKeyStore _keys = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _keys = await ApiKeyStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Minting_returns_the_key_once_and_stores_only_its_hash()
    {
        var minted = await _keys.MintAsync("alice@laptop", TimeSpan.FromDays(90), Now);

        Assert.StartsWith("dk_", minted.Key);
        Assert.Equal(minted.Key[3..11], minted.Record.Prefix);
        Assert.Equal("alice@laptop", minted.Record.Name);
        Assert.Equal(Now.AddDays(90), minted.Record.Expires);

        // The store must be unable to reproduce the key: nothing readable back contains it.
        var listed = Assert.Single(await _keys.ListAsync());
        Assert.Equal(minted.Record.Prefix, listed.Prefix);
        Assert.DoesNotContain(minted.Key, listed.Prefix + listed.Name);

        var validation = await _keys.ValidateAsync(minted.Key, Now);
        Assert.Equal(KeyVerdict.Valid, validation.Verdict);
        Assert.Equal("alice@laptop", validation.Key!.Name);
    }

    /// <summary>A near-miss must look exactly like a miss: right prefix, wrong secret, same answer.</summary>
    [Fact]
    public async Task A_wrong_key_is_unknown()
    {
        var minted = await _keys.MintAsync("alice@laptop", TimeSpan.FromDays(90), Now);
        var samePrefix = minted.Key[..^4] + "0000";

        Assert.Equal(KeyVerdict.Unknown, (await _keys.ValidateAsync(samePrefix, Now)).Verdict);
        Assert.Equal(KeyVerdict.Unknown, (await _keys.ValidateAsync("dk_not-a-key", Now)).Verdict);
        Assert.Equal(KeyVerdict.Unknown, (await _keys.ValidateAsync("", Now)).Verdict);
        Assert.Equal(KeyVerdict.Unknown, (await _keys.ValidateAsync("Bearer whatever", Now)).Verdict);
    }

    /// <summary>Expiring by default is the design's answer to what a leaked key costs (§5b).</summary>
    [Fact]
    public async Task An_expired_key_is_named_expired()
    {
        var minted = await _keys.MintAsync("alice@laptop", TimeSpan.FromHours(1), Now);

        var before = await _keys.ValidateAsync(minted.Key, Now.AddMinutes(59));
        var after = await _keys.ValidateAsync(minted.Key, Now.AddHours(2));

        Assert.Equal(KeyVerdict.Valid, before.Verdict);
        Assert.Equal(KeyVerdict.Expired, after.Verdict);
    }

    /// <summary>Revocation is named by the PREFIX — the audit handle — never by presenting the key.</summary>
    [Fact]
    public async Task A_revoked_key_is_named_revoked()
    {
        var minted = await _keys.MintAsync("alice@laptop", TimeSpan.FromDays(90), Now);

        Assert.True(await _keys.RevokeAsync(minted.Record.Prefix, Now.AddDays(1)));
        Assert.False(await _keys.RevokeAsync("00000000", Now.AddDays(1)));

        var validation = await _keys.ValidateAsync(minted.Key, Now.AddDays(2));
        Assert.Equal(KeyVerdict.Revoked, validation.Verdict);
        Assert.NotNull((await _keys.ListAsync())[0].Revoked);
    }

    [Fact]
    public async Task Keys_are_distinct_and_both_valid()
    {
        var one = await _keys.MintAsync("alice@laptop", TimeSpan.FromDays(90), Now);
        var two = await _keys.MintAsync("alice@desktop", TimeSpan.FromDays(90), Now);

        Assert.NotEqual(one.Key, two.Key);
        Assert.Equal(KeyVerdict.Valid, (await _keys.ValidateAsync(one.Key, Now)).Verdict);
        Assert.Equal(KeyVerdict.Valid, (await _keys.ValidateAsync(two.Key, Now)).Verdict);
        Assert.Equal(2, (await _keys.ListAsync()).Count);
    }
}
