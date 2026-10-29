namespace Daoris.Driver;

/// <summary>
/// The owner's acceptance of a repository's integration declaration (INTEGRATE1d; D215 point 2, the integration design §4.1, §4.3): git's
/// blob id of the <c>daoris.gates.json</c> the person accepted for integration, kept by this machine in <c>driver.json</c>'s
/// <c>integrationGates</c> (<see cref="IntegrationGatesKey"/>), apart from the landed route's (D192), so a repository needs no
/// verification pipeline to integrate. Only the accepted blob's text is read for what runs: a member that changes the declaration
/// integrates under the line's accepted one, and its own blob waits for the owner. Nothing here reads git or acts; the door that
/// accepts and the reader of the line's tip come after (INTEGRATE1d's second part).
/// </summary>
internal static class IntegrationAcceptance
{
    // The four states are the landed route's (LandedGates in Workflows, which this does not name), so a page reads both alike.

    /// <summary>The acceptance names the tip's declaration.</summary>
    public const string Accepted = "accepted";

    /// <summary>The tip's declaration is not the one accepted: integration goes on under the last accepted blob, and the new one waits for the owner.</summary>
    public const string Changed = "changed";

    /// <summary>No declaration has been accepted for integration here yet: nothing it declares runs.</summary>
    public const string Unaccepted = "unaccepted";

    /// <summary>The tip could not be read here: no checkout, or a line that names no commit.</summary>
    public const string Unread = "unread";

    /// <summary>The blob this machine accepted for <paramref name="repository"/>'s integration, its name trimmed and matched in any case; null for none.</summary>
    public static string? Of(DriverConfig config, string repository) =>
        config.IntegrationGates.TryGetValue(repository.Trim(), out var accepted) ? accepted : null;

    /// <summary>
    /// Where an acceptance stands against the line's tip, in the shape of the landed route's: <see cref="Unread"/> where the tip's
    /// blob could not be read (null), whatever was accepted; <see cref="Unaccepted"/> where nothing was; <see cref="Accepted"/> where
    /// the accepted blob is the tip's (<c>none</c> for a line that holds no declaration, accepted as such); else <see cref="Changed"/>.
    /// </summary>
    public static string StateOf(string? accepted, string? tip)
    {
        if (tip is null) return Unread;
        if (accepted is null) return Unaccepted;
        return string.Equals(accepted, tip, StringComparison.Ordinal) ? Accepted : Changed;
    }
}
