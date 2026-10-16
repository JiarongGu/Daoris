using Daoris.Driver;

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

/// <summary>How a module opens the file manager: through the start gate (PROCSTART2), never by calling the launcher itself.</summary>
public static class OpenFolders
{
    /// <summary>
    /// Open <paramref name="folder"/> once the start gate has a place, on the thread pool (PROCSTART2): the launcher starts
    /// the file manager's process, and a route's call to it before its first wait was a start on the window's thread. Its
    /// refusal is thrown as the launcher threw it.
    /// </summary>
    public static Task OpenAsync(this OpenFolder open, string folder, CancellationToken ct) =>
        ProcessStarts.Shared.RunAsync(() => open(folder), ct);
}
