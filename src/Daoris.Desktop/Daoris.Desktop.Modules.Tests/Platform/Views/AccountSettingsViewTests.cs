using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>An account's model and effort as its press's card shows them: judged at the ask, refused with no card, and its command.</summary>
public sealed class AccountSettingsViewTests
{
    private static StubGrounds Roster(bool known = true)
    {
        var grounds = new StubGrounds();
        grounds.Reads["HARNESSES"] = _ => new
        {
            harnesses = new object[]
            {
                new { door = "claude-code", accountOf = (string?)null, settingsChoices = known ? new { models = new[] { "opus" } } : null, profiles = new[] { new { name = "work" }, new { name = "my acct" }, new { name = "R&D" } } },
                new { door = "claude-code-acp", accountOf = "claude-code", settingsChoices = known ? new { models = new[] { "opus" } } : null, profiles = new[] { new { name = "work" } } },
            },
        };
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new AccountSettingsView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_model_and_an_effort_are_shown_with_the_command_that_sets_them()
    {
        var viewed = await View(Roster(), new { door = "claude-code", profile = "work", model = "sonnet", effort = "high" });

        Assert.Equal("account-settings", viewed.View!.Kind);
        Assert.Equal("sonnet", viewed.View.Facts["model"]);
        Assert.Equal("daoris agent settings claude-code --account work model sonnet effort high", viewed.View.Command);
    }

    [Theory]
    [InlineData("my acct", "daoris agent settings claude-code --account \"my acct\" model opus")]
    [InlineData("R&D", "daoris agent settings claude-code --account <account> model opus")]
    public async Task The_account_is_spelled_for_any_shell(string account, string command)
    {
        var viewed = await View(Roster(), new { door = "claude-code", profile = account, model = "opus" });

        Assert.Equal(command, viewed.View!.Command);
    }

    [Fact]
    public async Task A_cleared_key_reads_unset_and_a_key_left_out_is_not_named_and_a_borrowing_door_names_its_owner()
    {
        var cleared = await View(Roster(), new { door = "claude-code-acp", profile = "work", effort = (string?)null });

        Assert.Equal("account-settings-effort", cleared.View!.Kind);
        Assert.Equal("daoris agent settings claude-code --account work effort unset", cleared.View.Command);
        Assert.False(cleared.View.Facts.ContainsKey("model"));
    }

    [Fact]
    public async Task A_press_the_route_would_refuse_is_refused_at_the_ask_with_no_card()
    {
        Assert.Contains("no agent `nope`", (await View(Roster(), new { door = "nope", profile = "work", model = "opus" })).Refusal);
        Assert.Contains("does not know the shape", (await View(Roster(known: false), new { door = "claude-code", profile = "work", model = "opus" })).Refusal);
        Assert.Contains("name the account", (await View(Roster(), new { door = "claude-code", model = "opus" })).Refusal);
        Assert.Contains("no account `play`", (await View(Roster(), new { door = "claude-code", profile = "play", model = "opus" })).Refusal);
        Assert.Contains("a model, an effort, or both", (await View(Roster(), new { door = "claude-code", profile = "work" })).Refusal);
        Assert.NotNull((await View(Roster(), new { door = "claude-code", profile = "work", effort = "max" })).Refusal);
    }
}
