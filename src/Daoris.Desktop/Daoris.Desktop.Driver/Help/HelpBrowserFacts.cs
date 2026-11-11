namespace Daoris.Driver;

/// <summary>
/// What Daoris's browser's two files hold, as the Browser screen reads them: the settings and the favorites, why either could not be
/// read, and whether an Edge is installed. The room tells the machine's browser from it, so a press names a favorite that is kept.
/// </summary>
/// <param name="Browser">`daoris` or `edge`.</param>
/// <param name="Links">`system` or `daoris`.</param>
/// <param name="Extensions">`offer` or `refuse`.</param>
/// <param name="Favorites">The pages kept, each as the route's reader writes it.</param>
public sealed record HelpBrowserFacts(string Browser, string Links, string Extensions, IReadOnlyList<string> Favorites)
{
    /// <summary>Why the settings file could not be read, in the reader's words; null when it could.</summary>
    public string? Problem { get; init; }

    /// <summary>Why the favorites file could not be read; null when it could.</summary>
    public string? FavoritesProblem { get; init; }

    /// <summary>Whether an Edge is installed here, which the screen says before Edge is chosen.</summary>
    public bool EdgeFound { get; init; } = true;
}
