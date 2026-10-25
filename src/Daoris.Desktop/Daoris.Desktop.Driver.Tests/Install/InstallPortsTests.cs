using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports the install declares and the workplace answers (FOUNDATION1 mod-v): the git a child is handed. The workplace installs it when
/// the driver's assembly loads, so a start passes the tools' seam and is handed what it always was; a test may put another in and puts it back.
/// </summary>
public sealed class InstallPortsTests
{
    [Fact]
    public void The_git_wiring_in_force_is_the_workplaces_and_hands_what_the_tools_seam_always_handed()
    {
        Assert.IsType<WorkingTreeGit>(ChildGit.Current);

        var through = new ProcessStartInfo("x");
        Tools.Hand(through, home: null);
        var direct = new ProcessStartInfo("x");
        GitWiring.Hand(direct);

        Assert.Equal(direct.Environment.OrderBy(pair => pair.Key), through.Environment.OrderBy(pair => pair.Key));
        Assert.All(GitWiring.Prompts, pair => Assert.Equal(pair.Value, through.Environment[pair.Key]));
    }

    [Fact]
    public void A_test_may_install_another_git_wiring_and_put_the_workplaces_back()
    {
        var workplaces = ChildGit.Current;
        var told = new List<string?>();
        try
        {
            ChildGit.Current = new Stand(told);
            Tools.Hand(new ProcessStartInfo("x"), home: null);
            Tools.Hand(new ProcessStartInfo("x"), "home-a");
        }
        finally
        {
            ChildGit.Current = workplaces;
        }

        Assert.Equal([null, "home-a"], told);
        Assert.Same(workplaces, ChildGit.Current);
    }

    private sealed class Stand(List<string?> told) : IChildGit
    {
        public void Hand(ProcessStartInfo info, string? home) => told.Add(home);
    }
}
