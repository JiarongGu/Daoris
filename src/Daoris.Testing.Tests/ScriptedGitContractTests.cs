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

    private static GitAsk Ask(params string[] arguments) => new("somewhere", arguments, GitWait.Read, false);

    [Fact]
    public async Task An_ask_no_row_answers_is_a_failure_naming_it()
    {
        var git = new ScriptedGit().On("--version", new GitAnswer(0, "git version 2\n", ""));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => git.RunAsync(Ask("status"), CancellationToken.None));

        Assert.Contains("git status", failure.Message);
        Assert.Single(git.Asked);
    }

    [Fact]
    public async Task An_ask_no_row_answers_goes_beyond_and_every_ask_is_seen()
    {
        var seen = new List<string>();
        var beneath = new ScriptedGit().On("status", new GitAnswer(0, "clean\n", ""));
        var git = new ScriptedGit()
            .On("--version", new GitAnswer(0, "git version 2\n", ""))
            .Seen(ask => seen.Add(string.Join(' ', ask.Arguments)))
            .Beyond(beneath);

        var scripted = await git.RunAsync(Ask("--version"), CancellationToken.None);
        var passed = await git.RunAsync(Ask("status"), CancellationToken.None);

        Assert.Equal("git version 2\n", scripted.Output);
        Assert.Equal("clean\n", passed.Output);
        Assert.Equal(["--version", "status"], seen);
        Assert.Single(beneath.Asked);
    }

    [Fact]
    public async Task A_flow_that_chose_its_git_reaches_it_and_every_other_reaches_the_roots()
    {
        var root = new ScriptedGit().On(_ => true, _ => new GitAnswer(0, "root", ""));
        var own = new ScriptedGit().On(_ => true, _ => new GitAnswer(0, "own", ""));
        var flow = new FlowGit(root);

        string chosen;
        using (FlowGit.Use(own)) chosen = (await flow.RunAsync(Ask("x"), CancellationToken.None)).Output;
        var after = (await flow.RunAsync(Ask("x"), CancellationToken.None)).Output;
        var beside = await Task.Run(async () => (await flow.RunAsync(Ask("x"), CancellationToken.None)).Output);

        Assert.Equal("own", chosen);
        Assert.Equal("root", after);
        Assert.Equal("root", beside);
    }
}
