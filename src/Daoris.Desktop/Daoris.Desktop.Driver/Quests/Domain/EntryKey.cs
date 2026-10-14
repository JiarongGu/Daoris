namespace Daoris.Driver;

/// <summary>
/// An entry's key read apart (SESSONE1h, D167): <c>&lt;origin&gt;/&lt;repository&gt;</c>, split at its first slash, as the service
/// reads a stored key. The one reader the driver has, so <see cref="EntryParts"/> and the session groups agree on a key that is
/// odd: a key with no slash is all origin and has no repository.
/// </summary>
/// <param name="Origin">What comes before the first slash; the whole key where it has none.</param>
/// <param name="Repository">What comes after the first slash; empty where the key has none.</param>
/// <param name="HasSlash">Whether the key held a slash, with something before it, that is, whether it names an origin and a repository.</param>
public readonly record struct EntryKey(string Origin, string Repository, bool HasSlash)
{
    /// <summary>Reads a key; a null or empty key is empty, with no origin.</summary>
    public static EntryKey Parse(string? key)
    {
        if (string.IsNullOrEmpty(key)) return new EntryKey("", "", false);
        var at = key.IndexOf('/');
        return at < 0
            ? new EntryKey(key, "", false)
            : new EntryKey(key[..at], key[(at + 1)..], at > 0);
    }
}
