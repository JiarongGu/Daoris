using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A retry and a git identity's use as their presses' cards show them: refused with no card where the quest or identity is not one the machine holds, and the command that does it.</summary>
public sealed class RetryAndGitUseViewsTests
{
    private static StubGrounds Grounds()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "work";
        grounds.Remotes["engine"] = new Dictionary<string, string?> { ["origin"] = "https://github.com/acme/engine.git", ["upstream"] = "git@github.com:acme/engine.git", ["lab"] = "https://gitlab.com/acme/engine.git" };
        grounds.Facts = grounds.Facts with
        {
            Parked = [new ParkedQuest("q1", "engine", 3, 3)],
            Held = [new HeldQuest("q2", "engine", "s-9")],
            GitIdentities = [new HelpGitIdentity("id-1", "work key", "github.com", "ssh")],
        };
        return grounds;
    }

    private static Task<PressViewed> View(IPressView view, string json) =>
        view.ViewAsync(Grounds(), JsonDocument.Parse(json).RootElement, CancellationToken.None);

    [Fact]
    public async Task A_parked_quest_is_shown_counted_from_the_failures_the_look_counted()
    {
        var viewed = await View(new RetryQuestView(), """{"quest":"#q1"}""");

        Assert.Equal(("retry-parked", "daoris quest retry q1"), (viewed.View!.Kind, viewed.View.Command));
        Assert.Equal(("3", "3"), (viewed.View.Facts["failures"], viewed.View.Facts["more"]));
    }

    [Fact]
    public async Task A_quest_held_by_the_persons_stop_is_shown_released_from_that_session()
    {
        var viewed = await View(new RetryQuestView(), """{"quest":"q2"}""");

        Assert.Equal("retry-held", viewed.View!.Kind);
        Assert.Equal("s-9", viewed.View.Facts["session"]);
    }

    [Theory]
    [InlineData("""{"quest":"ghost"}""", "neither parked nor held")]
    [InlineData("""{}""", "names the quest")]
    public async Task A_quest_that_is_neither_parked_nor_held_is_refused_with_no_card(string json, string says)
    {
        var viewed = await View(new RetryQuestView(), json);

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Theory]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"origin","value":"work key"}""", "git-use", "daoris git use engine \"work key\"")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"upstream","value":"id-1"}""", "git-use", "daoris git use engine --remote upstream \"work key\"")]
    [InlineData("""{"scope":"workspace","workspace":"work","host":"github.com","value":"work key"}""", "git-use", "daoris git use --workspace work --host github.com \"work key\"")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"origin"}""", "git-use-clear", "daoris git use engine --clear")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"origin","value":"own"}""", "git-use-own", "daoris git use engine own")]
    [InlineData("""{"scope":"machine","host":"github.com","value":"own"}""", "git-use-own", "daoris git use --machine --host github.com own")]
    public async Task A_git_identity_is_shown_with_its_command(string json, string kind, string command)
    {
        var viewed = await View(new GitUseView(), json);

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_is_refused_with_no_card()
    {
        var grounds = Grounds();
        grounds.Remotes.Clear();

        var viewed = await new GitUseView().ViewAsync(grounds, JsonDocument.Parse("""{"scope":"repository","repository":"engine","value":"own"}""").RootElement, CancellationToken.None);

        Assert.Null(viewed.View);
        Assert.Contains("has no checkout here", viewed.Refusal);
    }

    [Theory]
    [InlineData("""{"scope":"repository","repository":"engine","value":"ghost"}""", "no git identity `ghost`")]
    [InlineData("""{"scope":"repository","repository":"nowhere","value":"work key"}""", "`nowhere` is not registered")]
    [InlineData("""{"scope":"workspace","workspace":"work","value":"work key"}""", "add `--host")]
    [InlineData("""{"scope":"workspace","workspace":"work","host":"gitlab.com","value":"work key"}""", "is for github.com")]
    [InlineData("""{"scope":"machine","host":"github.com","value":"work key"}""", "own sign-ins or nothing")]
    [InlineData("""{"scope":"folder"}""", "not a scope")]
    [InlineData("""{"scope":"machine","host":"github.com","value":"OWN"}""", "own sign-ins or nothing")]
    [InlineData("""{"scope":"machine","host":"bad host","value":"own"}""", "is not a host")]
    [InlineData("""{"scope":"workspace","workspace":"work","host":"https://github.com","value":"own"}""", "is not a host")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"lab","value":"work key"}""", "is for github.com, not gitlab.com")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"nope","value":"own"}""", "has no remote nope")]
    [InlineData("""{"scope":"repository","repository":"engine","remote":"nope"}""", "has no remote nope")]
    public async Task A_git_identity_the_machine_would_refuse_is_refused_with_no_card(string json, string says)
    {
        var viewed = await View(new GitUseView(), json);

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }
}
