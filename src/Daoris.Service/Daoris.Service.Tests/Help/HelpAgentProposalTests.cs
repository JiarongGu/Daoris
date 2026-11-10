using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>The <c>agent</c> kind's writer: an agent's Update, or a pin to one version — the Agents screen's two presses.</summary>
public sealed class HelpAgentProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void An_agent_update_or_pin_is_written_with_the_agent_and_the_version()
    {
        var (update, message) = Box().ProposeAgent("update", "claude-code-acp", null, "the person asked for the newest", "h1", Now);
        var (pin, _) = Box().ProposeAgent(" Pin ", "claude-code", "2.1.300", "the person wants that release", "h1", Now);

        Assert.Contains($"#{update}", message);
        var updated = Written(update!);
        Assert.Equal(("agent", "update", "claude-code-acp"), (updated.GetProperty("kind").GetString(), updated.GetProperty("door").GetString(), updated.GetProperty("target").GetString()));
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("value").ValueKind);
        var pinned = Written(pin!);
        Assert.Equal(("agent", "pin", "claude-code", "2.1.300"),
            (pinned.GetProperty("kind").GetString(), pinned.GetProperty("door").GetString(), pinned.GetProperty("target").GetString(), pinned.GetProperty("value").GetString()));
        Assert.Equal("h1", pinned.GetProperty("by").GetProperty("session").GetString());
    }

    /// <summary>A door the kind does not have is refused as an unknown word, naming the doors, the accounts' presses among them.</summary>
    [Theory]
    [InlineData("default", null, "An agent's change is `update` or `pin`")]
    [InlineData("use", null, "pressed with `daoris agent account`")]
    [InlineData("order", null, "pressed with `daoris agent account`")]
    [InlineData("ready", null, "pressed with `daoris agent account`")]
    [InlineData("update", "2.1.300", "names no version")]
    [InlineData("pin", null, "names the version")]
    public void A_door_of_no_kind_or_a_misplaced_version_is_refused_with_nothing_written(string action, string? version, string says)
    {
        var (id, message) = Box().ProposeAgent(action, "claude-code", version, "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
