using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An account with several cooling entries is said once, with a line for each of its entries.</summary>
public sealed class AgentCoolingLinesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private static CoolingEntry Entry(string account, string? model) =>
        new("claude-code", account, Now.AddHours(3), Stated: true, "weekly", Now, "s-1", Model: model);

    [Fact]
    public void A_whole_account_entry_does_not_hide_a_family_entry_of_the_same_account()
    {
        CoolingEntry[] cooling = [Entry("account-1", null), Entry("account-1", "opus"), Entry("account-2", "sonnet")];

        var lines = AgentAccountLines.CoolingLinesOf(cooling, "claude-code", "account-1", Now, TimeZoneInfo.Utc);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain("for `", lines[0], StringComparison.Ordinal);
        Assert.Contains("for `opus`", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_accounts_cooling_now_are_each_named_once()
    {
        CoolingEntry[] cooling = [Entry("account-1", null), Entry("account-1", "opus"), Entry("account-2", "sonnet")];

        Assert.Equal(["account-1", "account-2"], AgentAccountLines.CoolingNow(cooling, "claude-code"));
    }
}
