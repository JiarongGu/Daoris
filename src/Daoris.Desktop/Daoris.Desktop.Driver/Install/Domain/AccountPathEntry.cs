namespace Daoris.Driver;

/// <summary>Which entries of an account's <c>PATH</c> are the install's launcher folder (WIRING1e): the comparison, over paths already made whole.</summary>
public static class AccountPathEntry
{
    /// <summary>Whether a stored entry is the folder: both whole paths, or null where an entry is no path, compared by the machine's case rule.</summary>
    public static bool Names(string? entry, string? bin, StringComparison comparison) =>
        !string.IsNullOrWhiteSpace(entry) && !string.IsNullOrWhiteSpace(bin) && string.Equals(entry, bin, comparison);
}
