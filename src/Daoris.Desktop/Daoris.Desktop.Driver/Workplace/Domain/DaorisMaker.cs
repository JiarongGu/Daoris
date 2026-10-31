namespace Daoris.Driver;

/// <summary>
/// The maker line of a commit Daoris itself writes (DAORISMAKER1): the merge tool's commit check refuses a commit with no
/// <c>Co-Authored-By:</c> line naming a maker, and Daoris is the maker of the replay's own commits.
/// </summary>
public static class DaorisMaker
{
    /// <summary>The trailer, as <c>git interpret-trailers</c> reads it.</summary>
    public const string Trailer = "Co-Authored-By: Daoris <noreply@daoris.invalid>";

    /// <summary><paramref name="message"/> with the trailer after a blank line.</summary>
    public static string Of(string message) => $"{message}\n\n{Trailer}";
}
