using Microsoft.AspNetCore.Builder;

namespace Daoris.Knowledge.Http;

/// <summary>
/// Where the HTTP host reads its settings from and what it watches (HTTPWATCH1).
/// </summary>
/// <remarks>
/// The web host's defaults watch <c>appsettings.json</c> in the content root, and the framework's watcher covers the whole
/// folder below it and walks every folder renamed there. The host's folder is the install's <c>app/</c> when the desktop
/// hosts it, which an update swaps, and the project folder, with <c>bin/</c> and <c>obj/</c>, in development; a host that
/// watched either held the folders other work had to move (the fix log's KNOWWALK1 found it in the connector). No file of the
/// host's is watched: a settings change is read at the next start.
/// </remarks>
public static class HostRoot
{
    /// <summary>
    /// The folder the page's <c>wwwroot</c> is served from: the binary's own when it carries the bundle, else the working
    /// folder when that does (development, where the SDK serves the project's), else the binary's.
    /// </summary>
    public static string ContentRoot(string workingFolder, string binaryFolder) =>
        Directory.Exists(Path.Combine(binaryFolder, "wwwroot")) ? binaryFolder
        : Directory.Exists(Path.Combine(workingFolder, "wwwroot")) ? workingFolder
        : binaryFolder;

    /// <summary>The host's builder with that content root and no configuration source reloading on change.</summary>
    /// <remarks>
    /// The web builder takes no configuration of its own to seed, so the switch travels as the first command-line argument,
    /// which the host's defaults read before they add the settings files; the process's own arguments follow it.
    /// </remarks>
    public static WebApplicationBuilder Builder(string[] args) =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--hostBuilder:reloadConfigOnChange=false", .. args],
            ContentRootPath = ContentRoot(Directory.GetCurrentDirectory(), AppContext.BaseDirectory),
        });
}
