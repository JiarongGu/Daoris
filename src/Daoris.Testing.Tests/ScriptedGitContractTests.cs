using Daoris.Ports.Git;

namespace Daoris.Testing.Tests;

public sealed class ScriptedGitContractTests : GitContract
{
    protected override string Folder => "unused";

    protected override IGit Backend(params Script[] script)
    {
        var git = new ScriptedGit();
        foreach (var row in script) git.On(row.Arguments, row.Answer);
        return git;
    }

    [Fact]
    public async Task An_ask_no_row_answers_is_a_failure_naming_it()
    {
        var git = new ScriptedGit().On("--version", new GitAnswer(0, "git version 2\n", ""));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => git.RunAsync(new GitAsk("somewhere", ["status"], GitWait.Read, false), CancellationToken.None));

        Assert.Contains("git status", failure.Message);
        Assert.Single(git.Asked);
    }
}
