namespace Daoris.Desktop;

/// <summary>
/// The system's file picker (TOOLS7, D121 §4.1: a tool's *Browse…*), the other thing a page cannot do for itself beside
/// naming a directory (D48 §7). Answers the file the person chose, or null when they cancelled.
/// </summary>
/// <remarks>
/// A delegate for the reason <see cref="OpenFolder"/> is one: these modules are plain <c>net10.0</c> and tested as such,
/// and the application hands in its window's dialog.
/// </remarks>
/// <param name="title">The dialog's title, in the page's language.</param>
public delegate string? PickFile(string? title);
