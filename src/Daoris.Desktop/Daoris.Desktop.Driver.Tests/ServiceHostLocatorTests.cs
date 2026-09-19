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
        var candidates = ServiceHostLocator.Candidates("D:/somewhere/host.exe", "/home/dev", _root);

        Assert.Equal("D:/somewhere/host.exe", candidates[0].Executable);
    }

    [Fact]
    public void The_installed_home_is_next_and_runs_beside_its_bundle()
    {
        var profile = Path.Combine(_root, "profile");
        var candidates = ServiceHostLocator.Candidates(null, profile, _root);

        var home = Path.Combine(profile, ".daoris", "bin");
        Assert.Equal(Path.Combine(home, ServiceHostLocator.ExecutableName), candidates[0].Executable);
        Assert.Equal(home, candidates[0].WorkingDirectory);
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

        var candidates = ServiceHostLocator.Candidates(null, "/home/dev", deep);
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

        var candidates = ServiceHostLocator.Candidates(null, "/home/dev", lonely);

        Assert.Single(candidates); // the installed home only
    }

    [Fact]
    public void Locate_answers_null_rather_than_throwing_when_nothing_exists()
    {
        var lonely = Path.Combine(_root, "nothing");
        Directory.CreateDirectory(lonely);

        Assert.Null(ServiceHostLocator.Locate(null, Path.Combine(_root, "no-profile"), lonely));
    }
}
