using System.Text.Json;
using Daoris.Knowledge;

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
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>TOOL4l-a: a use door carries exactly the fields the driver reads; keep none is an explicit null, unchanged is absent.</summary>
    [Fact]
    public void A_use_is_written_with_its_fields_and_keep_none_is_told_from_unchanged()
    {
        var (all, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now,
            workspace: "lab", use: "order", keep: "work", early: true, near: 80);
        var (none, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now, noKeep: true);
        var (same, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now, early: false);

        var file = Written(all!);
        Assert.Equal(("agent", "use", "claude-code", "order", "work", "lab"),
            (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString(),
             file.GetProperty("use").GetString(), file.GetProperty("keep").GetString(), file.GetProperty("workspace").GetString()));
        Assert.Equal((JsonValueKind.True, 80), (file.GetProperty("early").ValueKind, file.GetProperty("near").GetInt32()));
        Assert.Equal(JsonValueKind.Null, Written(none!).GetProperty("keep").ValueKind);
        Assert.False(Written(none!).TryGetProperty("use", out _));
        var unchanged = Written(same!);
        Assert.False(unchanged.TryGetProperty("keep", out _));
        Assert.Equal(JsonValueKind.False, unchanged.GetProperty("early").ValueKind);
    }

    [Theory]
    [InlineData(null, null, false, null, null, "names what changes")]
    [InlineData("random", null, false, null, null, "`goal` or `order`")]
    [InlineData("goal", "two words", false, null, null, "An account is one word")]
    [InlineData("goal", "work", true, null, null, "keep one account or none")]
    [InlineData(null, null, false, null, 49, "from 50 to 99")]
    [InlineData(null, null, false, null, 100, "from 50 to 99")]
    public void A_malformed_use_is_refused_with_nothing_written(string? use, string? keep, bool noKeep, bool? early, int? near, string says)
    {
        var (id, message) = Box().ProposeAgent("use", "claude-code", null, "a reason", "h1", Now,
            use: use, keep: keep, noKeep: noKeep, early: early, near: near);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Fact]
    public void An_order_is_written_with_its_accounts_or_clears_a_workspace()
    {
        var (list, _) = Box().ProposeAgent("order", "claude-code", null, "the person asked", "h1", Now, accounts: ["work", "play"]);
        var (clear, _) = Box().ProposeAgent("order", "claude-code", null, "the person asked", "h1", Now, workspace: "lab", clear: true);

        var file = Written(list!);
        Assert.Equal("order", file.GetProperty("door").GetString());
        Assert.Equal(["work", "play"], file.GetProperty("accounts").EnumerateArray().Select(each => each.GetString()!).ToArray());
        Assert.False(file.TryGetProperty("clear", out _));
        var cleared = Written(clear!);
        Assert.Equal((JsonValueKind.True, "lab"), (cleared.GetProperty("clear").ValueKind, cleared.GetProperty("workspace").GetString()));
        Assert.False(cleared.TryGetProperty("accounts", out _));
    }

    [Theory]
    [InlineData(false, false, null, "names the accounts")]
    [InlineData(true, true, "lab", "never both")]
    [InlineData(false, true, null, "names the workspace")]
    [InlineData(true, false, null, "one word")]
    public void A_malformed_order_is_refused_with_nothing_written(bool list, bool clear, string? workspace, string says)
    {
        string[]? accounts = list ? [clear ? "work" : "two words"] : null;
        var (id, message) = Box().ProposeAgent("order", "claude-code", null, "a reason", "h1", Now,
            workspace: workspace, accounts: accounts, clear: clear);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Theory]
    [InlineData("work")]
    [InlineData("own")]
    public void A_ready_is_written_with_the_account(string account)
    {
        var (id, _) = Box().ProposeAgent("ready", "claude-code", null, "the person asked", "h1", Now, account: account);

        var file = Written(id!);
        Assert.Equal(("ready", account), (file.GetProperty("door").GetString(), file.GetProperty("account").GetString()));
    }

    [Theory]
    [InlineData(null, null, "names the account")]
    [InlineData("two words", null, "An account is one word")]
    [InlineData("work", "lab", "names no account or workspace")]
    public void A_malformed_ready_is_refused_with_nothing_written(string? account, string? workspace, string says)
    {
        var (id, message) = Box().ProposeAgent("ready", "claude-code", null, "a reason", "h1", Now, account, workspace);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>The shape, checked here and nothing more; what the Agents screen's route says is the driver's.</summary>
    [Theory]
    [InlineData("downgrade|claude-code|", "`update`, `pin`, `default`, `use`, `order` or `ready`")]
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
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
