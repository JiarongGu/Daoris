namespace Daoris.Desktop;

/// <summary>
/// The system's file picker (*Browse…*), which a page cannot do for itself. Answers the file chosen, or null when cancelled.
/// </summary>
/// <remarks>
/// A delegate for the reason <see cref="OpenFolder"/> is one: these modules are plain <c>net10.0</c> and tested as such,
/// and the application hands in the kit's dialog, which is owned by the window whose page asked.
/// </remarks>
/// <param name="kind">The file asked for, which settles its filters (<see cref="PickDialogs"/>).</param>
/// <param name="title">The dialog's title, in the page's language.</param>
public delegate Task<string?> PickFile(PickKind kind, string? title);

/// <summary>
/// The system's folder picker (adding a repository), the one thing a page cannot do for itself beside naming a file.
/// Answers the folder the person chose, or null when they cancelled.
/// </summary>
public delegate Task<string?> PickFolder();
