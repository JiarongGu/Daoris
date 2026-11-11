using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>How an agent's accounts are used, ordered and ended early, as their press's card shows them: judged at the ask, refused with no card, and its command.</summary>
public sealed class AccountUseViewTests
{
    private static StubGrounds Machine(bool cooling = true)
    {
        var grounds = new StubGrounds();
        grounds.Reads["HARNESSES"] = _ => new
        {
            harnesses = new object[]
            {
                new { door = "claude-code", accountOf = (string?)null, profiles = new[] { new { name = "work" }, new { name = "play" }, new { name = "spare" } } },
                new { door = "claude-code-acp", accountOf = "claude-code", profiles = new[] { new { name = "work" }, new { name = "play" }, new { name = "spare" } } },
                new { door = "dsh", accountOf = (string?)null, profiles = Array.Empty<object>() },
            },
        };
        grounds.Reads["ACCOUNTS"] = _ => new
        {
            agents = new object[]
            {
                new
                {
                    agent = "claude-code",
                    own = new { cooling = cooling ? new { until = "x" } : null },
                    accounts = new object[] { new { name = "work", cooling = (object?)null }, new { name = "play", cooling = cooling ? new { until = "x" } : null }, new { name = "spare", cooling = (object?)null } },
                    scopes = new[] { new { list = new[] { "work", "play" } } },
                },
            },
        };
        return grounds;
    }

    private static Task<PressViewed> View(object payload, StubGrounds? grounds = null) =>
        new AccountUseView().ViewAsync(grounds ?? Machine(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task How_the_set_is_used_is_shown_with_the_command_that_sets_it()
    {
        var viewed = await View(new { action = "use", door = "claude-code", use = "order", keep = "play", early = false, near = 85 });

        Assert.Equal("account-use", viewed.View!.Kind);
        Assert.Equal("claude-code", viewed.View.Facts["agent"]);
        Assert.Equal("daoris agent account use claude-code order --keep play --early off --near 85", viewed.View.Command);
        Assert.Equal("daoris agent account use claude-code --no-keep", (await View(new { action = "use", door = "claude-code-acp", noKeep = true })).View!.Command);
    }

    [Fact]
    public async Task A_switch_a_curve_and_a_clear_are_spelled_as_the_terminal_takes_them()
    {
        var curve = new { weekly = new[] { new[] { 0, 30 }, new[] { 50, 70 }, new[] { 100, 100 } } };

        Assert.Equal("daoris agent account use claude-code --switch curve --curve weekly=0:30,50:70,100:100",
            (await View(new { action = "use", door = "claude-code", @switch = "curve", curve })).View!.Command);
        Assert.Equal("daoris agent account use claude-code --switch off --no-curve",
            (await View(new { action = "use", door = "claude-code", @switch = "off", noCurve = true })).View!.Command);
        Assert.Equal("daoris agent account use claude-code --clear", (await View(new { action = "use", door = "claude-code", clear = true })).View!.Command);
    }

    [Theory]
    [InlineData("claude-code", null, null, 0, "a use names how the accounts are used")]
    [InlineData("dsh", "goal", null, 0, "`dsh` has no account of Daoris's, so there is nothing to use")]
    [InlineData("claude-code", "spread", null, 0, "`spread` is not a way to use accounts")]
    [InlineData("claude-code", null, null, 49, "a whole percent from 50 to 99, not 49")]
    [InlineData("claude-code", null, "solo", 0, "`solo` is not one of `claude-code`'s accounts (work, then play)")]
    [InlineData("gpt-agent", "goal", null, 0, "no agent `gpt-agent`")]
    public async Task A_use_the_route_would_refuse_is_refused_at_the_ask_with_no_card(string door, string? use, string? keep, int near, string says)
    {
        var viewed = await View(new { action = "use", door, use, keep, near = near == 0 ? (int?)null : near });

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task A_switch_this_build_does_not_know_and_a_workspace_are_refused()
    {
        Assert.Contains("`sometimes` is not when to switch accounts", (await View(new { action = "use", door = "claude-code", @switch = "sometimes" })).Refusal);
        Assert.Contains("every workspace", (await View(new { action = "use", door = "claude-code", use = "goal", workspace = "work" })).Refusal);
    }

    [Fact]
    public async Task An_order_is_shown_whole_and_a_clear_is_shown_as_the_ids_order()
    {
        var first = await View(new { action = "order", door = "claude-code-acp", accounts = new[] { "spare" } });
        var back = await View(new { action = "order", door = "claude-code", accounts = Array.Empty<string>(), clear = false, ids = Array.Empty<string>() });

        Assert.Equal("account-order", first.View!.Kind);
        Assert.Equal("`spare`, then `work`, then `play`", first.View.Facts["order"]);
        Assert.Equal("daoris agent account order claude-code spare", first.View.Command);
        // `--clear` sends an empty list with no flag, and the route clears the order for it.
        Assert.Equal("account-order-clear", back.View!.Kind);
        Assert.Equal("daoris agent account order claude-code --clear", back.View.Command);
        Assert.Equal("account-order-clear", (await View(new { action = "order", door = "claude-code" })).View!.Kind);
        Assert.Equal("daoris agent account order claude-code --clear", (await View(new { action = "order", door = "claude-code", clear = true })).View!.Command);
    }

    [Theory]
    [InlineData(new[] { "work", "gone" }, false, "has no account `gone` on this machine")]
    [InlineData(new[] { "work", "work" }, false, "`work` is named twice")]
    [InlineData(new[] { "work" }, true, "never both")]
    public async Task An_order_the_route_would_refuse_is_refused_at_the_ask(string[] accounts, bool clear, string says) =>
        Assert.Contains(says, (await View(new { action = "order", door = "claude-code", accounts, clear })).Refusal);

    [Fact]
    public async Task A_ready_is_shown_for_an_account_cooling_and_for_the_tools_own_sign_in()
    {
        var account = await View(new { action = "ready", door = "claude-code", profile = "play" });
        var own = await View(new { action = "ready", door = "claude-code-acp", own = true });

        Assert.Equal("account-ready", account.View!.Kind);
        Assert.Equal("daoris agent account ready claude-code play", account.View.Command);
        Assert.Equal("account-ready-own", own.View!.Kind);
        Assert.Equal("daoris agent account ready claude-code --own", own.View.Command);
    }

    [Theory]
    [InlineData("gone", "has no account `gone` on this machine")]
    [InlineData("spare", "`spare` is not cooling, so there is nothing to end — cooling now: `play`, its own sign-in")]
    [InlineData(null, "names the account whose cool-off ends")]
    public async Task A_ready_the_route_would_refuse_is_refused_at_the_ask(string? profile, string says) =>
        Assert.Contains(says, (await View(new { action = "ready", door = "claude-code", profile })).Refusal);

    [Fact]
    public async Task Nothing_cooling_is_nothing_to_end()
    {
        var viewed = await View(new { action = "ready", door = "claude-code", own = true }, Machine(cooling: false));

        Assert.Contains("is not cooling, so there is nothing to end", viewed.Refusal);
    }

    [Fact]
    public async Task An_action_with_no_view_of_its_own_shows_the_values_alone()
    {
        var viewed = await View(new { action = "said", door = "claude-code", ids = new[] { "r1" } });

        Assert.Null(viewed.View);
        Assert.Null(viewed.Refusal);
    }
}
