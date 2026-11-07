using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>The <c>setting</c> kind's writer: one of the driver's doors, spelled as the CLI's verbs are.</summary>
public sealed class HelpSettingProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_setting_is_written_as_a_file_the_driver_judges_with_who_proposed_it_and_why()
    {
        var (id, message) = Box().ProposeSetting(
            new SettingChange("line", Target: null, Workspace: "work", Value: "develop"),
            "the person asked for work to start from develop", session: "h1e1p000", Now);

        Assert.NotNull(id);
        Assert.Contains($"#{id}", message);
        Assert.Contains("Apply", message);
        var file = Written(id!);
        Assert.Equal("setting", file.GetProperty("kind").GetString());
        Assert.Equal("line", file.GetProperty("door").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("target").ValueKind);
        Assert.Equal("develop", file.GetProperty("value").GetString());
        Assert.Equal("h1e1p000", file.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", file.GetProperty("state").GetString());
        Assert.Equal("the person asked for work to start from develop", file.GetProperty("why").GetString());
    }

    [Fact]
    public void The_landing_is_no_door_here()
    {
        Assert.DoesNotContain("landing", HelpSettingProposalWriter.Doors);
        Assert.Contains("is not a door", Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "merge"), "why", session: "h1e1p000", Now).Message);
    }

    /// <summary>Reading and writing across and a cap pass the shape as the terminal spells them.</summary>
    [Theory]
    [InlineData("across", "engine", null, "read off")]
    [InlineData("across", null, "work", "read --clear")]
    [InlineData("across", "plugins", null, "write-to engine")]
    [InlineData("across", "plugins", null, "write-to engine --clear")]
    [InlineData("cap", null, null, "3")]
    [InlineData("cooloff", null, null, "90")]
    [InlineData("quiet", null, null, "5")]
    [InlineData("quiet", null, null, "120")]
    // A repository's remote or a workspace's host pointed at an identity, or taken back.
    [InlineData("git-use", "engine", null, "Work")]
    [InlineData("git-use", "engine", null, "--remote upstream Work")]
    [InlineData("git-use", "engine", null, "--clear")]
    [InlineData("git-use", null, "work", "--host github.com Work")]
    // Each kind's keep rule, a kind and its rule, for this machine.
    [InlineData("keep", null, null, "set-aside 30")]
    [InlineData("keep", null, null, "set-aside whole")]
    [InlineData("keep", null, null, "downloads never")]
    [InlineData("keep", null, null, "logs default")]
    // History's age, the person's to apply.
    [InlineData("keep", null, null, "history 365")]
    [InlineData("keep", null, null, "history never")]
    // A standing answer for a repository, in the person's words, or cleared.
    [InlineData("standing", "engine", null, "dev writes allowed; test locally against dev; prod only on a yes")]
    [InlineData("standing", "engine", null, "--clear")]
    // A session language for a repository or a workspace, a code of the table in any case, or cleared.
    [InlineData("language", "engine", null, "zh")]
    [InlineData("language", null, "work", "en")]
    [InlineData("language", "engine", null, "ZH")]
    [InlineData("language", null, "work", "--clear")]
    // A review rule for a repository or a workspace, as `daoris place review` takes its words, a
    // quoted command kept whole whatever it holds; the rule, the registry and the procedure are the driver's to judge.
    [InlineData("review", "storefront", null, "dev --kind local --procedure README.md --address http://localhost:4200 --required")]
    [InlineData("review", null, "work", "local --kind local --procedure README.md --address http://localhost:4200 --run \"npm run serve -- --port 4300\"")]
    [InlineData("review", "media-api", null, "none")]
    [InlineData("review", "storefront", null, "--drop dev")]
    [InlineData("review", null, "work", "--not-required")]
    [InlineData("review", "storefront", null, "--clear")]
    // A second-opinion rule for a repository or a workspace, as `daoris place opinion` takes its
    // words, a quoted list kept whole; the rule and the registry are the driver's to judge.
    [InlineData("opinion", "web-app", null, "--agents codex-acp,dsh --on landing,steps --required --verify --minutes 30")]
    [InlineData("opinion", null, "work", "--agents \"codex-acp, dsh\" --no-recheck")]
    [InlineData("opinion", "web-app", null, "--not-required --no-verify --recheck")]
    [InlineData("opinion", "notes-site", null, "none")]
    [InlineData("opinion", "web-app", null, "--clear")]
    // A helpers rule and a commit rule for a repository or a workspace,
    // as `daoris place helpers` and `daoris place commits` take their words, a quoted list or convention kept whole; the rule and
    // the registry are the driver's to judge.
    [InlineData("helpers", "web-app", null, "--mode on --agents codex-acp,dsh")]
    [InlineData("helpers", null, "work", "--agents \"codex-acp, dsh\"")]
    [InlineData("helpers", "web-app", null, "--clear")]
    [InlineData("commits", "web-app", null, "--mode ask --convention \"Imperative subject line, at most 72 characters.\"")]
    [InlineData("commits", null, "work", "--mode at-end --no-convention")]
    [InlineData("commits", "web-app", null, "--clear")]
    public void Across_a_cap_and_an_adapter_are_written_as_the_terminal_spells_them(string door, string? target, string? workspace, string value)
    {
        var (id, _) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "the person asked", session: "h1", Now);

        Assert.NotNull(id);
        var file = Written(id!);
        Assert.Equal(door, file.GetProperty("door").GetString());
        Assert.Equal(value, file.GetProperty("value").GetString());
    }

    /// <summary>A retry names its quest as the target, and nothing else; whether it is parked is the driver's to judge.</summary>
    [Fact]
    public void A_retry_is_written_with_its_quest_as_the_target()
    {
        var (id, _) = Box().ProposeSetting(new SettingChange("retry", "#q1a2b3c4", null, null), "the person asked", session: "h1", Now);

        var file = Written(id!);
        Assert.Equal("retry", file.GetProperty("door").GetString());
        Assert.Equal("#q1a2b3c4", file.GetProperty("target").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("value").ValueKind);
    }

    /// <summary>The connector's tool names every door the box takes, so the helper is told of each.</summary>
    [Fact]
    public void The_tool_names_every_door()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
        var door = method.GetParameters().Single(parameter => parameter.Name == "door")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        foreach (var name in HelpSettingProposalWriter.Doors)
        {
            Assert.Contains(name, door);
            Assert.Contains(name, tool);
        }
    }

    /// <summary>
    /// `retry` releases a quest the person's stop holds as well as one its failed sessions parked,
    /// so the tool and its target say both, as the room lists both.
    /// </summary>
    [Fact]
    public void The_tool_says_retry_takes_a_held_quest_as_well_as_a_parked_one()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
        var target = method.GetParameters().Single(parameter => parameter.Name == "target")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`retry` takes a quest parked by its failed sessions, or held by the person's stop", tool);
        Assert.Contains("for retry, the quest's id, as the room lists the parked and the held ones", target);
    }

    /// <summary>
    /// The tool tells the helper what a session language takes, and that it is the work's, never
    /// the window's, so it proposes the codes the box takes rather than learning them from a refusal.
    /// </summary>
    [Fact]
    public void The_tool_says_a_language_takes_a_code_of_the_table_or_a_clear_and_is_not_the_windows()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
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
    /// What every door says after a review rule's own sentences, in the gate's words: each sentence of the shared table's
    /// <c>gate</c> rows, <c>src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/review-rules.json</c>, which the driver's
    /// <c>ReviewRulesTests</c> reads too. Read from the table, never spelled here, so
    /// the tool's copy cannot drift from the twins' (REVIEWENV1c4).
    /// </summary>
    private static IReadOnlyList<string> GateSays()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "review-rules.json")));
        return Sentences(table);
    }

    /// <summary>
    /// What every door says after a second-opinion rule's own sentences, in the gate's words (XAGENT1f4): each sentence of the
    /// shared table's <c>gate</c> rows, <c>src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json</c>, which
    /// the driver's <c>OpinionRulesTests</c> reads too, read as REVIEWENV1c4 reads the
    /// review's: never spelled here.
    /// </summary>
    private static IReadOnlyList<string> OpinionGateSays()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "opinion-rules.json")));
        return Sentences(table);
    }

    /// <summary>Each sentence of a shared table's <c>gate</c> rows, once, in the order first said.</summary>
    private static IReadOnlyList<string> Sentences(JsonDocument table) =>
    [
        .. table.RootElement.GetProperty("gate").EnumerateArray()
            .SelectMany(row => row[2].EnumerateArray()).Select(sentence => sentence.GetString()!).Distinct(),
    ];

    /// <summary>The workspace root, found by walking up from the test binaries to <c>daoris.json</c>.</summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }

    /// <summary>
    /// The tool tells the helper what a review takes,
    /// that production is never one, that `--required` is the person's say-so, and what the landing's gate then does with
    /// work that waits, in the sentence every other door says, the twins' table's. Never that nothing reads
    /// it: the gate does.
    /// </summary>
    [Fact]
    public void The_tool_says_what_a_review_takes_that_production_is_never_one_and_what_the_gate_does()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
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

        // The review's clause runs from its door's name to the opinion's.
        var review = tool[tool.IndexOf("`review` declares", StringComparison.Ordinal)..tool.IndexOf("`opinion` declares", StringComparison.Ordinal)];
        var gate = GateSays();
        Assert.NotEmpty(gate);
        foreach (var sentence in gate) Assert.Contains(sentence, review);
        Assert.DoesNotContain("declared only", review, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nothing reads it", review, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The tool tells the helper what an opinion takes, that
    /// its reviewers are only the ones the person names, that `--required` and `--verify` are the person's say-so, and what the
    /// gate then does with work that waits, in the sentences every other door says, the twins' table's. Never that nothing
    /// reads it: the gate does.
    /// </summary>
    [Fact]
    public void The_tool_says_what_an_opinion_takes_that_its_reviewers_are_the_persons_and_what_the_gate_does()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
        string Described(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`--agents <adapter,adapter> [--on landing,steps]", Described("value"));
        Assert.Contains("opinion", Described("door"));
        Assert.Contains("a second opinion", Described("target"));
        Assert.Contains("a second opinion", Described("workspace"));
        Assert.Contains("only the agents the person names", tool);
        Assert.Contains("`opinion` declares which other agent reads", tool);

        // The opinion's clause runs from its door's name to the next door's.
        var opinion = tool[tool.IndexOf("`opinion` declares", StringComparison.Ordinal)..tool.IndexOf("`retry` takes", StringComparison.Ordinal)];
        var gate = OpinionGateSays();
        Assert.NotEmpty(gate);
        foreach (var sentence in gate) Assert.Contains(sentence, opinion);
        Assert.DoesNotContain("declared only", tool, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nothing reads it", tool, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The tool tells the helper what a helpers rule and a commit rule take, that it may
    /// seed either, and a landing's branch name, from the repository's own instructions for the person to apply, and that a
    /// landing's key and squash are kept unless the proposal names them, so it never learns them from a refusal.
    /// </summary>
    [Fact]
    public void The_tool_says_what_the_helpers_and_commit_rules_take_and_that_a_repositorys_text_only_seeds_them()
    {
        var method = typeof(Daoris.Knowledge.Mcp.HelpSettingTool).GetMethod(nameof(Daoris.Knowledge.Mcp.HelpSettingTool.ProposeSetting))!;
        string Described(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        Assert.Contains("`--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear` (helpers)", Described("value"));
        Assert.Contains("`--mode as-you-go|ask|at-end` and/or `--convention \"<words>\"` or `--no-convention`, or `--clear` (commits)", Described("value"));
        Assert.Contains("a helpers rule or a commit rule", Described("target"));
        Assert.Contains("a helpers rule or a commit rule", Described("workspace"));

        Assert.Contains("`helpers` sets what a helper an agent asks for does", tool);
        Assert.Contains("`commits` sets how a repository's or a workspace's work is committed", tool);
        Assert.Contains("from the repository's own instructions", tool);
        Assert.Contains("never the process until the person applies it", tool);
    }

    /// <summary>The shape is checked here, and nothing more: what the route would say is the driver's.</summary>
    [Theory]
    [InlineData("push", "engine", null, null, "is not a door")]
    [InlineData("drive", null, null, null, "names the repository")]
    [InlineData("trees", "engine", null, "sometimes", "`on` or `off`")]
    [InlineData("line", null, null, "develop", "a repository or a workspace")]
    [InlineData("line", "engine", "work", "develop", "a repository or a workspace")]
    [InlineData("line", "engine", null, null, "a branch, or `--clear`")]
    [InlineData("notify", null, null, "loud", "`on` or `off`")]
    [InlineData("across", null, null, "read off", "a repository or a workspace")]
    [InlineData("across", "engine", "work", "read off", "a repository or a workspace")]
    [InlineData("across", "engine", null, "read maybe", "`read on|off|--clear`")]
    [InlineData("across", null, "work", "write-to engine", "declared from one repository")]
    [InlineData("across", "plugins", null, "write-to", "`write-to <other>`")]
    [InlineData("across", "plugins", null, "write-to two words", "`write-to <other>`")]
    [InlineData("across", "engine", null, "peek", "`read on|off|--clear` or `write-to <other> [--clear]`")]
    [InlineData("cap", null, null, "0", "a whole number, 1 or more")]
    [InlineData("cooloff", null, null, "0", "a whole number of minutes, 1 or more")]
    [InlineData("cooloff", null, null, "soon", "a whole number of minutes, 1 or more")]
    [InlineData("quiet", null, null, "4", "a whole number of minutes from 5 to 120")]
    [InlineData("quiet", null, null, "121", "a whole number of minutes from 5 to 120")]
    [InlineData("quiet", null, null, "soon", "a whole number of minutes from 5 to 120")]
    [InlineData("git-use", null, null, "Work", "name exactly one")]
    [InlineData("git-use", "engine", "work", "Work", "name exactly one")]
    [InlineData("git-use", "engine", null, "", "`git-use` is an identity's label or `--clear`")]
    [InlineData("git-use", "engine", null, "--machine Work", "`git-use` is an identity's label or `--clear`")]
    [InlineData("keep", null, null, "trees 30", "`keep` names a kind and its rule")]
    [InlineData("keep", null, null, "set-aside", "`keep` names a kind and its rule")]
    [InlineData("keep", "engine", null, "set-aside 30", "for this machine")]
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
    // Exactly one of a repository or a workspace, some words, and only the flags `daoris place review` takes.
    [InlineData("review", null, null, "none", "review rule is set for a repository or a workspace — name exactly one")]
    [InlineData("review", "storefront", "work", "none", "review rule is set for a repository or a workspace — name exactly one")]
    [InlineData("review", "storefront", null, null, "review is `<environment> --kind local|deployed --procedure <path>")]
    [InlineData("review", "storefront", null, "dev --kind local --colour blue", "`none`, `--drop <environment>`, `--required|--not-required` or `--clear`")]
    // Exactly one of a repository or a workspace, some words, and only the flags `daoris place opinion` takes.
    [InlineData("opinion", null, null, "none", "second-opinion rule is set for a repository or a workspace — name exactly one")]
    [InlineData("opinion", "web-app", "work", "--clear", "second-opinion rule is set for a repository or a workspace — name exactly one")]
    [InlineData("opinion", "web-app", null, null, "opinion is `--agents <adapter,adapter> [--on landing,steps]")]
    [InlineData("opinion", "web-app", null, "  ", "opinion is `--agents <adapter,adapter> [--on landing,steps]")]
    [InlineData("opinion", "web-app", null, "--agents dsh --colour blue", "`none` or `--clear`")]
    // Exactly one of a repository or a workspace, some words, and only the flags `daoris place helpers` and
    // `daoris place commits` take; a merge carrying a key or a squash is no shape the box takes, and its sentence names both.
    [InlineData("helpers", null, null, "--mode on", "helpers rule is set for a repository or a workspace — name exactly one")]
    [InlineData("helpers", "web-app", "work", "--clear", "helpers rule is set for a repository or a workspace — name exactly one")]
    [InlineData("helpers", "web-app", null, null, "`helpers` is `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear`")]
    [InlineData("helpers", "web-app", null, "--mode on --on landing", "`helpers` is `--mode ask|on|off`")]
    [InlineData("commits", null, null, "--mode ask", "commit rule is set for a repository or a workspace — name exactly one")]
    [InlineData("commits", "web-app", "work", "--clear", "commit rule is set for a repository or a workspace — name exactly one")]
    [InlineData("commits", "web-app", null, "  ", "`commits` is `--mode as-you-go|ask|at-end` and/or `--convention \"<words>\"` or `--no-convention`, or `--clear`")]
    [InlineData("commits", "web-app", null, "--mode ask --squash", "`commits` is `--mode as-you-go|ask|at-end`")]
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
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
