using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>The <c>agent</c> kind's writer (HELP6): an agent's Update, or a pin to one version — the Agents screen's two presses.</summary>
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

    /// <summary>The shape, checked here and nothing more; what the Agents screen's route says is the driver's.</summary>
    [Theory]
    [InlineData("downgrade|claude-code|", "`update` or `pin`")]
    [InlineData("update| |", "names the agent")]
    [InlineData("pin|claude-code|", "names the version")]
    [InlineData("update|claude-code|2.1.300", "names no version")]
    public void A_malformed_agent_proposal_is_refused_with_nothing_written(string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();

        var (id, message) = Box().ProposeAgent(part[0] ?? "", part[1] ?? "", part[2], "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
