using Daoris.Desktop.Driver.Tests;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Every console program the modules, the application and the launcher start, and their tests, hides its window: a
/// parent with no console of its own (the merge queue's detached runner) gives each one a visible window on the owner's screen
/// otherwise. The scan and its named exemptions are the driver tests' <c>NoConsoleStarts</c>, compiled in as a link; the launcher and the
/// update launcher open a window on purpose and are named there with their reasons.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ProcessStartsCensusTests
{
    [Fact]
    public void Every_console_start_hides_its_window_but_the_ones_named_with_their_reason()
    {
        var sources = NoConsoleStarts.Read(NoConsoleStarts.Src(), "Daoris.Desktop/Daoris.Desktop.Modules", "Daoris.Desktop/Daoris.Desktop.App",
            "Daoris.Desktop/Daoris.Desktop.Launcher", "Daoris.Desktop/Daoris.Desktop.Modules.Tests").ToList();
        var (problems, starts) = NoConsoleStarts.Scan(sources);

        Assert.True(starts >= 2, $"expected to find the projects' starts, found {starts}");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        Assert.Empty(NoConsoleStarts.Stale(sources));
    }

    /// <summary>Every direct start a modules test makes is git past the ceiling or named with its purpose in <see cref="TestProcessStarts"/>.</summary>
    [Fact]
    public void Every_direct_start_a_modules_test_makes_is_named_with_its_purpose()
    {
        var problems = TestProcessStarts.Problems(TestProcessStarts.Sources("Daoris.Desktop.Modules.Tests"), "Daoris.Desktop.Modules.Tests");

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }
}
