using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What an agent Daoris starts may do (PERM1, D72): Daoris's rules in three scopes of its own, one file
/// under the home, unioned with its defaults and handed to the harness at spawn as one more scope.
/// </summary>
/// <remarks>
/// <para>It is the driver's half of a TWIN CONTRACT. The CLI's `permissions.ts` reads and writes the same
/// file by the same rules, because the two artefacts share no code — THE FILE is the contract. The rules
/// asserted here are asserted there too, and the defaults tables are held together by a test that reads
/// the other side's source.</para>
///
/// <para>Precedence is deliberately NOT tested here, because it is not Daoris's: the harness merges the
/// scopes it is handed with its own, and `deny` beats `ask` beats `allow` there. What Daoris owns is the
/// union, and that is what these assert.</para>
/// </remarks>
public sealed class PermissionRulesTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-permissions-" + Guid.NewGuid().ToString("N")[..8]);

    public PermissionRulesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly string[] Connector =
    [
        "mcp__daoris-knowledge__registry",
        "mcp__daoris-knowledge__knowledge_search",
        "mcp__daoris-knowledge__knowledge_get",
        "mcp__daoris-knowledge__knowledge_repositories",
        "mcp__daoris-knowledge__knowledge_convergence",
        "mcp__daoris-knowledge__quest_list",
        "mcp__daoris-knowledge__quest_respond",
        "mcp__daoris-knowledge__quest_publish",
    ];

    /// <summary>What the `commit` default allows (PERM4).</summary>
    private static readonly string[] Commit = ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)"];

    /// <summary>Everything the defaults allow, in the order they are handed.</summary>
    private static readonly string[] Allowed = [.. Connector, .. Commit];

    // ——— The defaults.

    /// <summary>
    /// A machine that wrote nothing still hands every session Daoris's defaults: the connector's own
    /// tools allowed (INT3b), because an ask is a refusal for anything Daoris starts, and a push denied
    /// (D37), structurally rather than by a script's exit code.
    /// </summary>
    /// <summary>
    /// Whether the connector reaches a session, which is what decides whether the driver holds for
    /// trust (D73): allowed by the tool, the server or its wildcard, and asked or denied by none.
    /// </summary>
    [Fact]
    public void The_connector_reaches_a_session_only_when_allowed_and_never_asked_or_denied()
    {
        const string respond = "mcp__daoris-knowledge__quest_respond";

        Assert.True(PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine"), "quest_respond"));
        Assert.True(PermissionRules.AllowsConnector(new RuleLists(["mcp__daoris-knowledge"], [], []), "quest_respond"));
        Assert.True(PermissionRules.AllowsConnector(new RuleLists(["mcp__daoris-knowledge__*"], [], []), "quest_respond"));
        Assert.False(PermissionRules.AllowsConnector(RuleLists.Empty, "quest_respond"));
        Assert.False(PermissionRules.AllowsConnector(new RuleLists([respond], [respond], []), "quest_respond"));
        Assert.False(PermissionRules.AllowsConnector(new RuleLists([respond], [], ["mcp__daoris-knowledge"]), "quest_respond"));
        Assert.False(PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.SwitchDefault(PermissionRules.Load(_home), "connector", on: false), "default", "engine"),
            "quest_respond"));
    }

    [Fact]
    public void Nothing_written_hands_the_defaults_alone()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");

        Assert.Equal(Allowed, rules.Allow);
        Assert.Equal(["Bash(git push)", "Bash(git push:*)"], rules.Deny);
        Assert.Empty(rules.Ask);
    }

    /// <summary>
    /// 🔴 Rebuilding the index is the machine's job, never a session's: the connector's refresh is the
    /// one tool of its server the default does not allow.
    /// </summary>
    [Fact]
    public void The_connector_default_does_not_allow_rebuilding_the_index()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");

        Assert.DoesNotContain("mcp__daoris-knowledge__knowledge_refresh", rules.Allow);
        Assert.DoesNotContain("mcp__daoris-knowledge", rules.Allow);
    }

    [Fact]
    public void A_default_switched_off_is_not_handed_and_the_other_still_is()
    {
        var file = PermissionRules.SwitchDefault(PermissionRules.Load(_home), "no-push", on: false);

        var rules = PermissionRules.Compose(file, "default", "engine");

        Assert.Empty(rules.Deny);
        Assert.Equal(Allowed, rules.Allow);
        Assert.Equal(["no-push"], file.DefaultsOff);

        var back = PermissionRules.Compose(PermissionRules.SwitchDefault(file, "no-push", on: true), "default", "engine");
        Assert.Contains("Bash(git push:*)", back.Deny);
    }

    [Fact]
    public void Switching_a_default_nobody_shipped_is_refused_naming_the_ones_that_exist()
    {
        var refused = Assert.Throws<DriverException>(
            () => PermissionRules.SwitchDefault(PermissionRules.Load(_home), "no-rm", on: false));

        Assert.Contains("no-rm", refused.Message);
        Assert.Contains("connector", refused.Message);
        Assert.Contains("commit", refused.Message);
        Assert.Contains("no-push", refused.Message);
        Assert.Contains(PermissionRules.TreeGuardId, refused.Message);
    }

    // ——— The scopes.

    /// <summary>
    /// The union a session is handed: the defaults, the machine's rules, its own circle's and its own
    /// repository's — and nothing from a circle or a repository it is not in.
    /// </summary>
    [Fact]
    public void Only_the_sessions_own_scopes_are_handed()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Bash(npm run test:*)");
        file = PermissionRules.Add(file, RuleScope.Workspace, "default", RuleList.Deny, "Bash(rm -rf:*)");
        file = PermissionRules.Add(file, RuleScope.Workspace, "team", RuleList.Deny, "WebFetch");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Ask, "Edit(/docs/**)");
        file = PermissionRules.Add(file, RuleScope.Repository, "game", RuleList.Allow, "Bash(make:*)");

        var rules = PermissionRules.Compose(file, "default", "engine");

        Assert.Contains("Bash(npm run test:*)", rules.Allow);
        Assert.Contains("Bash(rm -rf:*)", rules.Deny);
        Assert.Equal(["Edit(/docs/**)"], rules.Ask);
        Assert.DoesNotContain("WebFetch", rules.Deny);
        Assert.DoesNotContain("Bash(make:*)", rules.Allow);
    }

    /// <summary>An intake serves an ask in a circle, and belongs to no repository.</summary>
    [Fact]
    public void A_session_of_no_repository_is_handed_its_circles_rules_and_no_repositorys()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Workspace, "default", RuleList.Allow, "WebFetch(domain:tickets.example)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Allow, "Bash(make:*)");

        var rules = PermissionRules.Compose(file, null, null);

        Assert.Contains("WebFetch(domain:tickets.example)", rules.Allow);
        Assert.DoesNotContain("Bash(make:*)", rules.Allow);
    }

    [Fact]
    public void A_rule_held_in_two_scopes_is_handed_once_in_the_order_first_met()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Bash(make:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Allow, "Bash(make:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Allow, "mcp__daoris-knowledge__quest_list");

        var rules = PermissionRules.Compose(file, "default", "engine");

        Assert.Single(rules.Allow, "Bash(make:*)");
        Assert.Single(rules.Allow, "mcp__daoris-knowledge__quest_list");
        Assert.Equal(Allowed.Length + 1, rules.Allow.Count);
    }

    /// <summary>One place per scope: a rule added to a list leaves the scope's other lists.</summary>
    [Fact]
    public void Adding_a_rule_moves_it_within_its_scope_and_removing_it_takes_it_from_every_list()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "WebFetch");
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Deny, "WebFetch");

        Assert.DoesNotContain("WebFetch", file.Machine.Allow);
        Assert.Equal(["WebFetch"], file.Machine.Deny);

        file = PermissionRules.Remove(file, RuleScope.Machine, null, "WebFetch");
        Assert.True(file.Machine.IsEmpty);
    }

    [Theory]
    [InlineData(RuleScope.Workspace)]
    [InlineData(RuleScope.Repository)]
    public void A_circle_or_repository_scope_needs_its_name(RuleScope scope)
    {
        var refused = Assert.Throws<DriverException>(
            () => PermissionRules.Add(PermissionRules.Load(_home), scope, "  ", RuleList.Allow, "WebFetch"));

        Assert.Contains("name", refused.Message);
    }

    // ——— A rule is the harness's own shape.

    [Theory]
    [InlineData("WebFetch")]
    [InlineData("Bash(npm run test:*)")]
    [InlineData("Edit(/src/**)")]
    [InlineData("mcp__daoris-knowledge__quest_list")]
    [InlineData("WebFetch(domain:example.com)")]
    public void A_tool_name_with_an_optional_specifier_is_a_rule(string rule)
    {
        Assert.Null(PermissionRules.Refusal(rule));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bash(")]
    [InlineData("rm -rf /")]
    [InlineData("(npm test)")]
    [InlineData("Bash(npm test)\nWebFetch")]
    public void Anything_else_is_refused_naming_the_shape(string rule)
    {
        var refused = PermissionRules.Refusal(rule);

        Assert.NotNull(refused);
        Assert.Contains("Bash(npm run test:*)", refused);
        Assert.Throws<DriverException>(
            () => PermissionRules.Add(PermissionRules.Load(_home), RuleScope.Machine, null, RuleList.Allow, rule));
    }

    // ——— The file.

    /// <summary>
    /// Written beside and renamed over, BOM-less LF — and a key a newer build wrote is kept, so an
    /// older build editing one rule never deletes another build's setting.
    /// </summary>
    [Fact]
    public void The_file_round_trips_and_keeps_what_a_newer_build_wrote()
    {
        File.WriteAllText(PermissionRules.PathOf(_home), """{"future":{"x":1},"defaultsOff":["connector"]}""");

        var file = PermissionRules.Add(
            PermissionRules.Load(_home), RuleScope.Repository, "engine", RuleList.Deny, "Bash(rm:*)");
        PermissionRules.Save(_home, file);

        var bytes = File.ReadAllBytes(PermissionRules.PathOf(_home));
        Assert.NotEqual(0xEF, bytes[0]);
        var text = File.ReadAllText(PermissionRules.PathOf(_home));
        Assert.DoesNotContain("\r", text);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(1, document.RootElement.GetProperty("future").GetProperty("x").GetInt32());
        Assert.Equal("Bash(rm:*)", document.RootElement.GetProperty("repositories").GetProperty("engine").GetProperty("deny")[0].GetString());

        var read = PermissionRules.Load(_home);
        Assert.Equal(["connector"], read.DefaultsOff);
        Assert.Equal(["Bash(rm:*)"], read.Repositories["engine"].Deny);
        Assert.Null(read.Problem);
    }

    /// <summary>
    /// Wiring never stops a spawn (D21): a file that is not JSON reads as empty, says why, and the
    /// defaults are still handed — the safe direction, since the guard is one of them.
    /// </summary>
    [Fact]
    public void A_file_that_is_not_json_reads_as_empty_says_why_and_the_defaults_still_hold()
    {
        File.WriteAllText(PermissionRules.PathOf(_home), "{ not json");

        var file = PermissionRules.Load(_home);

        Assert.NotNull(file.Problem);
        Assert.Contains(PermissionRules.FileName, file.Problem);
        Assert.Contains("Bash(git push:*)", PermissionRules.Compose(file, "default", "engine").Deny);
    }

    // ——— What the harness is handed.

    /// <summary>
    /// The harness's own settings shape, one file per session under the home — beside the servers file
    /// the same session is handed — and gone when the session is.
    /// </summary>
    [Fact]
    public void The_spawn_file_is_the_harness_settings_shape_under_the_home()
    {
        var rules = PermissionRules.Compose(
            PermissionRules.Add(PermissionRules.Load(_home), RuleScope.Machine, null, RuleList.Ask, "WebFetch"),
            "default", "engine");

        var path = SpawnSettings.Write(_home, "s1", rules);

        Assert.Equal(Path.Combine(_home, SpawnServers.Folder, "s1.settings.json"), path);
        var text = File.ReadAllText(path!);
        Assert.DoesNotContain("\r", text);
        using var document = JsonDocument.Parse(text);
        var permissions = document.RootElement.GetProperty("permissions");
        Assert.Equal(
            [.. Connector, "Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)"],
            permissions.GetProperty("allow").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["WebFetch"], permissions.GetProperty("ask").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("Bash(git push)", permissions.GetProperty("deny").EnumerateArray().Select(e => e.GetString()));

        SpawnSettings.Remove(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Nothing_to_hand_writes_no_file()
    {
        var file = Off(PermissionRules.Load(_home), "connector", "commit", "no-push");

        Assert.Null(SpawnSettings.Write(_home, "s1", PermissionRules.Compose(file, "default", "engine")));
    }

    // ——— PERM4: a driven session may commit.

    /// <summary>
    /// 🔴 The owner's answer to PERM4 (2026-09-24), from a measured failure: in a folder the agent had
    /// never trusted, ACP2's real session took its quest, made the edit, was refused `git commit` —
    /// the repository's own allow-list does not apply there — and declined. D37 makes a local commit
    /// automatic and a push the person's, so Daoris ships the commit and `no-push` still refuses the push.
    /// </summary>
    [Fact]
    public void Daoris_ships_a_commit_default_and_the_person_can_switch_it_off()
    {
        string[] commit = ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)"];

        var on = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");
        foreach (var rule in commit) Assert.Contains(rule, on.Allow);
        Assert.Contains("Bash(git push:*)", on.Deny);

        var off = PermissionRules.Compose(Off(PermissionRules.Load(_home), "commit"), "default", "engine");
        foreach (var rule in commit) Assert.DoesNotContain(rule, off.Allow);
        Assert.Contains("mcp__daoris-knowledge__quest_respond", off.Allow);
    }

    // ——— PERM3: the tree guard, a hook rather than a rule.

    /// <summary>
    /// A rule cannot say "outside" (design §3), so the guard is a hook Daoris ships. It is a default like
    /// the others — on unless the person switches it off, by id, from either door — but it adds no rule.
    /// </summary>
    [Fact]
    public void The_tree_guard_is_a_default_the_person_can_switch_off_and_it_adds_no_rule()
    {
        var shipped = Assert.Single(PermissionRules.Defaults, d => d.Id == PermissionRules.TreeGuardId);
        Assert.Empty(shipped.Rules);
        Assert.Equal(TreeGuard.Matcher, shipped.Hook);

        Assert.True(PermissionRules.GuardsTree(PermissionRules.Load(_home)));
        Assert.False(PermissionRules.GuardsTree(Off(PermissionRules.Load(_home), PermissionRules.TreeGuardId)));
    }

    /// <summary>
    /// The harness's own hook shape, in the same file the rules ride — exec form, so the script and the
    /// tree are one argument each with no shell to quote them through (Windows' is Git Bash or
    /// PowerShell, and a path survives neither reliably).
    /// </summary>
    [Fact]
    public void The_spawn_file_carries_the_guard_as_a_pre_tool_use_hook_with_the_sessions_tree()
    {
        var tree = Path.Combine(_home, "engine");
        var guard = TreeGuard.For(_home, tree);

        var path = SpawnSettings.Write(_home, "s1", PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine"), guard);

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var entry = Assert.Single(document.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray());
        Assert.Equal("Edit|Write|MultiEdit|NotebookEdit", entry.GetProperty("matcher").GetString());
        var hook = Assert.Single(entry.GetProperty("hooks").EnumerateArray());
        Assert.Equal("command", hook.GetProperty("type").GetString());
        Assert.Equal("node", hook.GetProperty("command").GetString());
        Assert.Equal([guard.Script, tree], hook.GetProperty("args").EnumerateArray().Select(e => e.GetString()));
        Assert.True(File.Exists(guard.Script));
    }

    [Fact]
    public void No_guard_is_no_hooks_key_and_a_guard_alone_is_still_a_file()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");
        using (var plain = JsonDocument.Parse(File.ReadAllText(SpawnSettings.Write(_home, "s1", rules)!)))
        {
            Assert.False(plain.RootElement.TryGetProperty("hooks", out _));
        }

        var nothing = PermissionRules.Compose(Off(PermissionRules.Load(_home), "connector", "commit", "no-push"), "default", "engine");
        var alone = SpawnSettings.Write(_home, "s2", nothing, TreeGuard.For(_home, _home));
        Assert.NotNull(alone);
        using var guarded = JsonDocument.Parse(File.ReadAllText(alone!));
        Assert.True(guarded.RootElement.TryGetProperty("hooks", out _));
    }

    private static PermissionFile Off(PermissionFile file, params string[] ids) =>
        ids.Aggregate(file, (held, id) => PermissionRules.SwitchDefault(held, id, on: false));

    // ——— The twin.

    /// <summary>
    /// The CLI lists and switches the same defaults the driver hands, in another language — two tables
    /// with no compiler between them, held together by reading the other side's source.
    /// </summary>
    [Fact]
    public void The_defaults_are_the_clis_defaults()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Cli", "src", "permissions.ts"));

        foreach (var shipped in PermissionRules.Defaults)
        {
            Assert.Contains($"id: '{shipped.Id}'", source);
            Assert.Contains($"list: '{shipped.List.ToString().ToLowerInvariant()}'", source);
            foreach (var rule in shipped.Rules)
            {
                var tool = rule.Replace("mcp__daoris-knowledge__", "", StringComparison.Ordinal);
                Assert.Contains($"'{tool}'", source);
            }

            if (shipped.Hook is { } hook) Assert.Contains($"hook: '{hook}'", source);
        }
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
