using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's <c>system_plan</c> is judged by the plugins' planning, installed when the driver loads, so Course names no plugin type;
/// and a watch over processes says so where none is installed.
/// </summary>
[Collection("agent operation ports")]
public sealed class CourseSystemsTests
{
    [Fact]
    public void The_installed_planner_is_the_plugins_and_a_watch_over_processes_requires_one()
    {
        Assert.IsType<CourseSystemsOperations>(CourseSystems.Current);
        Assert.Equal(typeof(SystemPlanning), CourseSystems.Current.Plan("a home", () => null).Target!.GetType());

        var actual = CourseSystems.Current;
        try
        {
            CourseSystems.Current = null!;
            Assert.Contains("Plugins installs them", Assert.Throws<InvalidOperationException>(() => CourseSystems.Current).Message);
            var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "course-systems", Guid.NewGuid().ToString("N")[..8]);
            Assert.Throws<InvalidOperationException>(() => new SessionRequestWatch(home, new SessionProcesses(), () => null));
        }
        finally { CourseSystems.Current = actual; }
    }
}
