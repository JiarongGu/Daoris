namespace Daoris.Driver;

/// <summary>
/// *Put on my PATH* and *Take off my PATH* (WIRING1e, the this-Daoris design §4): the install's <c>app/bin</c> added to, or removed
/// from, <b>the account's</b> <c>PATH</c>, never the machine's, so a terminal opened afterwards finds <c>daoris</c> and
/// <c>daoris-driver</c>. Daoris never does it by itself: it is the person's press, said first and undone by the next.
/// </summary>
/// <remarks>
/// The entry is appended, the rest of the value kept as written (its <c>%NAME%</c> references and order), and an off removes exactly the
/// entries that name the folder an on would add. A value that cannot be read is never written over.
/// </remarks>
public static class AccountPath
{
    private const string Variable = "PATH";

    /// <summary>The install's folder of launchers: what an on adds; null for a development run, which has none.</summary>
    public static string? BinOf(string? install)
    {
        if (install is null) return null;
        var bin = Path.Combine(install, "app", "bin");
        return Directory.Exists(bin) ? bin : null;
    }

    /// <param name="install">The install this Daoris runs from, or null in a development run.</param>
    /// <param name="account">The account's variables; null where the machine keeps none (off Windows).</param>
    /// <param name="on">Put the folder on the account's <c>PATH</c>, or take it off.</param>
    public static SetupResult Set(string? install, IAccountEnvironment? account, bool on)
    {
        if (account is null) return SetupResult.Refused("PATH_NOT_WINDOWS", "This machine keeps no account PATH to change: only Windows does.");
        if (BinOf(install) is not { } bin)
        {
            return SetupResult.Refused("PATH_NOT_INSTALL", "A development run has no launchers to put on a PATH: only an installed Daoris does.");
        }

        string? stored;
        try
        {
            stored = account.Read(Variable);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return SetupResult.Refused("PATH_UNREADABLE", "Your account's PATH could not be read, so nothing was written.");
        }

        var entries = (stored ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var present = entries.Where(entry => Names(entry, bin)).ToArray();
        if (on == present.Length > 0) return SetupResult.Did(changed: false);

        var next = on ? [.. entries, bin] : entries.Where(entry => !Names(entry, bin)).ToArray();
        try
        {
            account.Write(Variable, next.Length == 0 ? null : string.Join(Path.PathSeparator, next));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return SetupResult.Refused("PATH_NOT_WRITTEN", "Your account's PATH could not be written, so it is as it was.");
        }

        return SetupResult.Did(changed: true);
    }

    /// <summary>Whether a stored entry, its references expanded and its quotes taken off, is the folder.</summary>
    private static bool Names(string entry, string bin) =>
        SystemWiring.SamePath(Environment.ExpandEnvironmentVariables(entry.Trim('"')), bin);
}
