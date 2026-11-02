using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>git-use</c> door (GITAUTH1e, D198 point 15; the git-auth design §9): a repository's remote or a workspace's host pointed
/// at an identity that exists, or taken back; never the person's own sign-ins, never an identity made up, never one for another host.
/// </summary>
public sealed class HelpGitUseProposalsTests : HelpProposalsFixture
{
    private static readonly HelpMachineFacts WithIdentities = Facts with
    {
        GitIdentities =
        [
            new HelpGitIdentity("a1", "Work", "github.com", "ssh"),
            new HelpGitIdentity("b2", "Org A deploy", "dev.azure.com", "https"),
        ],
    };

    [Theory]
    [InlineData("engine", null, "Work", "daoris-driver git use engine Work", "Use git identity `Work` for origin of `engine`")]
    [InlineData("engine", null, "--remote upstream work", "daoris-driver git use engine --remote upstream Work", "for upstream of `engine`")]
    [InlineData("engine", null, "--clear", "daoris-driver git use engine --clear", "Take back the git identity chosen for origin of `engine`")]
    [InlineData(null, "work", "--host github.com Work", "daoris-driver git use --workspace work --host github.com Work", "for github.com in workspace `work`")]
    [InlineData(null, "work", "--host github.com --clear", "daoris-driver git use --workspace work --host github.com --clear", "Take back workspace `work`'s choice")]
    public void A_pointer_is_planned_as_what_it_changes_and_the_command_that_does_the_same(string? target, string? workspace, string value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting("git-use", target, workspace, value), DriverConfig.Empty, WithIdentities);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        Assert.NotNull(plan.GitUse);
    }

    [Theory]
    [InlineData("engine", null, "own", "never chooses the person's own git sign-ins")]
    [InlineData("engine", null, "Nobody", "there is no git identity `Nobody`")]
    [InlineData("engine", null, "", "`git-use` takes an identity's label")]
    [InlineData("engine", null, "Work --clear", "`git-use` takes an identity's label")]
    [InlineData(null, "work", "Work", "add `--host <host>`")]
    [InlineData(null, "work", "--host dev.azure.com Work", "is for github.com, not dev.azure.com")]
    [InlineData(null, null, "Work", "name exactly one")]
    [InlineData("engine", "work", "--host github.com Work", "name exactly one")]
    [InlineData("elsewhere", null, "Work", "is not registered on this machine")]
    public void A_pointer_that_is_not_one_is_refused_in_words_and_nothing_is_planned(string? target, string? workspace, string value, string says)
    {
        var plan = HelpProposals.Plan(Setting("git-use", target, workspace, value), DriverConfig.Empty, WithIdentities);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.GitUse);
    }

    [Fact]
    public async Task A_pointer_is_applied_through_the_pointers_own_write_and_not_the_drivers_file()
    {
        var (setting, doors, _) = await ApplyAsync(Setting("git-use", "engine", null, "Work"), facts: WithIdentities);

        Assert.True(setting.Applied);
        Assert.Equal(["git-use"], doors.Calls);
        Assert.Equal(new HelpGitUse("repository", "engine", null, "origin", null, "a1"), Assert.Single(doors.GitUses));
    }
}
