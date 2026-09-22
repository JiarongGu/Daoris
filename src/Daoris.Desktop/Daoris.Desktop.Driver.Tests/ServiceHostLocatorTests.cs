using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shell's first question — where is the HTTP host — answered in an order a person can predict:
/// what they said, then the installed home, then the workspace build. A wrong order here starts a
/// stale binary while the person stares at a fresh one.
/// </summary>
public sealed class ServiceHostLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-locator-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void What_the_person_said_comes_first()
    {
        var candidates = ServiceHostLocator.Candidates("D:/somewhere/host.exe", "/profile", _root);

        Assert.Equal("D:/somewhere/host.exe", candidates[0].Executable);
    }

    /// <summary>
    /// Then what the install carries, then the machine's installed home — and the home runs beside
    /// its bundle. Asserted as an ORDER, because the order is the contract: the second deployment
    /// found the machine's older host outranking the one `desktop-publish --service` had just put
    /// beside the shell, and the window showed the previous page with nothing failed.
    /// </summary>
    [Fact]
    public void What_the_install_carries_comes_next_and_then_the_installed_home_beside_its_bundle()
    {
        var profile = Path.Combine(_root, "profile");
        var candidates = ServiceHostLocator.Candidates(null, profile, _root);

        var home = Path.Combine(profile, ".daoris", "bin");
        Assert.Equal(
            [
                Path.Combine(_root, "daoris-knowledge-http", ServiceHostLocator.ExecutableName),
                Path.Combine(_root, "app", "daoris-knowledge-http", ServiceHostLocator.ExecutableName),
                Path.Combine(home, ServiceHostLocator.ExecutableName),
                Path.Combine(home, "daoris-knowledge-http", ServiceHostLocator.ExecutableName),
            ],
            candidates.Select(c => c.Executable).Take(4));
        Assert.Equal(home, candidates[2].WorkingDirectory);
    }

    /// <summary>
    /// 🔴 <b>A deployed shell runs the host it was published with, even when the machine has one of
    /// its own.</b> The previous order ranked `~/.daoris/bin` above the install's copy so that a
    /// service upgrade would reach every shell — and the second deployment showed the inverse:
    /// `desktop-publish --service` put a NEWER host beside the shell, the shell spawned the OLDER
    /// machine-wide one, and the window served a bundle that no longer existed on disk anywhere but
    /// there. The install is self-sufficient (the deployment gate's own phase 3 says so), and what it
    /// carries is what it runs; a shell published without `--service` still falls through to the
    /// machine's home next.
    /// </summary>
    [Fact]
    public void A_deployed_shell_prefers_the_host_published_with_it_over_the_machines_installed_home()
    {
        var profile = Path.Combine(_root, "profile");
        var installedHome = Path.Combine(profile, ".daoris", "bin", "daoris-knowledge-http");
        Directory.CreateDirectory(installedHome);
        File.WriteAllText(Path.Combine(installedHome, ServiceHostLocator.ExecutableName), "");

        var app = Path.Combine(_root, "install");
        var beside = Path.Combine(app, "app", "daoris-knowledge-http");
        Directory.CreateDirectory(beside);
        var own = Path.Combine(beside, ServiceHostLocator.ExecutableName);
        File.WriteAllText(own, "");

        var found = ServiceHostLocator.Locate(null, profile, app);

        Assert.NotNull(found);
        Assert.Equal(own, found.Executable);
        Assert.Equal(beside, found.WorkingDirectory);
    }

    /// <summary>
    /// 🔴 <b>Where the installer actually puts it.</b> `service-publish --install` lands the HTTP
    /// host in a directory of its own — `bin/daoris-knowledge-http/daoris-knowledge-http.exe` —
    /// because its <c>wwwroot</c> has to travel beside the executable, while the MCP host installs
    /// flat beside it. The locator only ever looked flat, so a correctly installed host was invisible
    /// and the shell fell through to the workspace build.
    /// </summary>
    /// <remarks>
    /// On a developer machine that fallback exists, which is exactly why this survived: the shell
    /// found *a* host every time and nobody saw which one. On a deployed machine there is no
    /// workspace to fall through to, and the shell reports no host at all. Found by deploying.
    /// </remarks>
    [Fact]
    public void The_installed_host_is_found_where_the_installer_puts_it()
    {
        var profile = Path.Combine(_root, "profile");
        var nested = Path.Combine(profile, ".daoris", "bin", "daoris-knowledge-http");
        Directory.CreateDirectory(nested);
        var executable = Path.Combine(nested, ServiceHostLocator.ExecutableName);
        File.WriteAllText(executable, "");

        var found = ServiceHostLocator.Locate(null, profile, _root);

        Assert.NotNull(found);
        Assert.Equal(executable, found.Executable);
        // Beside its own bundle, for the reason the dev candidate runs from its project directory:
        // a host started elsewhere answers every API call while serving no page.
        Assert.Equal(nested, found.WorkingDirectory);
    }

    /// <summary>
    /// 🔴 <b>A deployed shell carries its own host beside it.</b> `desktop-publish --service` puts
    /// one there so the install folder is self-sufficient — and an install folder has no workspace
    /// above it to fall through to, so if this candidate did not exist the flag would be a claim
    /// nothing reads.
    /// </summary>
    /// <remarks>
    /// It ranked BELOW the installed home once, so that a service upgrade would reach every shell
    /// on the machine — see the test above for what that cost, and why it is the other way now.
    /// </remarks>
    [Theory]
    // Beside the executable — the simple case, and what a hand-assembled folder looks like.
    [InlineData("daoris-knowledge-http")]
    // 🔴 Under `app/` — where `desktop-publish --service` actually puts it, because an install folder
    // shows ONE launcher at its root and keeps its supporting binaries out of sight. The layout and
    // the locator are a counterpart set: tidying the folder without this candidate would have made
    // `--service` publish a host nothing looks for.
    [InlineData("app/daoris-knowledge-http")]
    public void A_deployed_shell_finds_the_host_published_with_it(string relative)
    {
        var app = Path.Combine(_root, "install");
        var beside = Path.Combine(app, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(beside);
        var executable = Path.Combine(beside, ServiceHostLocator.ExecutableName);
        File.WriteAllText(executable, "");

        // An empty profile: nothing installed machine-wide, which is the deployed case.
        var found = ServiceHostLocator.Locate(null, Path.Combine(_root, "empty-profile"), app);

        Assert.NotNull(found);
        Assert.Equal(executable, found.Executable);
        Assert.Equal(beside, found.WorkingDirectory);
    }

    /// <summary>
    /// A flat executable someone placed by hand still wins over the nested one — it is the more
    /// deliberate of the two, and the order has to be predictable either way.
    /// </summary>
    [Fact]
    public void A_hand_placed_flat_binary_still_comes_first()
    {
        var profile = Path.Combine(_root, "profile");
        var bin = Path.Combine(profile, ".daoris", "bin");
        Directory.CreateDirectory(Path.Combine(bin, "daoris-knowledge-http"));
        File.WriteAllText(Path.Combine(bin, "daoris-knowledge-http", ServiceHostLocator.ExecutableName), "");
        var flat = Path.Combine(bin, ServiceHostLocator.ExecutableName);
        File.WriteAllText(flat, "");

        Assert.Equal(flat, ServiceHostLocator.Locate(null, profile, _root)!.Executable);
    }

    /// <summary>
    /// Development: the workspace manifest anchors the walk — and the working directory is the
    /// PROJECT, not the bin: the bundle lives in the project's wwwroot, and a dev host spawned from
    /// its bin answers every API call while serving no page.
    /// </summary>
    [Fact]
    public void The_workspace_build_is_found_and_runs_from_its_project_directory()
    {
        var workspace = Path.Combine(_root, "workspace");
        var deep = Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(workspace, "daoris.json"), "{}");

        var candidates = ServiceHostLocator.Candidates(null, "/profile", deep);
        var project = Path.Combine(workspace, "src", "Daoris.Service", "Daoris.Service.Http");

        var dev = candidates.Single(c =>
            c.Executable == Path.Combine(project, "bin", "Debug", "net10.0", ServiceHostLocator.ExecutableName));
        Assert.Equal(project, dev.WorkingDirectory);
    }

    [Fact]
    public void No_workspace_means_no_dev_candidates_and_no_crash()
    {
        var lonely = Path.Combine(_root, "lonely");
        Directory.CreateDirectory(lonely);

        var candidates = ServiceHostLocator.Candidates(null, "/profile", lonely);

        // 🔴 Asserted by what the candidates ARE, not by counting them. The assertion here used to be
        // `Single`, which said "one" while meaning "nothing from a workspace" — so every later
        // candidate that was not a workspace one (the installer's own layout, then the host beside a
        // deployed shell) read as a regression when it was the fix.
        Assert.DoesNotContain(candidates, c => c.Executable.Contains("Daoris.Service.Http"));
        Assert.NotEmpty(candidates);
    }

    [Fact]
    public void Locate_answers_null_rather_than_throwing_when_nothing_exists()
    {
        var lonely = Path.Combine(_root, "nothing");
        Directory.CreateDirectory(lonely);

        Assert.Null(ServiceHostLocator.Locate(null, Path.Combine(_root, "no-profile"), lonely));
    }
}
