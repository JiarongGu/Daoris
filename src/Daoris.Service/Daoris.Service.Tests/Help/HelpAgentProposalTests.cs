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

    /// <summary>HELP10: an account made the default carries the account as its value, and the workspace where it is one workspace's.</summary>
    [Fact]
    public void An_accounts_default_is_written_with_the_account_and_the_workspace()
    {
        var (machine, _) = Box().ProposeAgent("default", "claude-code", null, "the person asked", "h1", Now, account: "work");
        var (circle, _) = Box().ProposeAgent("default", "claude-code-acp", null, "the person asked", "h1", Now, account: "play", workspace: "lab");

        var file = Written(machine!);
        Assert.Equal(("agent", "default", "claude-code", "work"),
            (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString(), file.GetProperty("value").GetString()));
        Assert.Equal(JsonValueKind.Null, file.GetProperty("workspace").ValueKind);
        Assert.Equal(("play", "lab"), (Written(circle!).GetProperty("value").GetString(), Written(circle!).GetProperty("workspace").GetString()));
    }

    /// <summary>HELP10: a default's shape — an account named, one word, and no version; an update or a pin names no account.</summary>
    [Theory]
    [InlineData("default", null, null, null, "names the account")]
    [InlineData("default", "2.1.300", "work", null, "names no version")]
    [InlineData("default", null, "two words", null, "An account is one word")]
    [InlineData("default", null, "work", "two words", "A workspace is one word")]
    [InlineData("update", null, "work", null, "names no account")]
    [InlineData("pin", "2.1.300", null, "lab", "names no account")]
    public void A_malformed_default_is_refused_with_nothing_written(string action, string? version, string? account, string? workspace, string says)
    {
        var (id, message) = Box().ProposeAgent(action, "claude-code", version, "a reason", "h1", Now, account, workspace);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }

    /// <summary>The shape, checked here and nothing more; what the Agents screen's route says is the driver's.</summary>
    [Theory]
    [InlineData("downgrade|claude-code|", "`update`, `pin` or `default`")]
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
