using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What an agent may do, over the bridge (`DriverModule.Rules.cs`, MOD5): the rules, a change to them, and
/// the answer to an agent's proposal.
/// </summary>
public sealed class DriverModuleRulesTests : DriverModuleBridge
{
    /// <summary>
    /// What an agent may do (PERM1, D72) as the page reads it: Daoris's defaults, each with its reason and
    /// whether it is on, and every scope the machine's file holds — the file the terminal's
    /// `daoris agent rules` edits.
    /// </summary>
    [Fact]
    public async Task The_rules_are_answered_with_the_defaults_and_every_scope_the_file_holds()
    {
        var file = PermissionRules.Load(Home);
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Bash(npm run test:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Deny, "Bash(rm -rf:*)");
        PermissionRules.Save(Home, PermissionRules.SwitchDefault(file, "no-push", on: false));

        var answered = await AnswerAsync(Module(), "RULES");

        Assert.Equal(PermissionRules.PathOf(Home), answered.GetProperty("path").GetString());
        var defaults = answered.GetProperty("defaults").EnumerateArray().ToList();
        Assert.Equal(
            ["connector", "commit", "no-push", "tree-guard"],
            defaults.Select(d => d.GetProperty("id").GetString()!).ToArray());
        Assert.True(defaults[0].GetProperty("on").GetBoolean());
        Assert.False(defaults[2].GetProperty("on").GetBoolean());
        Assert.Equal("deny", defaults[2].GetProperty("list").GetString());
        // The driver's own sentence travels: what the number meant, never the number (POLISH4a).
        Assert.Contains("stays the person's call", defaults[2].GetProperty("why").GetString());
        // The tree guard (PERM3) is a hook: no rule, and the tools it judges named instead. The bridge
        // leaves a null out, so a rule default carries no `hook` the page could misread.
        Assert.Empty(defaults[3].GetProperty("rules").EnumerateArray());
        Assert.Equal(TreeGuard.Matcher, defaults[3].GetProperty("hook").GetString());
        Assert.True(defaults[0].GetProperty("hook").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

        var scopes = answered.GetProperty("scopes").EnumerateArray().ToList();
        var machine = scopes.Single(s => s.GetProperty("scope").GetString() == "machine");
        Assert.Equal(["Bash(npm run test:*)"], machine.GetProperty("allow").EnumerateArray().Select(r => r.GetString()!).ToArray());
        var engine = scopes.Single(s => s.GetProperty("scope").GetString() == "repository");
        Assert.Equal("engine", engine.GetProperty("name").GetString());
        Assert.Equal(["Bash(rm -rf:*)"], engine.GetProperty("deny").EnumerateArray().Select(r => r.GetString()!).ToArray());
    }

    /// <summary>
    /// The screen's half of `daoris agent rules` (D50): every change is an edit to the same file, and
    /// the answer is the state after it, like every other control here.
    /// </summary>
    [Fact]
    public async Task A_rule_changed_on_the_screen_lands_in_the_file_a_terminal_edits()
    {
        var module = Module();

        var added = await AnswerAsync(module, "RULE_ACTION",
            new { action = "add", list = "allow", rule = "Bash(make:*)", scope = "repository", name = "engine" });
        Assert.Equal(["Bash(make:*)"], PermissionRules.Load(Home).Repositories["engine"].Allow);
        Assert.Contains(added.GetProperty("scopes").EnumerateArray(), s => s.GetProperty("scope").GetString() == "repository");

        await AnswerAsync(module, "RULE_ACTION",
            new { action = "add", list = "ask", rule = "WebFetch", scope = "workspace", name = "default" });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Workspaces["default"].Ask);

        await AnswerAsync(module, "RULE_ACTION",
            new { action = "remove", rule = "Bash(make:*)", scope = "repository", name = "engine" });
        Assert.False(PermissionRules.Load(Home).Repositories.ContainsKey("engine"));

        var off = await AnswerAsync(module, "RULE_ACTION", new { action = "default", id = "connector", on = false });
        Assert.Equal(["connector"], PermissionRules.Load(Home).DefaultsOff);
        Assert.False(off.GetProperty("defaults").EnumerateArray().First().GetProperty("on").GetBoolean());
    }

    /// <summary>A refusal is the driver's own sentence, verbatim — never a bare code or a crash.</summary>
    [Fact]
    public async Task A_rule_that_is_not_one_and_an_action_this_build_lacks_are_refused_in_the_drivers_words()
    {
        var module = Module();

        var notARule = await RefusalAsync(module, "RULE_ACTION",
            new { action = "add", list = "allow", rule = "rm -rf /", scope = "machine" });
        Assert.Contains(Refusals.DriverRefused, notARule);
        Assert.Contains("not a permission rule", notARule);

        var unknown = await RefusalAsync(module, "RULE_ACTION", new { action = "explode" });
        Assert.Contains("add, remove, default", unknown);
        Assert.False(File.Exists(PermissionRules.PathOf(Home)));
    }

    /// <summary>A proposal exactly as the connector writes it (PERM2) — THE FILE is the contract.</summary>
    private void Propose(
        string id, string state, string action, string? list = null, string? rule = null,
        string scope = "machine", string? name = null, string? session = "s1a2b3c4", string? ask = null,
        string proposed = "2026-09-24T10:00:00.0000000+00:00")
    {
        var folder = Path.Combine(Home, RuleProposals.Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, $"{id}.json"), JsonSerializer.Serialize(new
        {
            id,
            proposed,
            by = new { session, ask, folder = "C:/somewhere/engine" },
            change = new { action, scope, name, list, rule, @default = (string?)null, on = (bool?)null },
            why = "The tests need it.",
            state,
        }));
    }

    /// <summary>
    /// What agents proposed about the rules (PERM2, D74) rides the same answer as the rules: the person
    /// reads a waiting widening beside the rules it would change, and the history of every other.
    /// </summary>
    [Fact]
    public async Task The_rules_carry_every_proposal_newest_first_with_who_made_it()
    {
        Propose("p0000001", "applied", "add", list: "deny", rule: "Bash(rm:*)", session: null);
        Propose("p0000002", "waiting", "add", list: "allow", rule: "WebFetch", scope: "workspace", name: "default",
            session: "i9n8t7k6", ask: "a1b2c3", proposed: "2026-09-24T11:00:00.0000000+00:00");

        var answered = await AnswerAsync(Module(), "RULES");

        var proposals = answered.GetProperty("proposals").EnumerateArray().ToList();
        Assert.Equal(["p0000002", "p0000001"], proposals.Select(p => p.GetProperty("id").GetString()!).ToArray());
        var waiting = proposals[0];
        Assert.Equal("waiting", waiting.GetProperty("state").GetString());
        Assert.Equal("add", waiting.GetProperty("action").GetString());
        Assert.Equal("allow", waiting.GetProperty("list").GetString());
        Assert.Equal("WebFetch", waiting.GetProperty("rule").GetString());
        Assert.Equal("workspace", waiting.GetProperty("scope").GetString());
        Assert.Equal("default", waiting.GetProperty("name").GetString());
        Assert.Equal("i9n8t7k6", waiting.GetProperty("session").GetString());
        Assert.Equal("a1b2c3", waiting.GetProperty("ask").GetString());
        Assert.Equal("The tests need it.", waiting.GetProperty("why").GetString());
        // 🔴 The folder a session ran in is a machine path, and it stays in the file: the page is told
        // who proposed, never where they stood.
        Assert.False(waiting.TryGetProperty("folder", out _));
        // A session the driver did not start carries none, which the bridge leaves out.
        Assert.True(!proposals[1].TryGetProperty("session", out var none) || none.ValueKind is JsonValueKind.Null);
    }

    /// <summary>The screen's half of `daoris agent rules accept|decline` (D50), answered with the rules after it.</summary>
    [Fact]
    public async Task A_proposal_answered_on_the_screen_lands_as_the_terminal_would_leave_it()
    {
        Propose("p0000007", "waiting", "add", list: "allow", rule: "WebFetch");
        Propose("p0000008", "waiting", "add", list: "allow", rule: "Bash(curl:*)");
        var module = Module();

        var accepted = await AnswerAsync(module, "RULE_PROPOSAL", new { id = "p0000007", accept = true });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Machine.Allow);
        var row = accepted.GetProperty("proposals").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "p0000007");
        Assert.Equal("accepted", row.GetProperty("state").GetString());
        Assert.Equal("the person", row.GetProperty("settledBy").GetString());

        var declined = await AnswerAsync(module, "RULE_PROPOSAL", new { id = "p0000008", accept = false, note = "Not from a session." });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Machine.Allow);
        Assert.Equal("Not from a session.", declined.GetProperty("proposals").EnumerateArray()
            .Single(p => p.GetProperty("id").GetString() == "p0000008").GetProperty("note").GetString());

        var again = await RefusalAsync(module, "RULE_PROPOSAL", new { id = "p0000007", accept = false });
        Assert.Contains(Refusals.DriverRefused, again);
        Assert.Contains("already", again);
        Assert.Contains("no proposal", await RefusalAsync(module, "RULE_PROPOSAL", new { id = "nope1234", accept = true }));
    }
}
