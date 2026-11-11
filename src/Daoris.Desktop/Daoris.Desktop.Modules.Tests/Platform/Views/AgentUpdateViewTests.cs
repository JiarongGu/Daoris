using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>An agent's Update or pin as its press's card shows it: judged at the ask by what the Agents screen offers, refused with no card, and its command.</summary>
public sealed class AgentUpdateViewTests
{
    private static StubGrounds Machine()
    {
        var grounds = new StubGrounds();
        grounds.Reads["HARNESSES"] = _ => new
        {
            harnesses = new object[]
            {
                new { door = "claude-code", present = true, updates = "tool", pinned = (string?)null, pinnable = true },
                new { door = "claude-code-acp", present = true, updates = "pin", pinned = "0.84.0", pinnable = true },
                new { door = "dsh", present = true, updates = (string?)null, pinned = (string?)null, pinnable = false },
                new { door = "codex-acp", present = false, updates = "tool", pinned = (string?)null, pinnable = true },
            },
        };
        return grounds;
    }

    private static Task<PressViewed> View(object payload) =>
        new AgentUpdateView().ViewAsync(Machine(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Theory]
    [InlineData("claude-code", "agent-update-tool", "daoris agent update claude-code")]
    [InlineData("claude-code-acp", "agent-update-pin", "daoris agent update claude-code-acp")]
    public async Task An_update_is_shown_as_what_the_agents_screen_does_with_its_command(string door, string kind, string command)
    {
        var viewed = await View(new { action = "update", door });

        Assert.Equal((kind, command, door), (viewed.View!.Kind, viewed.View.Command, viewed.View.Facts["agent"]));
    }

    [Theory]
    [InlineData("dsh", "declares no updater")]
    [InlineData("codex-acp", "is not installed")]
    [InlineData("ghost", "there is no agent `ghost`")]
    public async Task An_update_the_screen_would_not_offer_is_refused_with_no_card(string door, string says)
    {
        var viewed = await View(new { action = "update", door });

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task A_pin_names_its_version_and_the_one_it_replaces()
    {
        var moved = await View(new { action = "pin", door = "claude-code-acp", version = "0.90.1" });
        var first = await View(new { action = "pin", door = "claude-code", version = "2.1.300" });

        Assert.Equal(("agent-pin-from", "0.84.0", "daoris agent pin claude-code-acp 0.90.1"), (moved.View!.Kind, moved.View.Facts["pinned"], moved.View.Command));
        Assert.Equal(("agent-pin", "2.1.300"), (first.View!.Kind, first.View.Facts["version"]));
    }

    [Theory]
    [InlineData("claude-code", "", "one exact release")]
    [InlineData("claude-code", "latest", "never a pointer")]
    [InlineData("dsh", "1.2.3", "declares no package or release channel")]
    public async Task A_pin_the_route_would_refuse_is_refused_with_no_card(string door, string version, string says)
    {
        var viewed = await View(new { action = "pin", door, version });

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task Another_action_of_the_route_is_shown_by_its_values_alone()
    {
        var viewed = await View(new { action = "unpin", door = "claude-code" });

        Assert.Equal(new PressViewed(null, null), viewed);
    }
}
