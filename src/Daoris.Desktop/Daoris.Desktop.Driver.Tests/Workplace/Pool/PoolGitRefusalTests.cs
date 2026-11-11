using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A claim's git answers a refusal of the scheduler as git's failure, never as an exception out of the act engine.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PoolGitRefusalTests
{
    [Fact]
    public async Task A_start_the_scheduler_refuses_is_the_exit_minus_one_with_its_reason()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = await PoolGitFixture.CreateAsync();
        using var git = PoolGit.Create(fixture.Home, PoolGit.NewJobName())!;

        var (code, _, said) = await git.GitAsync(fixture.Root, ["no-such-command"]);

        Assert.Equal(-1, code);
        Assert.Contains("no row", said);
    }
}
