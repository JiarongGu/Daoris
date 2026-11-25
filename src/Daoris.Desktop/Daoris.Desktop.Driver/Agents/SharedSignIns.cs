using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// A one-way fingerprint of the refresh token an account's store holds: a SHA-256 digest of the token's bytes as the store has them,
/// made inside the provider's reader (<see cref="IStoredSignIn.Fingerprint"/>) and kept in memory to be compared with another's, never
/// logged, shown or written. Two stores holding one refresh token have equal fingerprints.
/// </summary>
/// <param name="Digest">The digest; its text is never made.</param>
public sealed record StoredFingerprint(byte[] Digest)
{
    /// <summary>Whether <paramref name="other"/> is the same token's.</summary>
    public bool Same(StoredFingerprint other) => Digest.AsSpan().SequenceEqual(other.Digest);

    /// <summary>Never the digest: a record's own text would print it.</summary>
    public override string ToString() => "StoredFingerprint";

    /// <summary>The digest of <paramref name="token"/>'s bytes (a JSON string's own, quotes and all, since no text of a token is made).</summary>
    internal static StoredFingerprint Of(ReadOnlySpan<byte> token) => new(SHA256.HashData(token));
}

/// <summary>
/// Accounts of one agent whose stores hold one refresh token: a store copied from another sign-in, or two accounts signed in with one.
/// A single-use refresh token spent by one is spent for the others, so whichever refreshes first signs the rest out, and no read or start of
/// Daoris's can tell which will. Said once on each such account in the roster, with how to separate them: sign each in on its own.
/// </summary>
internal static class SharedSignIns
{
    /// <summary>
    /// For each account of <paramref name="owner"/> whose store shares its refresh token with another of its accounts, the names of those
    /// others. An account whose store is not read, has no refresh token or shares none is left out. Only fingerprints are compared.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Of(string home, string owner, IStoredSignIn? store)
    {
        var shared = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (store is null) return shared;

        IReadOnlyList<string> accounts;
        try
        {
            accounts = HarnessSettings.Profiles(home, owner);
        }
        catch (DriverException)
        {
            return shared;
        }

        var prints = accounts
            .Select(account => (Account: account, Print: store.Fingerprint(HarnessSettings.ProfileHome(home, owner, account))))
            .Where(each => each.Print is not null)
            .ToList();
        foreach (var each in prints)
        {
            var others = prints.Where(other => !string.Equals(other.Account, each.Account, StringComparison.OrdinalIgnoreCase) && other.Print!.Same(each.Print!))
                .Select(other => other.Account).ToList();
            if (others.Count > 0) shared[each.Account] = others;
        }

        return shared;
    }
}
