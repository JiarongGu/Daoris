namespace Daoris.Driver;

/// <summary>
/// How a setting-up press ended: done, whether it changed anything, or refused as data with a code the page words in both
/// languages and a sentence for a log. A refusal changes nothing.
/// </summary>
/// <param name="Done">Whether the press did what it is asked, or found it already so.</param>
/// <param name="Changed">Whether the account's environment is different now.</param>
/// <param name="Why">The refusal's code; null when done.</param>
/// <param name="Message">The refusal's sentence, in English; null when done.</param>
public sealed record SetupResult(bool Done, bool Changed, string? Why, string? Message)
{
    /// <summary>Done, having changed the environment or found it already as asked.</summary>
    public static SetupResult Did(bool changed) => new(true, changed, null, null);

    /// <summary>Refused, nothing changed.</summary>
    public static SetupResult Refused(string why, string message) => new(false, false, why, message);
}
