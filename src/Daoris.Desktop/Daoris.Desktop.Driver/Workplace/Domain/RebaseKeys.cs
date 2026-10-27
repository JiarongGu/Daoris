namespace Daoris.Driver;

/// <summary>
/// The key of the rebase words an automatic landing says to a session whose replay conflicted (LANDREBASE1, <see cref="RebaseTelling"/>):
/// pure key vocabulary, which <see cref="DaorisKeys"/> keeps under its own names for the readers above the workplace.
/// </summary>
public static class RebaseKeys
{
    public const string Kind = "rebase";

    /// <summary>
    /// The rebase words' key (LANDREBASE1): the session an automatic landing refused for a conflict, and the tip that conflicted, so a
    /// look that tries the same tip again keeps one word. No quest's fact: <see cref="DaorisKeys.Read"/> names none, so the word waits as it is.
    /// </summary>
    public static string Of(string session, string tip) => $"{Kind}/{session}/{tip}";

    /// <summary>
    /// The commit a rebase word's key names (<see cref="Of"/>), which the line may hold since (LIMITSWITCH3); null for any other key.
    /// Read from the end, since a session's name may hold a slash (a teammate's).
    /// </summary>
    public static string? TipOf(string? key)
    {
        if (key is null || !key.StartsWith(Kind + "/", StringComparison.Ordinal)) return null;
        var tip = key[(key.LastIndexOf('/') + 1)..];
        return key.IndexOf('/') < key.LastIndexOf('/') && tip.Length > 0 ? tip : null;
    }
}
