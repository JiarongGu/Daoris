namespace Daoris.Driver;

/// <summary>
/// Which Daoris home works a git place in place, for the homes on this machine that cannot see each other's ledgers: a marker in the
/// checkout's common git directory naming the home, written by its first in-place start. Every other home's in-place start there is
/// refused while the home it names still stands; one that is gone gives it up to the next. A folder takes none, and its row says so.
/// </summary>
internal static class PlaceHomeMarker
{
    /// <summary>The marker's name inside the common git directory, which git neither reads nor tracks.</summary>
    public const string Name = "daoris-place-home";

    /// <summary>
    /// Claim <paramref name="root"/>'s checkout for <paramref name="home"/>'s in-place starts, or why not. Never names either home's path:
    /// a refusal is shown wherever its start's hold lands.
    /// </summary>
    /// <returns>Null where the checkout is this home's or now is; the refusal where another home on this machine works it.</returns>
    public static string? Claim(string home, string root, string repository)
    {
        if (RefFiles.CommonDirOf(root) is not { } common) return null;
        var marker = Path.Combine(common, Name);
        var ours = Path.GetFullPath(home);
        try
        {
            if (File.Exists(marker) && AtomicFile.ReadText(marker).Trim() is { Length: > 0 } named
                && !string.Equals(Path.GetFullPath(named), ours, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                && Directory.Exists(named))
            {
                return $"`{repository}`'s checkout is worked in place by another Daoris home on this machine, which this home's ledger cannot see, "
                       + "so no session of this home starts there: one writer at a time. Work it from that home, or give this repository trees of its own.";
            }

            AtomicFile.WriteText(marker, ours + "\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A marker that cannot be read or written guards nothing more than the lease of this home already does.
        }

        return null;
    }
}
