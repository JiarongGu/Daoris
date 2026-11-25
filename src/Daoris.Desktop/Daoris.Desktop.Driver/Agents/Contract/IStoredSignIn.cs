namespace Daoris.Driver;

/// <summary>
/// The shape of an account's stored sign-in, as its agent keeps it in the account's home: whether an access and a refresh token are
/// there, when the access token expires, and when the store is written. <b>Never a token's value</b>: presence alone is read, and
/// nothing of it is kept, logged or shown.
/// </summary>
/// <param name="Access">An access token is there (a non-empty value).</param>
/// <param name="Refresh">A refresh token is there.</param>
/// <param name="ExpiresAt">When the access token expires, where the store says; null where it says none or says zero.</param>
/// <param name="Written">When the store is last written.</param>
public sealed record StoredSignInShape(bool Access, bool Refresh, DateTimeOffset? ExpiresAt, DateTimeOffset Written)
{
    /// <summary>Both tokens gone: the agent cleared the account's sign-in, as it does when a refresh of it is refused.</summary>
    public bool Cleared => !Access && !Refresh;

    /// <summary>Whether the access token is expired, or expires within <paramref name="margin"/> of <paramref name="now"/>.</summary>
    public bool Expiring(DateTimeOffset now, TimeSpan margin) => Refresh && ExpiresAt is { } at && at - now <= margin;
}

/// <summary>
/// A provider's reader of the shape of an account's stored sign-in (<see cref="StoredSignInShape"/>): what lets Daoris renew a
/// sign-in once before several sessions start on the account together, and show at once a sign-in its agent cleared. A provider
/// that keeps its sign-in elsewhere (a keychain) declares none.
/// </summary>
public interface IStoredSignIn
{
    /// <summary>The store's shape in <paramref name="profileHome"/>; null where there is none or it does not read.</summary>
    StoredSignInShape? Read(string profileHome);

    /// <summary>The store's file name in an account's home, which Daoris watches; null where none is declared.</summary>
    string? FileName => null;

    /// <summary>The shape of one file the agent wrote as or beside the store (a temp file); null where it does not read.</summary>
    StoredSignInShape? ReadFile(string path) => null;

    /// <summary>The agent's file-name pattern (<c>*</c> for the rest) for a write's temp files, or null where its writes leave none.</summary>
    string? LeftoverPattern => null;

    /// <summary>A one-way fingerprint of the store's refresh token, made without a text of it; null where there is none.</summary>
    StoredFingerprint? Fingerprint(string profileHome) => null;

    /// <summary>How long a write takes: a temp file younger than this may be in flight, an older one is a leftover.</summary>
    TimeSpan WriteLife => TimeSpan.FromMinutes(5);
}
