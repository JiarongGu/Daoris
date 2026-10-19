namespace Daoris.Desktop;

/// <summary>
/// The main window wearing how much waits on the person (NOTIFY1b): its taskbar button's overlay and its title, told by
/// its page over <c>SET_BADGE</c>. <paramref name="count"/> is never below none; <paramref name="description"/> is what a
/// screen reader says of the overlay, in the page's language, or null.
/// </summary>
/// <remarks>
/// A delegate for the reason <see cref="PickFile"/> is one: these modules are plain <c>net10.0</c> and tested as such, and
/// the application hands in its window. Resolved at call time there, since the window is made after the modules are.
/// </remarks>
public delegate void ShowWaiting(int count, string? description);

/// <summary>
/// A main window's page telling its frame the workspace it holds (MAINWINDOWS3b), for the title: <paramref name="name"/> is the
/// window's, or null for the first main window; <paramref name="workspace"/> is null where the page names none.
/// </summary>
public delegate void ShowWorkspace(string? name, string? workspace);

/// <summary>
/// What the main window's taskbar button and title say of a count (NOTIFY1b): the judgement, here where it is tested,
/// while the window draws the overlay and sets the title.
/// </summary>
/// <remarks>
/// The title is the taskbar's hover and Alt-Tab's label, since the window is frameless and shows no caption. A count
/// leading the name is the convention a mail or chat window keeps there, and needs no words in either language.
/// </remarks>
public static class WaitingBadge
{
    /// <summary>The window's name with nothing waiting.</summary>
    public const string Name = "Daoris (道衍)";

    /// <summary>
    /// The overlay's text, or null for no overlay. A small icon holds one digit and a sign, so past nine it says
    /// <c>9+</c>.
    /// </summary>
    public static string? Label(int count) => count switch
    {
        <= 0 => null,
        <= 9 => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "9+",
    };

    /// <summary>
    /// The window's title: the count before the name while anything waits. Past ninety-nine it says <c>99+</c>, as the
    /// page's own badge does (<c>design/ui.tsx</c>).
    /// </summary>
    public static string Title(int count, string? workspace = null)
    {
        var named = string.IsNullOrWhiteSpace(workspace) ? Name : $"{Name}{Separator}{workspace}";
        return count switch
        {
            <= 0 => named,
            <= 99 => $"({count.ToString(System.Globalization.CultureInfo.InvariantCulture)}) {named}",
            _ => $"(99+) {named}",
        };
    }

    /// <summary>What the page's title puts between the application's name and the workspace (<c>windowTitle</c>, the page's).</summary>
    private const string Separator = " — ";

    /// <summary>
    /// The workspace a window's page names in its title (MAINWINDOWS3b): the part after the separator, taken only from a title
    /// that starts with <see cref="Name"/> and the separator, so the frame's name stays its own and no other page text reaches
    /// the taskbar. Null when the page names none.
    /// </summary>
    public static string? Workspace(string? pageTitle)
    {
        if (pageTitle is null || !pageTitle.StartsWith(Name + Separator, StringComparison.Ordinal)) return null;
        var workspace = pageTitle[(Name.Length + Separator.Length)..].Trim();
        return workspace.Length == 0 ? null : workspace;
    }
}
