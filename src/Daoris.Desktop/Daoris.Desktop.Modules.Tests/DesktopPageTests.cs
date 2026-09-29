namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// D92: on the Chromium the shell ships, each window's page is a path on the engine's app origin, told
/// where its host is — the address the page's <c>host.ts</c> accepts only when it is loopback.
/// </summary>
public sealed class DesktopPageTests
{
    [Fact]
    public void The_main_window_opens_the_platform_told_where_its_host_is()
    {
        Assert.Equal("/?host=http%3A%2F%2Flocalhost%3A5177", DesktopPage.PathFor("http://localhost:5177/"));
    }

    [Fact]
    public void A_secondary_window_opens_its_route_the_same_way()
    {
        Assert.Equal(
            "/?window=session%3Aa1b2c3d4&host=http%3A%2F%2F127.0.0.1%3A5188",
            DesktopPage.PathFor("http://127.0.0.1:5188", SecondaryWindow.ForSession("a1b2c3d4")));
    }

    /// <summary>The page's origin, spelled as the service's twin spells it (a local host allows exactly it).</summary>
    [Fact]
    public void The_app_origin_is_the_one_the_service_allows()
    {
        Assert.Equal("daoris.localhost", DesktopPage.VirtualHost);
    }

    [Fact]
    public void The_bundle_is_the_first_wwwroot_holding_a_page()
    {
        var root = Path.Combine(Path.GetTempPath(), "daoris-bundle-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // A workspace build: the host runs from bin, and its page is the project's, three folders up.
            var project = Path.Combine(root, "project");
            var bin = Path.Combine(project, "bin", "Debug", "net10.0");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(Path.Combine(project, "wwwroot"));
            File.WriteAllText(Path.Combine(project, "wwwroot", "index.html"), "<html></html>");
            var dev = new Daoris.Driver.HostLocation(Path.Combine(bin, "host.exe"), bin);
            Assert.Equal(Path.Combine(project, "wwwroot"), DesktopPage.BundleOf(dev));

            // A build for a runtime (what a publish leaves behind) sits one folder deeper, and still finds it.
            var rid = Path.Combine(project, "bin", "Release", "net10.0", "win-x64");
            Directory.CreateDirectory(rid);
            Assert.Equal(Path.Combine(project, "wwwroot"), DesktopPage.BundleOf(new(Path.Combine(rid, "host.exe"), rid)));

            // An install: the page beside the host wins.
            Directory.CreateDirectory(Path.Combine(bin, "wwwroot"));
            File.WriteAllText(Path.Combine(bin, "wwwroot", "index.html"), "<html></html>");
            Assert.Equal(Path.Combine(bin, "wwwroot"), DesktopPage.BundleOf(dev));

            // None holds a page: the working directory's is named, for the window's sentence.
            var bare = Path.Combine(root, "bare");
            Directory.CreateDirectory(bare);
            Assert.Equal(Path.Combine(bare, "wwwroot"), DesktopPage.BundleOf(new(Path.Combine(bare, "host.exe"), bare)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
