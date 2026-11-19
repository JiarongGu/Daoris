namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The test host leaves out of its own environment, and so of every child a test starts, what a Daoris session's shell is wired with
/// (the git door's variables above all): a run inside a session reads as one outside it.
/// </summary>
/// <remarks>It plants variables in the process's environment, which every class beside it reads, so it runs alone.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Collection(Alone.Name)]
public sealed class HostEnvironmentTests
{
    private static readonly string[] Planted =
        ["GIT_SSH_VARIANT", "GIT_SSH_COMMAND", "GIT_SSH", "GIT_TRACE", "GIT_ASKPASS", "GIT_AUTHOR_NAME", "SSH_ASKPASS", "GCM_INTERACTIVE"];

    [Fact]
    public async Task A_git_a_fixture_starts_does_not_inherit_the_wiring_a_session_hands_its_shell()
    {
        var was = Planted.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in Planted) Environment.SetEnvironmentVariable(name, "stand-in");

            HostEnvironment.Strip();
            var seen = await GitFixture.RunAsync(GitSeed.RepoRoot(), "-c", "alias.seen=!env", "seen");

            Assert.Equal(0, seen.ExitCode);
            foreach (var name in Planted) Assert.DoesNotContain(name + "=", seen.Stdout);
        }
        finally
        {
            foreach (var (name, value) in was) Environment.SetEnvironmentVariable(name, value);
        }
    }
}
