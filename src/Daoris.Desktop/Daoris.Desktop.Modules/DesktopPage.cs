namespace Daoris.Desktop;

/// <summary>
/// Where the shell's page lives on the Chromium it ships, and what each window opens (D92, CHR2).
/// </summary>
/// <remarks>
/// <para>A <c>ChromiumView</c> serves the platform bundle from a folder at <c>https://{VirtualHost}/</c>,
/// so the page is on that origin, and it reaches its host at the loopback address the shell puts in
/// the page's own URL (<c>?host=</c>): the page takes only a loopback address, and only inside the
/// shell (<c>Daoris.Web/src/host.ts</c>).</para>
///
/// <para>A twin (<c>.claude/knowledge/twins.md</c>): the service's <c>DesktopPage.Origin</c> spells the same
/// host, and a local host allows exactly that origin. Duplicated on purpose, never shared; the family
/// rehearsal asks the host with it.</para>
/// </remarks>
public static class DesktopPage
{
    /// <summary>The engine's host name for the app's own origin. <c>.localhost</c> is reserved for loopback.</summary>
    public const string VirtualHost = "daoris.localhost";

    /// <summary>The query parameter the page reads its host's address from.</summary>
    public const string HostParameter = "host";

    /// <summary>
    /// The page a window opens, relative to the app's origin: the platform, told where its host is — and,
    /// for a secondary window, which window it is (<see cref="SecondaryWindow.Parameter"/>).
    /// </summary>
    public static string PathFor(string serviceUrl, string? window = null)
    {
        var host = $"{HostParameter}={Uri.EscapeDataString(serviceUrl.TrimEnd('/'))}";
        return window is null
            ? $"/?{host}"
            : $"/?{SecondaryWindow.Parameter}={Uri.EscapeDataString(window)}&{host}";
    }

    /// <summary>
    /// The bundle a host serves: the first <c>wwwroot</c> holding a page, in the directory it runs from,
    /// then beside its executable and a few folders above it.
    /// </summary>
    /// <remarks>
    /// The host's own rule is its working directory, then its binary's directory — and in development
    /// ASP.NET serves the PROJECT's <c>wwwroot</c> even to a host started from <c>bin/</c>, through its static
    /// assets manifest. The walk up covers that: a workspace build's project is three folders above its
    /// binary, and four above a build for a runtime identifier, which is what a publish leaves in
    /// <c>bin/</c>. Found by looking, twice: the first rule named <c>bin</c>'s empty folder, and then a
    /// runtime build one folder deeper than the walk went; the window said so both times. Where none
    /// holds a page, the working directory's is named, for the window's sentence.
    /// </remarks>
    public static string BundleOf(Daoris.Driver.HostLocation host)
    {
        var named = Path.Combine(host.WorkingDirectory, "wwwroot");
        if (File.Exists(Path.Combine(named, "index.html"))) return named;

        var directory = Path.GetDirectoryName(Path.GetFullPath(host.Executable)) is { } beside ? new DirectoryInfo(beside) : null;
        for (var up = 0; directory is not null && up <= 4; up++, directory = directory.Parent)
        {
            var bundle = Path.Combine(directory.FullName, "wwwroot");
            if (File.Exists(Path.Combine(bundle, "index.html"))) return bundle;
        }

        return named;
    }
}
