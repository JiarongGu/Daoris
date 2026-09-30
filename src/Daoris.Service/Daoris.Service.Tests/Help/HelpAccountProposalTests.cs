using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>The <c>account</c> kind's writer (HELP6): an account's own model and effort (the Agents screen's <i>Model &amp; effort</i>).</summary>
public sealed class HelpAccountProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void An_accounts_model_and_effort_are_written_with_the_agent_and_the_account()
    {
        var (id, _) = Box().ProposeAgentSettings("claude-code", "work", "opus", "high", "the person wants it to think harder", "h1", Now);
        var (cleared, _) = Box().ProposeAgentSettings("claude-code", "work", "unset", null, "back to the tool's own", "h1", Now);

        var file = Written(id!);
        Assert.Equal(("account", "settings", "claude-code"), (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString()));
        Assert.Equal(("work", "opus", "high"), (file.GetProperty("account").GetString(), file.GetProperty("model").GetString(), file.GetProperty("effort").GetString()));
        var clearing = Written(cleared!);
        Assert.Equal("unset", clearing.GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Null, clearing.GetProperty("effort").ValueKind);
    }

    /// <summary>The shape, checked here and nothing more; what the route says of the values is the driver's.</summary>
    [Theory]
    [InlineData("claude-code||opus|", "names the account")]
    [InlineData("claude-code|work||", "a model, an effort, or both")]
    [InlineData("claude-code|work|two words|", "one word")]
    public void A_malformed_account_proposal_is_refused_with_nothing_written(string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();

        var (id, message) = Box().ProposeAgentSettings(part[0] ?? "", part[1] ?? "", part[2], part.ElementAtOrDefault(3), "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
