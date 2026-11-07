using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal verb <c>git push &lt;repository&gt; branch|tag &lt;name&gt; [&lt;commit&gt;] [--plan|--yes]</c>,
/// one ref and never a force or <c>-u</c>; a person's press holding the home's lease only with a yes.
/// </summary>
public sealed class GitPushCommandTests
{
    [Theory]
    [InlineData("push|repo|branch|work/fix", "repo", "branch", "work/fix", null, false)]
    [InlineData("push|repo|tag|v1|abc123|--plan", "repo", "tag", "v1", "abc123", false)]
    [InlineData("push|--yes|repo|branch|main", "repo", "branch", "main", null, true)]
    public void The_words_are_read_into_the_ask(string words, string repository, string kind, string name, string? at, bool yes)
    {
        Assert.Equal(new GitPushVerb(repository, kind, name, at, yes), GitPushCommand.Read(words.Split('|'), out var problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("push")]
    [InlineData("push|repo|main")]
    [InlineData("push|repo|note|main")]
    [InlineData("push|repo|branch|main|--force")]
    [InlineData("push|repo|branch|main|-u")]
    [InlineData("push|repo|branch|main|abc|extra")]
    [InlineData("push|repo|branch|main|--plan|--yes")]
    public void Words_that_are_not_the_verbs_are_refused_with_the_usage(string words)
    {
        Assert.Null(GitPushCommand.Read(words.Split('|'), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_push_holds_the_lease_only_with_a_yes_and_is_a_person_s_press()
    {
        Assert.True(VerbLeases.Holds(["git", "push", "repo", "branch", "main", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "push", "repo", "branch", "main", "--plan"]));
        Assert.Contains(PersonPresses.Presses, press => press.Route == PersonPresses.GitPush && press.Verb == "git push <repository> branch <name> [<commit>] --yes");
        Assert.Contains(PersonPresses.Presses, press => press.Route == PersonPresses.GitPush && press.Verb == "git push <repository> tag <name> [<commit>] --yes");
    }

    [Fact]
    public void The_push_is_one_ref_never_forced_never_upstream_with_no_followed_tags_and_its_hooks_run()
    {
        var words = GitPushActs.Arguments("abc", "refs/heads/work/fix");

        Assert.Equal("abc:refs/heads/work/fix", words[^1]);
        Assert.Equal("origin", words[^2]);
        Assert.DoesNotContain(words, word => word.StartsWith('+') || word is "-u" or "--set-upstream" or "--force" or "-f" or "--mirror" or "--all" or "--tags" or "--no-verify" or "--prune");
        Assert.Contains("--no-follow-tags", words);
        Assert.Contains("--recurse-submodules=no", words);
    }
}
