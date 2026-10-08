/// <summary>
/// Where the desktop's own page lives when the shell renders it on the Chromium it ships (D92).
/// </summary>
/// <remarks>
/// <para>A <c>ChromiumView</c> serves the platform bundle from a folder at <c>https://{VirtualHost}/</c>, so
/// the page is on that origin and reaches this host at its loopback address, cross-origin. A LOCAL host
/// allows exactly this origin (<see cref="Daoris.Knowledge.Http.BrowserOrigins.Allowed"/>: the CORS policy
/// and the write gate, ORIGIN1). No website can present it: only the shell's engine serves that host name.</para>
///
/// <para>A twin (<c>.claude/knowledge/twins.md</c>): the shell's <c>DesktopPage.VirtualHost</c> spells the
/// same name, duplicated on purpose rather than shared, and the family rehearsal holds the pair
/// together by asking this host with that origin.</para>
/// </remarks>
public static class DesktopPage
{
    /// <summary>The engine's host name for the app's own origin. <c>.localhost</c> is reserved for loopback.</summary>
    public const string VirtualHost = "daoris.localhost";

    /// <summary>The page's origin, as a browser sends it.</summary>
    public const string Origin = "https://" + VirtualHost;
}
