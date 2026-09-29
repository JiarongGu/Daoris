namespace Daoris.Desktop;

/// <summary>
/// The platform bundle every window's page is served from (D92): the <c>wwwroot</c> beside the host this
/// shell would start, or null where no host was found. A missing bundle is said in the window.
/// </summary>
public sealed record PlatformBundle(string? Folder)
{
    /// <summary>Whether there is a page to show: the bundle's own <c>index.html</c> exists.</summary>
    public bool Present => Folder is not null && File.Exists(Path.Combine(Folder, "index.html"));
}
