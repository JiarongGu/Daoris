using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 rows d, e and f: what the roster reads of an account's words, its key variable and its week, it reads through the
/// registry, each door answering as the toolchain it was read from did; Claude Code's and Codex's words stand in their own folders; Claude
/// Code's conversation home is the one the tool-home leftovers read; the pin's move is decided once.
/// </summary>
public sealed class ProviderAccountReadsTests
{
    private static HarnessRoster Roster() => new(ProviderRegistry.Built(), "provider-account-reads-nowhere");

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void A_Claude_Code_door_reads_Claude_Codes_words_and_asks_its_windows(string door)
    {
        var roster = Roster();
        Assert.Same(ClaudeLimits.Words, roster.LimitsOf(door));
        Assert.Same(ClaudeSignIn.Words, roster.SignInOf(door));
        Assert.Same(ClaudeWindows.Words, roster.WindowsOf(door));
        Assert.NotNull(roster.UsageOf(door));
        Assert.True(roster.Speaks(door));
    }

    [Fact]
    public void Codex_s_door_reads_Codex_s_limit_words_and_asks_its_windows_and_no_sign_in_words()
    {
        var roster = Roster();
        Assert.Same(CodexLimits.Words, roster.LimitsOf("codex-acp"));
        Assert.Null(roster.SignInOf("codex-acp"));
        Assert.Null(roster.WindowsOf("codex-acp"));
        Assert.IsType<CodexUsageQuestion>(roster.UsageOf("codex-acp"));
    }

    [Fact]
    public void A_name_nobody_carries_reads_no_words()
    {
        var roster = Roster();
        Assert.Null(roster.LimitsOf("nobody-knows"));
        Assert.Null(roster.SignInOf("nobody-knows"));
        Assert.Null(roster.WindowsOf("nobody-knows"));
        Assert.Null(roster.UsageOf("nobody-knows"));
        Assert.Null(roster.KeyVariableOf("nobody-knows"));
        Assert.False(roster.WeekFixedOf("nobody-knows"));
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void A_door_of_an_agent_that_takes_a_key_reads_the_agent_s_variable_once(string door)
    {
        var roster = Roster();
        Assert.Equal("ANTHROPIC_API_KEY", roster.KeyVariableOf(door));
        Assert.Equal(ProviderRegistry.Built().Resolve("claude-code").Toolchain!.KeyVariable, roster.KeyVariableOf(door));
    }

    [Fact]
    public void A_door_whose_agent_takes_no_key_reads_none()
    {
        var roster = Roster();
        foreach (var door in ProviderRegistry.Built().Names)
        {
            Assert.Equal(
                roster.AccountToolchain(door)?.KeyVariable is { Length: > 0 } variable ? variable : null,
                roster.KeyVariableOf(door));
        }
    }

    [Fact]
    public void An_agent_carries_its_week_as_its_account_agent_declares_it()
    {
        var roster = Roster();
        Assert.True(roster.WeekFixedOf("claude-code"));
        Assert.True(roster.WeekFixedOf("claude-code-acp"));
        Assert.False(roster.WeekFixedOf("codex-acp"));
    }

    [Fact]
    public void Claude_Code_alone_declares_the_project_layout_the_leftovers_read()
    {
        var declaring = Providers.Built().Where(provider => provider.Conversations.IsKnown).Select(provider => provider.Id.Value).ToList();
        Assert.Equal(["claude-code", "alt-stub"], declaring.OrderByDescending(id => id == "claude-code").ToList());
        Assert.Equal(["alt-sessions"], Providers.Built().Single(provider => provider.Id.Value == "alt-stub").Conversations.Required.Keeps(null));
        Assert.Equal(
            ["claude-code"],
            Providers.Built().Where(provider => provider.Conversations.IsKnown && provider.Conversations.Required.Projects is not null)
                .Select(provider => provider.Id.Value).ToList());

        var layout = Providers.Built().Single(provider => provider.Id.Value == "claude-code").Conversations.Required.Projects!;
        Assert.Equal(layout.Variable, ToolHomeLeftovers.Variable);
        Assert.Equal("CLAUDE_CONFIG_DIR", layout.Variable);
        Assert.Equal("projects", layout.Folder);
        Assert.Equal("C--Users-a-b--c", ToolHomeLeftovers.Flatten("C:" + @"\Users\a b-.c"));
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3", true, "Stays")]
    [InlineData("1.2.3", "1.2.3", false, "Reinstall")]
    [InlineData("1.2.2", "1.2.3", true, "Behind")]
    [InlineData("1.2.2", "1.2.3", false, "Behind")]
    [InlineData("1.3.0", "1.2.3", true, "Move")]
    [InlineData("1.3.0", "1.2.3", false, "Move")]
    public void A_pin_s_move_is_decided_once_for_the_window_and_the_terminal(string newest, string pin, bool installed, string expected) =>
        Assert.Equal(Enum.Parse<PinMoveKind>(expected), PinMove.Decide(newest, pin, installed));

    [Fact]
    public void The_holder_s_program_and_home_variable_are_Claude_Code_s_registration()
    {
        var program = ManagedProgram.Of("claude-code");
        Assert.Equal("claude", program.Binary);
        Assert.Equal("CLAUDE_CONFIG_DIR", program.HomeVariable);
        Assert.Throws<DriverException>(() => ManagedProgram.Of("nobody-knows"));
    }
}
