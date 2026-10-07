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
        "mcp__daoris-knowledge__permission_propose",
        "mcp__daoris-knowledge__go_ahead_ask",
    ];

    /// <summary>What the `commit` default allows (PERM4), a rename among it (UNBLOCK4, D122 §3.6).</summary>
    private static readonly string[] Commit = ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)", "Bash(git mv:*)"];

    /// <summary>
    /// What the `no-push` default denies: a push as written, and with options before its subcommand, the
    /// forms the harness's maker says a `git push` rule does not stop (UNBLOCK4, D122 §3.7).
    /// </summary>
    private static readonly string[] NoPush = ["Bash(git push)", "Bash(git push:*)", "Bash(git -* push)", "Bash(git -* push *)"];

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

    /// <summary>
    /// A session may read the folder its own quest's or ask's files are kept in (INT4j), and the rule
    /// is written in the harness's own absolute form: `//` then the path in POSIX form, a Windows drive
    /// lower-cased (`C:\x` → `/c/x`), because that is what Claude Code compares a Read's target against
    /// on Windows — and an allow is compared case-sensitively there, so nothing else is re-cased.
    /// </summary>
    [Fact]
    public void A_kept_folder_reads_as_the_harness_own_absolute_rule()
    {
        Assert.Equal("Read(//c/somewhere/data/asks/a1b2c3/**)", PermissionRules.ReadRule(@"C:\somewhere\data\asks\a1b2c3"));
        Assert.Equal("Read(//c/somewhere/data/asks/a1b2c3/**)", PermissionRules.ReadRule("C:/somewhere/data/asks/a1b2c3/"));
        Assert.Equal("Read(//d/Games/Daoris/data/quests/q1/attachments/**)",
            PermissionRules.ReadRule(@"D:\Games\Daoris\data\quests\q1\attachments"));
        Assert.Equal("Read(//srv/daoris/asks/a1/**)", PermissionRules.ReadRule("/srv/daoris/asks/a1"));
    }

    /// <summary>D107: what a session reaches across joins the person's rules, each rule once, theirs first.</summary>
    [Fact]
    public void Two_sets_of_lists_join_each_rule_once_in_the_order_first_met()
    {
        var person = new RuleLists(["Bash(make:*)", "Read(//a/**)"], ["WebFetch"], ["Bash(rm:*)"]);
        var across = new RuleLists(["Read(//a/**)", "Read(//b/**)"], [], ["Edit(//b/**)"]);

        var joined = person.Joined(across);

        Assert.Equal(["Bash(make:*)", "Read(//a/**)", "Read(//b/**)"], joined.Allow);
        Assert.Equal(["WebFetch"], joined.Ask);
        Assert.Equal(["Bash(rm:*)", "Edit(//b/**)"], joined.Deny);
    }

    /// <summary>D107: an edit of another checkout is written in the same absolute form as a read.</summary>
    [Fact]
    public void A_checkouts_edit_is_the_same_absolute_rule_as_its_read()
    {
        Assert.Equal("Edit(//c/work/engine/**)", PermissionRules.EditRule(@"C:\work\engine\"));
        Assert.Equal("Edit(//srv/engine/**)", PermissionRules.EditRule("/srv/engine"));
        Assert.Null(PermissionRules.Refusal(PermissionRules.EditRule(@"D:\my work\engine")));
    }

    [Fact]
    public void Nothing_written_hands_the_defaults_alone()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");

        Assert.Equal(Allowed, rules.Allow);
        Assert.Equal(NoPush, rules.Deny);
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

    /// <summary>
    /// 🔴 REV3: a scope is a name as a person writes it, and every other layer — the registry, the planner,
    /// the quest's `to` — matches names case-insensitively. Here they were keyed ordinally, so a quest to
    /// `engine` spawned a session handed none of `Engine`'s denies: a widening nobody chose.
    /// </summary>
    [Fact]
    public void A_scope_named_in_another_case_still_reaches_its_session_and_an_edit_keeps_one_scope()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Repository, "Engine", RuleList.Deny, "Bash(rm:*)");
        file = PermissionRules.Add(file, RuleScope.Workspace, "Aurora", RuleList.Deny, "WebFetch");

        var rules = PermissionRules.Compose(file, "aurora", "engine");
        Assert.Contains("Bash(rm:*)", rules.Deny);
        Assert.Contains("WebFetch", rules.Deny);

        // Edited in another case, it is still the one scope, under the spelling it already had.
        file = PermissionRules.Add(file, RuleScope.Repository, "ENGINE", RuleList.Deny, "Bash(dd:*)");
        Assert.Equal(["Engine"], file.Repositories.Keys);
        Assert.Equal(["Bash(rm:*)", "Bash(dd:*)"], file.Repositories["Engine"].Deny);
    }

    /// <summary>
    /// CASEFOLD1d: a scope is one in another case only as <c>OrdinalIgnoreCase</c> finds it, each letter to its one capital,
    /// as the CLI's <c>composeRules</c> and <c>addRule</c> find it through <c>casefold.ts</c> (<c>permissions.test.ts</c> names
    /// this behaviour): a name full case mapping would widen or lower to the same letters is another scope, neither handed
    /// with it nor written into it, and a final sigma is the sigma it is.
    /// </summary>
    [Fact]
    public void A_scope_in_any_case_is_one_only_as_OrdinalIgnoreCase_finds_it()
    {
        var dotted = $"i{(char)0x0307}zmir";
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Repository, "İzmir", RuleList.Deny, "WebFetch");
        file = PermissionRules.Add(file, RuleScope.Repository, "straße", RuleList.Deny, "WebSearch");
        file = PermissionRules.Add(file, RuleScope.Repository, dotted, RuleList.Ask, "Edit(/docs/**)");
        file = PermissionRules.Add(file, RuleScope.Repository, "Νίκος", RuleList.Allow, "Bash(npm test)");

        Assert.Equal(["İzmir", "straße", dotted, "Νίκος"], file.Repositories.Keys);
        Assert.DoesNotContain("WebFetch", PermissionRules.Compose(file, "default", dotted).Deny);
        Assert.DoesNotContain("WebSearch", PermissionRules.Compose(file, "default", "STRASSE").Deny);
        Assert.Contains("Bash(npm test)", PermissionRules.Compose(file, "default", "νίκοσ").Allow);
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
    // PERMSHAPE1: .NET's `$` also matches before a final line break; the shape ends at the very end, as the CLI's does.
    [InlineData("Bash\n")]
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

    /// <summary>
    /// 🔴 REV3 CLEAN1 — REV3's CLI F3, still live on the desktop's side. Empty is right for a SPAWN;
    /// an edit made over it (a rule added from Settings, a proposal accepted) wrote the empty read
    /// back, and every deny the person had written was gone. The CLI refuses that edit, and so does this.
    /// </summary>
    [Fact]
    public void An_edit_over_a_file_that_could_not_be_read_is_refused_and_the_file_is_kept()
    {
        const string held = """{ "machine": { "deny": ["Bash(rm:*)"] }, torn""";
        File.WriteAllText(PermissionRules.PathOf(_home), held);

        var edited = PermissionRules.Add(PermissionRules.Load(_home), RuleScope.Machine, null, RuleList.Allow, "Read");
        var refused = Assert.Throws<DriverException>(() => PermissionRules.Save(_home, edited));

        Assert.Contains("could not be read", refused.Message);
        Assert.Equal(held, File.ReadAllText(PermissionRules.PathOf(_home)));
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

        var path = SpawnSettings.Write(_home, "s1", rules, hardDeny: []);

        Assert.Equal(Path.Combine(_home, SpawnServers.Folder, "s1.settings.json"), path);
        var text = File.ReadAllText(path!);
        Assert.DoesNotContain("\r", text);
        using var document = JsonDocument.Parse(text);
        var permissions = document.RootElement.GetProperty("permissions");
        Assert.Equal(
            [.. Connector, .. Commit],
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

        Assert.Null(SpawnSettings.Write(_home, "s1", PermissionRules.Compose(file, "default", "engine"), PermissionRules.HardDeny(file)));
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
        var on = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");
        foreach (var rule in Commit) Assert.Contains(rule, on.Allow);
        Assert.Contains("Bash(git push:*)", on.Deny);

        var off = PermissionRules.Compose(Off(PermissionRules.Load(_home), "commit"), "default", "engine");
        foreach (var rule in Commit) Assert.DoesNotContain(rule, off.Allow);
        Assert.Contains("mcp__daoris-knowledge__quest_respond", off.Allow);
    }

    // ——— UNBLOCK4: the push carve-out, held harder (D122 §3.6, §3.7).

    /// <summary>
    /// 🔴 A push written another way than `git push …` met no deny rule in the composed file: the
    /// harness's maker says a `git push` rule does not stop `git -C . push` or `git -c &lt;key&gt;=&lt;value&gt;
    /// push`, and auto mode allows a push to the working repository by default (D81 put the protocol door
    /// in auto mode). Every form here must meet a deny rule the composed spawn file carries, on either
    /// door. A quoted subcommand, an alias, a path to git or a shell running it meet none; the classifier
    /// is told those (below), and on the pipe door nothing allows them.
    /// </summary>
    [Theory]
    [InlineData("git push")]
    [InlineData("git push origin main")]
    [InlineData("git push --force origin main")]
    [InlineData("git push -u origin feature/budget")]
    [InlineData("git -C . push")]
    [InlineData("git -C . push origin main")]
    [InlineData("git -C /work/engine push --force")]
    [InlineData("git -c push.default=current push")]
    [InlineData("git -c push.default=current push origin main")]
    [InlineData("git --git-dir=.git push origin main")]
    [InlineData("git --no-pager push")]
    public void A_push_in_each_form_meets_a_deny_rule_in_the_composed_file(string command)
    {
        var file = PermissionRules.Load(_home);
        var path = SpawnSettings.Write(
            _home, "s1", PermissionRules.Compose(file, "default", "engine"), PermissionRules.HardDeny(file));

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var deny = document.RootElement.GetProperty("permissions").GetProperty("deny").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.True(deny.Any(rule => BashRule.Matches(rule, command)), $"`{command}` meets none of: {string.Join(", ", deny)}");
    }

    /// <summary>
    /// The push's denies take nothing a session is allowed: its commit, a message that says "push", a
    /// rename, the read-only git it runs unasked, and what reading and writing across hand it (D107). A
    /// commit made with `-C` whose message has "push" as a word before another is refused, and that is
    /// the price: a false refusal costs a rewording, a false allowance the thing the carve-out keeps.
    /// </summary>
    [Theory]
    [InlineData("git add -A")]
    [InlineData("git commit -m \"Hold the push carve-out harder\"")]
    [InlineData("git mv docs/old.md docs/new.md")]
    [InlineData("git status")]
    [InlineData("git log --oneline -5")]
    [InlineData("git -C /work/game status")]
    [InlineData("git -C /work/game branch --list")]
    [InlineData("git -C /work/game commit -m \"Expose a streaming budget\"")]
    public void The_push_denies_refuse_none_of_the_work_a_session_is_allowed(string command)
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");

        Assert.DoesNotContain(rules.Deny, rule => BashRule.Matches(rule, command));
    }

    /// <summary>
    /// The harness's auto mode judges each action with a classifier, which allows a push to the working
    /// repository by default. While `no-push` is on, the spawn file tells it a push in any form, a
    /// publish and a release are the person's — as a hard denial, which neither the classifier's own
    /// allowances nor the conversation can clear. It reads `autoMode` from this command-line tier and
    /// never from a repository's own settings.
    /// </summary>
    [Fact]
    public void The_spawn_file_tells_auto_modes_classifier_a_push_in_any_form_is_the_persons()
    {
        var file = PermissionRules.Load(_home);
        var path = SpawnSettings.Write(
            _home, "s1", PermissionRules.Compose(file, "default", "engine"), PermissionRules.HardDeny(file));

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var hardDeny = document.RootElement.GetProperty("autoMode").GetProperty("hard_deny").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Equal(2, hardDeny.Count);
        Assert.Equal("$defaults", hardDeny[0]);
        Assert.Contains("git -C <dir> push", hardDeny[1]);
        Assert.Contains("git -c <key>=<value> push", hardDeny[1]);
        Assert.Contains("publishing a package", hardDeny[1]);
        Assert.Contains("creating a release", hardDeny[1]);
    }

    /// <summary>
    /// 🔴 `"$defaults"` first, always: a `hard_deny` list without it REPLACES the harness's built-in list,
    /// whose entry is the rule against sending data out. Whatever a caller hands, the file's list begins
    /// with it exactly once.
    /// </summary>
    [Fact]
    public void The_classifiers_own_hard_denials_are_never_dropped()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");
        string[] Written(string id, IReadOnlyList<string> hardDeny)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(SpawnSettings.Write(_home, id, rules, hardDeny)!));
            return [.. document.RootElement.GetProperty("autoMode").GetProperty("hard_deny").EnumerateArray().Select(e => e.GetString()!)];
        }

        Assert.Equal(["$defaults", "Never X."], Written("s1", ["Never X."]));
        Assert.Equal(["$defaults", "Never X."], Written("s2", ["Never X.", "$defaults"]));
        Assert.Equal(["$defaults", "Never X.", "Never Y."], Written("s3", ["$defaults", "Never X.", "$defaults", "Never Y."]));
    }

    /// <summary>
    /// The classifier is told only while `no-push` is on: switched off, the person has taken the push
    /// back, and nothing about it is handed — no `autoMode` key at all, so the harness's own lists stand.
    /// </summary>
    [Fact]
    public void With_no_push_switched_off_the_classifier_is_told_nothing()
    {
        var file = Off(PermissionRules.Load(_home), "no-push");

        Assert.Empty(PermissionRules.HardDeny(file));
        var path = SpawnSettings.Write(_home, "s1", PermissionRules.Compose(file, "default", "engine"), PermissionRules.HardDeny(file));
        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        Assert.False(document.RootElement.TryGetProperty("autoMode", out _));
        Assert.NotEmpty(PermissionRules.HardDeny(PermissionRules.Load(_home)));
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

        var path = SpawnSettings.Write(_home, "s1", PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine"), [], guard);

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var entry = Assert.Single(document.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray());
        Assert.Equal("Edit|Write|MultiEdit|NotebookEdit", entry.GetProperty("matcher").GetString());
        var hook = Assert.Single(entry.GetProperty("hooks").EnumerateArray());
        Assert.Equal("command", hook.GetProperty("type").GetString());
        // The node Tools resolves (TOOLS5): with no tools file, the one PATH finds, by its whole path.
        Assert.Equal(CommandPresence.Resolve("node", startable: true) ?? "node", hook.GetProperty("command").GetString());
        Assert.Equal([guard.Script, tree], hook.GetProperty("args").EnumerateArray().Select(e => e.GetString()));
        Assert.True(File.Exists(guard.Script));
    }

    /// <summary>D107: each declared write target rides the hook as one more argument after the tree.</summary>
    [Fact]
    public void The_spawn_files_hook_names_each_declared_target_after_the_tree()
    {
        var tree = Path.Combine(_home, "plugins");
        var target = Path.Combine(_home, "engine");
        var guard = TreeGuard.For(_home, tree, [target]);

        var path = SpawnSettings.Write(_home, "s1", RuleLists.Empty, [], guard);

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var hook = document.RootElement.GetProperty("hooks").GetProperty("PreToolUse")[0].GetProperty("hooks")[0];
        Assert.Equal([guard.Script, tree, target], hook.GetProperty("args").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void No_guard_is_no_hooks_key_and_a_guard_alone_is_still_a_file()
    {
        var rules = PermissionRules.Compose(PermissionRules.Load(_home), "default", "engine");
        using (var plain = JsonDocument.Parse(File.ReadAllText(SpawnSettings.Write(_home, "s1", rules, [])!)))
        {
            Assert.False(plain.RootElement.TryGetProperty("hooks", out _));
        }

        var nothing = PermissionRules.Compose(Off(PermissionRules.Load(_home), "connector", "commit", "no-push"), "default", "engine");
        var alone = SpawnSettings.Write(_home, "s2", nothing, [], TreeGuard.For(_home, _home));
        Assert.NotNull(alone);
        using var guarded = JsonDocument.Parse(File.ReadAllText(alone!));
        Assert.True(guarded.RootElement.TryGetProperty("hooks", out _));
    }

    private static PermissionFile Off(PermissionFile file, params string[] ids) =>
        ids.Aggregate(file, (held, id) => PermissionRules.SwitchDefault(held, id, on: false));

    /// <summary>
    /// A default's reason is read by a person on the rules card and in `daoris agent rules list`, who
    /// has no decisions record to look a number up in (POLISH4). The reason says what the number meant.
    /// </summary>
    [Fact]
    public void A_defaults_reason_names_no_decision_number()
    {
        foreach (var shipped in PermissionRules.Defaults)
        {
            Assert.DoesNotMatch(@"\bD\d+\b", shipped.Why);
        }
    }

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
