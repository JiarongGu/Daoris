namespace Daoris.Desktop;

/// <summary>
/// The person's file manager, opened on a folder of Daoris's own (LOG1c): the other thing a page cannot
/// do for itself, beside naming a directory (D48 §7).
/// </summary>
/// <remarks>
/// A delegate rather than the window kit's shell launcher, because these modules are plain
/// <c>net10.0</c> and tested as such; the application hands in the launcher's <c>OpenDirectory</c>. The
/// module that holds one decides WHICH folder: a page never names the path it opens.
/// </remarks>
public delegate void OpenFolder(string folder);
