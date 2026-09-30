using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's proposals (HELP1c, D89): a change it wants is a file under the driver's home, shaped
/// here and judged by the driver with the route's own code before the person sees it. Nothing here
/// applies anything: every change is the person's press.
/// </summary>
public sealed class HelpProposalsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-proposals-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-29T10:00:00Z");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private HelpProposalBox Box() => new(_home);

    private JsonElement Written(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_home, "help", "proposals", $"{id}.json")));
        return document.RootElement.Clone();
    }

    [Fact]
    public void A_setting_is_written_as_a_file_the_driver_judges_with_who_proposed_it_and_why()
    {
        var (id, message) = Box().ProposeSetting(
            new SettingChange("landing", Target: null, Workspace: "work", Value: "branch feature/{quest}-{slug} --tidy"),
            "the person asked for work to land on feature branches", session: "h1e1p000", Now);

        Assert.NotNull(id);
        Assert.Contains($"#{id}", message);
        Assert.Contains("Apply", message);
        var file = Written(id!);
        Assert.Equal("setting", file.GetProperty("kind").GetString());
        Assert.Equal("landing", file.GetProperty("door").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("target").ValueKind);
        Assert.Equal("branch feature/{quest}-{slug} --tidy", file.GetProperty("value").GetString());
        Assert.Equal("h1e1p000", file.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", file.GetProperty("state").GetString());
        Assert.Equal("the person asked for work to land on feature branches", file.GetProperty("why").GetString());
    }

    [Fact]
    public void An_ask_is_written_with_its_words_and_its_workspace()
    {
        var (id, _) = Box().ProposeAsk("fix the chunk streamer's cold-cache stall", "work", "the person wants it started", "h1", Now);

        var file = Written(id!);
        Assert.Equal("ask", file.GetProperty("kind").GetString());
        Assert.Equal("fix the chunk streamer's cold-cache stall", file.GetProperty("sentence").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
    }

    /// <summary>The shape is checked here, and nothing more: what the route would say is the driver's.</summary>
    [Theory]
    [InlineData("push", "engine", null, null, "is not a door")]
    [InlineData("drive", null, null, null, "names the repository")]
    [InlineData("trees", "engine", null, "sometimes", "`on` or `off`")]
    [InlineData("line", null, null, "develop", "a repository or a workspace")]
    [InlineData("line", "engine", "work", "develop", "a repository or a workspace")]
    [InlineData("line", "engine", null, null, "a branch, or `--clear`")]
    [InlineData("landing", "engine", null, "rebase", "`merge`, `branch <pattern>`")]
    [InlineData("intake", null, null, null, "an agent, or `off`")]
    [InlineData("strikes", null, null, "many", "a whole number")]
    [InlineData("timeout", null, null, "0", "a whole number of minutes, 1 or more")]
    [InlineData("notify", null, null, "loud", "`on` or `off`")]
    public void A_setting_that_is_no_door_s_shape_is_refused_with_nothing_written(
        string door, string? target, string? workspace, string? value, string says)
    {
        var (id, message) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }

    [Fact]
    public void A_proposal_needs_its_reason_and_a_home()
    {
        Assert.Null(Box().ProposeSetting(new SettingChange("drive", "engine", null, null), " ", "h1", Now).Id);
        Assert.Null(Box().ProposeAsk(" ", "work", "a reason", "h1", Now).Id);
        Assert.Null(new HelpProposalBox(null).ProposeSetting(new SettingChange("drive", "engine", null, null), "a reason", "h1", Now).Id);
        Assert.Null(Box().ProposeAgent("update", "claude-code", null, " ", "h1", Now).Id);
        Assert.Null(new HelpProposalBox(null).ProposeGo("quests", null, null, "a reason", "h1", Now).Id);
    }

    /// <summary>HELP6: an agent's Update, or a pin to one version — the Agents screen's two presses.</summary>
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

    /// <summary>HELP6: a quest or an ask made by mistake, by its id — the drawer's and the record's Delete.</summary>
    [Fact]
    public void A_delete_is_written_with_what_it_would_delete()
    {
        var (quest, _) = Box().ProposeDelete("#q1a2b3c4", null, "a duplicate of #q9", "h1", Now);
        var (ask, _) = Box().ProposeDelete(null, "a5b6c7d8", "made by mistake", "h1", Now);

        var one = Written(quest!);
        Assert.Equal(("delete", "quest", "q1a2b3c4"), (one.GetProperty("kind").GetString(), one.GetProperty("door").GetString(), one.GetProperty("target").GetString()));
        var other = Written(ask!);
        Assert.Equal(("delete", "ask", "a5b6c7d8"), (other.GetProperty("kind").GetString(), other.GetProperty("door").GetString(), other.GetProperty("target").GetString()));
    }

    /// <summary>HELP6: an account's own model and effort (the Agents screen's *Model &amp; effort*).</summary>
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

    /// <summary>HELP6: a screen to open — a view, a Settings domain, a part of it or a setup step.</summary>
    [Fact]
    public void A_go_is_written_with_the_place()
    {
        var (id, _) = Box().ProposeGo("Settings", "start", "helper", "the person asked where to name its agent", "h1", Now);
        var (view, _) = Box().ProposeGo("quests", null, null, "the person asked where asks are", "h1", Now);

        var file = Written(id!);
        Assert.Equal(("go", "go", "settings"), (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString()));
        Assert.Equal(("start", "helper"), (file.GetProperty("domain").GetString(), file.GetProperty("part").GetString()));
        Assert.Equal(JsonValueKind.Null, Written(view!).GetProperty("domain").ValueKind);
    }

    /// <summary>HELP6: the new kinds' shapes, checked here and nothing more; what each route says is the driver's.</summary>
    [Theory]
    [InlineData("agent", "downgrade|claude-code|", "`update` or `pin`")]
    [InlineData("agent", "update| |", "names the agent")]
    [InlineData("agent", "pin|claude-code|", "names the version")]
    [InlineData("agent", "update|claude-code|2.1.300", "names no version")]
    [InlineData("delete", "||", "a quest or an ask — name exactly one")]
    [InlineData("delete", "q1|a1|", "a quest or an ask — name exactly one")]
    [InlineData("account", "claude-code||opus|", "names the account")]
    [InlineData("account", "claude-code|work||", "a model, an effort, or both")]
    [InlineData("account", "claude-code|work|two words|", "one word")]
    [InlineData("go", "||", "names the view")]
    [InlineData("go", "quests|agents|", "domain is a part of Settings")]
    [InlineData("go", "settings|work space|", "one word")]
    public void A_malformed_proposal_of_a_new_kind_is_refused_with_nothing_written(string kind, string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();
        var (id, message) = kind switch
        {
            "agent" => Box().ProposeAgent(part[0] ?? "", part[1] ?? "", part[2], "a reason", "h1", Now),
            "delete" => Box().ProposeDelete(part[0], part[1], "a reason", "h1", Now),
            "account" => Box().ProposeAgentSettings(part[0] ?? "", part[1] ?? "", part[2], part.ElementAtOrDefault(3), "a reason", "h1", Now),
            _ => Box().ProposeGo(part[0] ?? "", part[1], part[2], "a reason", "h1", Now),
        };

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
