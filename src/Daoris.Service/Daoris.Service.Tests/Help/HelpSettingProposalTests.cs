using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>The <c>setting</c> kind's writer (HELP1c): one of the driver's doors, spelled as the CLI's verbs are.</summary>
public sealed class HelpSettingProposalTests : HelpProposalBoxFixture
{
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

    /// <summary>HELP8: a branch rule naming a plugin passes the shape; whether the plugin lands work here is the driver's to judge.</summary>
    [Fact]
    public void A_landing_naming_a_plugin_is_written_as_the_terminal_spells_it()
    {
        var (id, _) = Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "branch feature/{quest}-{slug} --plugin example.lands"),
            "the person wants a pull request opened for each landing", session: "h1e1p000", Now);

        Assert.Equal("branch feature/{quest}-{slug} --plugin example.lands", Written(id!).GetProperty("value").GetString());
        Assert.Contains("`--plugin <id>`", Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "rebase"), "why", session: "h1e1p000", Now).Message);
    }

    /// <summary>
    /// LANDSVC1 (LAND2a, LAND2b): a branch rule that accepts automatically passes the shape as the terminal spells it, and
    /// a landing refused for its shape names the switch beside `--tidy` and `--plugin`, so the helper learns it as the room
    /// says it; whether the plugin lands work here, and a merge carrying it, are the driver's to judge.
    /// </summary>
    [Fact]
    public void A_landing_that_accepts_automatically_is_written_and_the_refusal_names_the_switch()
    {
        var (id, _) = Box().ProposeSetting(
            new SettingChange("landing", Target: null, Workspace: "work", Value: "branch feature/{quest}-{slug} --plugin example.lands --auto-accept"),
            "the person asked for done work to land itself and open its pull request", session: "h1e1p000", Now);

        Assert.Equal("branch feature/{quest}-{slug} --plugin example.lands --auto-accept", Written(id!).GetProperty("value").GetString());
        Assert.Contains("`--auto-accept` for a quest's done to land it with no press", Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "rebase"), "why", session: "h1e1p000", Now).Message);
    }

    /// <summary>
    /// LANDSVC1 (D145 points 1 and 5): the tool tells the helper a branch rule may accept automatically, what that does, and
    /// that it is the person's standing say-so for a push, proposed only when they ask and never on a merge, as the room
    /// says it, so an agent finding what it may propose learns the switch from the tool and not from the room alone.
    /// </summary>
    [Fact]
    public void The_tool_says_a_branch_rule_may_accept_automatically_and_only_when_the_person_asks()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        var value = method.GetParameters().Single(parameter => parameter.Name == "value")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`--tidy`, `--plugin <id>` and `--auto-accept` if wanted", value);
        Assert.Contains("a `landing` on a branch may add `--auto-accept`: a quest's done then lands its work with no press", tool);
        Assert.Contains("the rule's plugin pushes it and opens a pull request without asking each time", tool);
        Assert.Contains("the person's standing say-so for that push, so propose it only when they ask for it, and never on a merge", tool);
    }

    /// <summary>HELP9: reading and writing across (D107), a cap and an adapter pass the shape as the terminal spells them.</summary>
    [Theory]
    [InlineData("across", "engine", null, "read off")]
    [InlineData("across", null, "work", "read --clear")]
    [InlineData("across", "plugins", null, "write-to engine")]
    [InlineData("across", "plugins", null, "write-to engine --clear")]
    [InlineData("cap", null, null, "3")]
    [InlineData("adapter", null, null, "claude-code-acp")]
    // KNOWUSE1b: a standing answer for a repository, in the person's words, or cleared.
    [InlineData("standing", "engine", null, "dev writes allowed; test locally against dev; prod only on a yes")]
    [InlineData("standing", "engine", null, "--clear")]
    // LANG1c2 (D142 point 7): a session language for a repository or a workspace, a code of the table in any case, or cleared.
    [InlineData("language", "engine", null, "zh")]
    [InlineData("language", null, "work", "en")]
    [InlineData("language", "engine", null, "ZH")]
    [InlineData("language", null, "work", "--clear")]
    // REVIEWENV1a (D154 point 2): a review rule for a repository or a workspace, as `daoris driver review` takes its words, a
    // quoted command kept whole whatever it holds; the rule, the registry and the procedure are the driver's to judge.
    [InlineData("review", "storefront", null, "dev --kind local --procedure README.md --address http://localhost:4200 --required")]
    [InlineData("review", null, "work", "local --kind local --procedure README.md --address http://localhost:4200 --run \"npm run serve -- --port 4300\"")]
    [InlineData("review", "media-api", null, "none")]
    [InlineData("review", "storefront", null, "--drop dev")]
    [InlineData("review", null, "work", "--not-required")]
    [InlineData("review", "storefront", null, "--clear")]
    // XAGENT1a (D155 point 3): a second-opinion rule for a repository or a workspace, as `daoris driver opinion` takes its
    // words, a quoted list kept whole; the rule and the registry are the driver's to judge.
    [InlineData("opinion", "web-app", null, "--reviewers codex-acp,dsh --on landing,steps --required --verify --minutes 30")]
    [InlineData("opinion", null, "work", "--reviewers \"codex-acp, dsh\" --no-recheck")]
    [InlineData("opinion", "web-app", null, "--not-required --no-verify --recheck")]
    [InlineData("opinion", "notes-site", null, "none")]
    [InlineData("opinion", "web-app", null, "--clear")]
    public void Across_a_cap_and_an_adapter_are_written_as_the_terminal_spells_them(string door, string? target, string? workspace, string value)
    {
        var (id, _) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "the person asked", session: "h1", Now);

        Assert.NotNull(id);
        var file = Written(id!);
        Assert.Equal(door, file.GetProperty("door").GetString());
        Assert.Equal(value, file.GetProperty("value").GetString());
    }

    /// <summary>HELP10: a retry names its quest as the target, and nothing else; whether it is parked is the driver's to judge.</summary>
    [Fact]
    public void A_retry_is_written_with_its_quest_as_the_target()
    {
        var (id, _) = Box().ProposeSetting(new SettingChange("retry", "#q1a2b3c4", null, null), "the person asked", session: "h1", Now);

        var file = Written(id!);
        Assert.Equal("retry", file.GetProperty("door").GetString());
        Assert.Equal("#q1a2b3c4", file.GetProperty("target").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("value").ValueKind);
    }

    /// <summary>HELP9: the connector's tool names every door the box takes, so the helper is told of each.</summary>
    [Fact]
    public void The_tool_names_every_door()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        var door = method.GetParameters().Single(parameter => parameter.Name == "door")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        foreach (var name in HelpProposalBox.Doors)
        {
            Assert.Contains(name, door);
            Assert.Contains(name, tool);
        }
    }

    /// <summary>
    /// SESSUX1b2: `retry` releases a quest the person's stop holds as well as one its failed sessions parked (D126 §3.4),
    /// so the tool and its target say both, as the room lists both.
    /// </summary>
    [Fact]
    public void The_tool_says_retry_takes_a_held_quest_as_well_as_a_parked_one()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        var target = method.GetParameters().Single(parameter => parameter.Name == "target")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`retry` takes a quest parked by its failed sessions, or held by the person's stop", tool);
        Assert.Contains("for retry, the quest's id, as the room lists the parked and the held ones", target);
    }

    /// <summary>
    /// LANG1c2 (D142 points 7–8): the tool tells the helper what a session language takes, and that it is the work's, never
    /// the window's, so it proposes the codes the box takes rather than learning them from a refusal.
    /// </summary>
    [Fact]
    public void The_tool_says_a_language_takes_a_code_of_the_table_or_a_clear_and_is_not_the_windows()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        string Described(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`en` or `zh`, or `--clear` (language)", Described("value"));
        Assert.Contains("a language", Described("target"));
        Assert.Contains("a language", Described("workspace"));
        Assert.Contains("never the window's", tool);
    }

    /// <summary>
    /// REVIEWENV1a (D154 points 1–2, design §1.7): the tool tells the helper what a review takes, that production is never
    /// one, that `--required` is the person's say-so, and that nothing reads it yet, so it proposes the shape the box takes.
    /// </summary>
    [Fact]
    public void The_tool_says_what_a_review_takes_that_production_is_never_one_and_that_nothing_reads_it_yet()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        string Described(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`<environment> --kind local|deployed --procedure <path> [--address <url>]", Described("value"));
        Assert.Contains("a review", Described("target"));
        Assert.Contains("a review", Described("workspace"));
        Assert.Contains("never production", tool);
        Assert.Contains("propose it only when they ask", tool);
        Assert.Contains("declared only, nothing reads it yet", tool);
    }

    /// <summary>
    /// XAGENT1a (D155 point 3, design §2.4–§2.5): the tool tells the helper what an opinion takes, that its reviewers are only
    /// the ones the person names, that `--required` and `--verify` are the person's say-so, and that nothing reads it yet.
    /// </summary>
    [Fact]
    public void The_tool_says_what_an_opinion_takes_that_its_reviewers_are_the_persons_and_that_nothing_reads_it_yet()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        string Described(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`--reviewers <adapter,adapter> [--on landing,steps]", Described("value"));
        Assert.Contains("opinion", Described("door"));
        Assert.Contains("a second opinion", Described("target"));
        Assert.Contains("a second opinion", Described("workspace"));
        Assert.Contains("only the agents the person names", tool);
        Assert.Contains("`opinion` declares which other agent reads", tool);
        Assert.Contains("declared only, nothing reads it yet", tool);
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
    // LANDSVC1: a merge carrying the switch is no shape the box takes, and its sentence says the switch rides a branch.
    [InlineData("landing", "engine", null, "merge --auto-accept", "`branch <pattern>` (with `--tidy`")]
    [InlineData("landing", "engine", null, "merge --auto-accept", "`--auto-accept` for a quest's done")]
    [InlineData("intake", null, null, null, "an agent, or `off`")]
    [InlineData("strikes", null, null, "many", "a whole number")]
    [InlineData("timeout", null, null, "0", "a whole number of minutes, 1 or more")]
    [InlineData("notify", null, null, "loud", "`on` or `off`")]
    [InlineData("across", null, null, "read off", "a repository or a workspace")]
    [InlineData("across", "engine", "work", "read off", "a repository or a workspace")]
    [InlineData("across", "engine", null, "read maybe", "`read on|off|--clear`")]
    [InlineData("across", null, "work", "write-to engine", "declared from one repository")]
    [InlineData("across", "plugins", null, "write-to", "`write-to <other>`")]
    [InlineData("across", "plugins", null, "write-to two words", "`write-to <other>`")]
    [InlineData("across", "engine", null, "peek", "`read on|off|--clear` or `write-to <other> [--clear]`")]
    [InlineData("cap", null, null, "0", "a whole number, 1 or more")]
    [InlineData("adapter", null, null, null, "an agent")]
    [InlineData("adapter", null, null, "two words", "an agent")]
    [InlineData("standing", null, null, "dev only", "names the repository it holds for")]
    [InlineData("standing", "engine", "work", "dev only", "names the repository it holds for")]
    [InlineData("standing", "engine", null, null, "the person's words, or `--clear`")]
    [InlineData("standing", "engine", null, "  ", "the person's words, or `--clear`")]
    // LANG1c2: exactly one of a repository or a workspace, and a code the table holds or `--clear`.
    [InlineData("language", null, null, "zh", "session language is set for a repository or a workspace — name exactly one")]
    [InlineData("language", "engine", "work", "zh", "session language is set for a repository or a workspace — name exactly one")]
    [InlineData("language", "engine", null, null, "`language` is set to `en` or `zh`, or `--clear`")]
    [InlineData("language", null, "work", "fr", "`language` is set to `en` or `zh`, or `--clear`")]
    [InlineData("language", "engine", null, "en zh", "`language` is set to `en` or `zh`, or `--clear`")]
    // REVIEWENV1a: exactly one of a repository or a workspace, some words, and only the flags `daoris driver review` takes.
    [InlineData("review", null, null, "none", "review rule is set for a repository or a workspace — name exactly one")]
    [InlineData("review", "storefront", "work", "none", "review rule is set for a repository or a workspace — name exactly one")]
    [InlineData("review", "storefront", null, null, "review is `<environment> --kind local|deployed --procedure <path>")]
    [InlineData("review", "storefront", null, "dev --kind local --colour blue", "`none`, `--drop <environment>`, `--required|--not-required` or `--clear`")]
    // XAGENT1a: exactly one of a repository or a workspace, some words, and only the flags `daoris driver opinion` takes.
    [InlineData("opinion", null, null, "none", "second-opinion rule is set for a repository or a workspace — name exactly one")]
    [InlineData("opinion", "web-app", "work", "--clear", "second-opinion rule is set for a repository or a workspace — name exactly one")]
    [InlineData("opinion", "web-app", null, null, "opinion is `--reviewers <adapter,adapter> [--on landing,steps]")]
    [InlineData("opinion", "web-app", null, "  ", "opinion is `--reviewers <adapter,adapter> [--on landing,steps]")]
    [InlineData("opinion", "web-app", null, "--reviewers dsh --colour blue", "`none` or `--clear`")]
    [InlineData("retry", null, null, null, "names the quest its failed sessions parked or the person's stop holds")]
    [InlineData("retry", "q1 q2", null, null, "names the quest its failed sessions parked or the person's stop holds")]
    [InlineData("retry", "q1a2b3c4", "work", null, "names the quest its failed sessions parked or the person's stop holds")]
    [InlineData("retry", "q1a2b3c4", null, "--at 2", "names the quest its failed sessions parked or the person's stop holds")]
    public void A_setting_that_is_no_door_s_shape_is_refused_with_nothing_written(
        string door, string? target, string? workspace, string? value, string says)
    {
        var (id, message) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
