namespace Daoris.Driver;

/// <summary>
/// The key of the watcher's word to a quiet session (SESSWATCH1b, <see cref="QuietWord"/>): pure key vocabulary, which
/// <see cref="DaorisKeys"/> keeps under its own names for the readers above the service.
/// </summary>
public static class WatchKeys
{
    public const string Kind = "watch";

    /// <summary>
    /// The watcher's word to a quest session seen quiet (SESSWATCH1b, the session-watch design §2.1): the session and its quiet episode, so
    /// one episode keeps one word. No quest's fact: <see cref="DaorisKeys.Read"/> names none.
    /// </summary>
    public static string Of(string session, int episode) => $"{Kind}/{session}/{episode.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Whether a word's key is a watcher's word's (<see cref="Of"/>).</summary>
    public static bool Is(string? key) => key?.StartsWith(Kind + "/", StringComparison.Ordinal) == true;
}
