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

    /// <summary>
    /// POOL1b (D197 point 6, the account-pool design §5.3): <c>default</c> left the doors, so none is written, naming <c>order</c>; and
    /// an update or a pin names no account. HELP10 wrote a default with its account and its workspace before.
    /// </summary>
    [Theory]
    [InlineData("default", null, "work", "`default` is retired: every account of Daoris's serves every workspace (D197)")]
    [InlineData("default", null, null, "propose `order` with the account first")]
    [InlineData("update", null, "work", "names no account")]
    [InlineData("pin", "2.1.300", "work", "names no account")]
    public void A_default_or_a_misplaced_account_is_refused_with_nothing_written(string action, string? version, string? account, string says)
    {
        var (id, message) = Box().ProposeAgent(action, "claude-code", version, "a reason", "h1", Now, account);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>
    /// TOOL4l-a: a use door carries exactly the fields the driver reads; keep none is an explicit null, unchanged is absent. Since POOL1b
    /// (§5.3) it is marked for the set, `set: true`, and names no workspace; it carried `lab` before.
    /// </summary>
    [Fact]
    public void A_use_is_written_with_its_fields_and_keep_none_is_told_from_unchanged()
    {
        var (all, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now,
            use: "order", keep: "work", early: true, near: 80);
        var (none, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now, noKeep: true);
        var (same, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now, early: false);

        var file = Written(all!);
        Assert.Equal(("agent", "use", "claude-code", "order", "work"),
            (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString(),
             file.GetProperty("use").GetString(), file.GetProperty("keep").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.True), (file.GetProperty("workspace").ValueKind, file.GetProperty("set").ValueKind));
        Assert.Equal(JsonValueKind.True, Written(none!).GetProperty("set").ValueKind);
        Assert.Equal((JsonValueKind.True, 80), (file.GetProperty("early").ValueKind, file.GetProperty("near").GetInt32()));
        Assert.Equal(JsonValueKind.Null, Written(none!).GetProperty("keep").ValueKind);
        Assert.False(Written(none!).TryGetProperty("use", out _));
        var unchanged = Written(same!);
        Assert.False(unchanged.TryGetProperty("keep", out _));
        Assert.Equal(JsonValueKind.False, unchanged.GetProperty("early").ValueKind);
    }

    /// <summary>
    /// SWITCH1d: the use door's switch and curve, the curve spelled as the terminal's `--curve` a window and written as
    /// `harnesses.json` keeps it; no curve is an explicit null, which the driver reads as `--no-curve`.
    /// </summary>
    [Fact]
    public void A_use_is_written_with_its_switch_and_curve_and_no_curve_as_null()
    {
        var (own, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now,
            switched: "curve", curve: ["weekly=0:30,50:70,100:100", "session=0:85"]);
        var (cleared, _) = Box().ProposeAgent("use", "claude-code", null, "the person asked", "h1", Now, switched: "smart", noCurve: true);

        var file = Written(own!);
        Assert.Equal("curve", file.GetProperty("switch").GetString());
        Assert.Equal(
            """{"weekly":[[0,30],[50,70],[100,100]],"session":[[0,85]]}""",
            System.Text.Json.Nodes.JsonNode.Parse(file.GetProperty("curve").GetRawText())!.ToJsonString());
        Assert.False(file.TryGetProperty("early", out _));
        var none = Written(cleared!);
        Assert.Equal("smart", none.GetProperty("switch").GetString());
        Assert.Equal(JsonValueKind.Null, none.GetProperty("curve").ValueKind);
    }

    [Theory]
    [InlineData("sometimes", null, false, "`smart`, `curve` or `off`")]
    [InlineData(null, "weekly", false, "`<window>=<elapsed>:<used>,…`")]
    [InlineData(null, "weekly=0:30,fifty:70", false, "`<window>=<elapsed>:<used>,…`")]
    [InlineData(null, "weekly=0:30", true, "a curve or no curve, not both")]
    public void A_malformed_switch_or_curve_is_refused_with_nothing_written(string? switched, string? curve, bool noCurve, string says)
    {
        var (id, message) = Box().ProposeAgent("use", "claude-code", null, "a reason", "h1", Now,
            switched: switched, curve: curve is null ? null : [curve], noCurve: noCurve);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
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

    /// <summary>
    /// An order puts the accounts named first, or clears back to the ids' order, each marked for the set (POOL1b, §5.3) and naming no
    /// workspace; before POOL1b a clear named the workspace it sent back to the machine's accounts.
    /// </summary>
    [Fact]
    public void An_order_is_written_with_its_accounts_or_a_clear_marked_for_the_set()
    {
        var (list, _) = Box().ProposeAgent("order", "claude-code", null, "the person asked", "h1", Now, accounts: ["work", "play"]);
        var (clear, _) = Box().ProposeAgent("order", "claude-code", null, "the person asked", "h1", Now, clear: true);

        var file = Written(list!);
        Assert.Equal("order", file.GetProperty("door").GetString());
        Assert.Equal(["work", "play"], file.GetProperty("accounts").EnumerateArray().Select(each => each.GetString()!).ToArray());
        Assert.False(file.TryGetProperty("clear", out _));
        Assert.Equal(JsonValueKind.True, file.GetProperty("set").ValueKind);
        var cleared = Written(clear!);
        Assert.Equal((JsonValueKind.True, JsonValueKind.Null), (cleared.GetProperty("clear").ValueKind, cleared.GetProperty("workspace").ValueKind));
        Assert.Equal(JsonValueKind.True, cleared.GetProperty("set").ValueKind);
        Assert.False(cleared.TryGetProperty("accounts", out _));
    }

    /// <summary>An update, a pin or a ready is no proposal for the set, so it carries no mark (POOL1b, §5.3).</summary>
    [Fact]
    public void Only_a_use_or_an_order_is_marked_for_the_set()
    {
        var (update, _) = Box().ProposeAgent("update", "claude-code", null, "the person asked", "h1", Now);
        var (ready, _) = Box().ProposeAgent("ready", "claude-code", null, "the person asked", "h1", Now, account: "work");

        Assert.False(Written(update!).TryGetProperty("set", out _));
        Assert.False(Written(ready!).TryGetProperty("set", out _));
    }

    [Theory]
    [InlineData(false, false, "names the accounts")]
    [InlineData(true, true, "never both")]
    [InlineData(true, false, "one word")]
    public void A_malformed_order_is_refused_with_nothing_written(bool list, bool clear, string says)
    {
        string[]? accounts = list ? [clear ? "work" : "two words"] : null;
        var (id, message) = Box().ProposeAgent("order", "claude-code", null, "a reason", "h1", Now,
            accounts: accounts, clear: clear);

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
    [InlineData(null, "names the account")]
    [InlineData("two words", "An account is one word")]
    public void A_malformed_ready_is_refused_with_nothing_written(string? account, string says)
    {
        var (id, message) = Box().ProposeAgent("ready", "claude-code", null, "a reason", "h1", Now, account);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>The shape, checked here and nothing more; what the Agents screen's route says is the driver's.</summary>
    [Theory]
    [InlineData("downgrade|claude-code|", "`update`, `pin`, `use`, `order` or `ready`")]
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
