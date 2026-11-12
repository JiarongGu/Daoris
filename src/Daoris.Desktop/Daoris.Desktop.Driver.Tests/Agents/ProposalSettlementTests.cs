using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A widening settles once whoever answers: a supervisor's yes writes the rule recorded by its role and the person's no after it finds it
/// settled; what stays the person's (a deny of theirs, a denying default, a publish) is returned unwritten; a proposal found settling is
/// never saved again.
/// </summary>
public sealed class ProposalSettlementTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-settle-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T10:00:00Z");
    private static readonly RuleSettler Role = RuleSettler.Supervisor("allow-keeper", "d1");

    public ProposalSettlementTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Propose(
        string id, string action, string scope = "machine", string? name = null, string? list = null, string? rule = null,
        string? defaultId = null, bool? on = null, string state = "waiting")
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.RuleProposals);
        Directory.CreateDirectory(folder);
        var node = new JsonObject
        {
            ["id"] = id,
            ["proposed"] = At.ToString("O"),
            ["by"] = new JsonObject { ["session"] = "s1a2b3c4" },
            ["change"] = new JsonObject
            {
                ["action"] = action, ["scope"] = scope, ["name"] = name, ["list"] = list, ["rule"] = rule, ["default"] = defaultId, ["on"] = on,
            },
            ["why"] = "The session's own reason.",
            ["state"] = state,
        };
        var path = Path.Combine(folder, $"{id}.json");
        File.WriteAllText(path, node.ToJsonString() + "\n");
        return path;
    }

    private static JsonElement Read(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();

    private void Rules(Func<PermissionFile, PermissionFile> change) => PermissionRules.Save(_home, change(PermissionRules.Load(_home)));

    [Fact]
    public void A_supervisors_yes_writes_the_rule_recorded_by_its_role_and_the_persons_no_after_it_finds_it_settled()
    {
        var path = Propose("p1", "add", scope: "repository", name: "engine", list: "allow", rule: "Bash(npm run build:*)");

        var settled = ProposalSettlement.Settle(_home, "p1", Role, yes: true, "the build is read-only", At);

        Assert.Equal(SettleOutcome.Settled, settled.Outcome);
        Assert.Equal(["Bash(npm run build:*)"], PermissionRules.Load(_home).Repositories["engine"].Allow);
        var by = Read(path).GetProperty("settled");
        Assert.Equal(("the supervisor", "allow-keeper", "d1"), (by.GetProperty("by").GetString(), by.GetProperty("role").GetString(), by.GetProperty("decision").GetString()));
        var late = Assert.Throws<DriverException>(() => ProposalSettlement.Settle(_home, "p1", RuleSettler.Person, yes: false, null, At));
        Assert.Contains("already accepted by its supervisor", late.Message);
        Assert.Equal(["Bash(npm run build:*)"], PermissionRules.Load(_home).Repositories["engine"].Allow);
    }

    [Fact]
    public void A_persons_no_settles_it_and_a_supervisors_yes_after_it_writes_nothing()
    {
        Propose("p2", "add", list: "allow", rule: "Bash(make:*)");

        ProposalSettlement.Settle(_home, "p2", RuleSettler.Person, yes: false, null, At);
        var error = Assert.Throws<DriverException>(() => ProposalSettlement.Settle(_home, "p2", Role, yes: true, null, At));

        Assert.Contains("already declined", error.Message);
        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
    }

    [Fact]
    public void A_proposal_found_settling_is_never_saved_again_and_is_returned_to_the_person_where_the_access_was_withdrawn()
    {
        // A stop between the save and the mark; then the person's `remove` of the allow while no Daoris ran.
        var path = Propose("p3", "add", list: "allow", rule: "Bash(make:*)", state: "settling");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["settled"] = new JsonObject { ["at"] = At.ToString("O"), ["by"] = "the supervisor", ["role"] = "allow-keeper", ["decision"] = "d1" };
        File.WriteAllText(path, root.ToJsonString());

        var recovered = ProposalSettlement.Recover(_home, "p3", At)!;

        Assert.Equal(SettleOutcome.Returned, recovered.Outcome);
        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
        Assert.Equal("waiting", Read(path).GetProperty("state").GetString());
    }

    [Fact]
    public void A_proposal_found_settling_whose_change_the_rules_hold_is_settled_by_its_recorded_role_with_no_save()
    {
        var path = Propose("p4", "add", list: "allow", rule: "Bash(make:*)", state: "settling");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["settled"] = new JsonObject { ["at"] = At.ToString("O"), ["by"] = "the supervisor", ["role"] = "allow-keeper", ["decision"] = "d1" };
        File.WriteAllText(path, root.ToJsonString());
        Rules(file => PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Bash(make:*)"));
        var before = File.ReadAllText(PermissionRules.PathOf(_home));

        var recovered = ProposalSettlement.Recover(_home, "p4", At)!;

        Assert.Equal(SettleOutcome.Settled, recovered.Outcome);
        Assert.Equal(before, File.ReadAllText(PermissionRules.PathOf(_home)));
        var settled = Read(path);
        Assert.Equal(("accepted", "allow-keeper"), (settled.GetProperty("state").GetString(), settled.GetProperty("settled").GetProperty("role").GetString()));
    }

    [Fact]
    public void The_tick_recovers_a_proposal_left_settling()
    {
        var path = Propose("p5", "add", list: "allow", rule: "Bash(make:*)", state: "settling");

        RuleProposals.Settle(_home, At);

        Assert.Equal("waiting", Read(path).GetProperty("state").GetString());
        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
    }

    [Fact]
    public void A_yes_on_a_widening_whose_rule_the_person_has_since_denied_is_returned_to_them_with_nothing_written()
    {
        var path = Propose("p6", "add", list: "allow", rule: "Bash(make:*)");
        Rules(file => PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Deny, "Bash(make:*)"));
        var before = File.ReadAllText(PermissionRules.PathOf(_home));

        var settled = ProposalSettlement.Settle(_home, "p6", Role, yes: true, null, At);

        Assert.Equal((SettleOutcome.Returned, WideningAllows.RemovesDeny), (settled.Outcome, settled.Why));
        Assert.Equal(before, File.ReadAllText(PermissionRules.PathOf(_home)));
        Assert.Equal("waiting", Read(path).GetProperty("state").GetString());
    }

    [Fact]
    public void A_yes_on_a_default_that_denies_switched_off_is_returned_and_a_persons_yes_on_it_applies()
    {
        PermissionRules.Save(_home, PermissionRules.SwitchDefault(PermissionRules.Load(_home), PermissionRules.NoPublishId, on: true));
        Propose("p7", "default", defaultId: "no-publish", on: false);

        var settled = ProposalSettlement.Settle(_home, "p7", Role, yes: true, null, At);
        Assert.Equal((SettleOutcome.Returned, WideningAllows.SwitchesOffDenial), (settled.Outcome, settled.Why));
        Assert.Contains("no-publish", PermissionRules.Load(_home).DefaultsOn);

        ProposalSettlement.Settle(_home, "p7", RuleSettler.Person, yes: true, null, At);
        Assert.DoesNotContain("no-publish", PermissionRules.Load(_home).DefaultsOn);
    }

    [Theory]
    [InlineData("Bash(npm publish:*)")]
    [InlineData("Bash(npm:*)")]
    [InlineData("Bash(npm *)")]
    [InlineData("Bash(npm -g publish)")]
    [InlineData("Bash(npm -* publish *)")]
    [InlineData("Bash")]
    [InlineData("PowerShell(npm publish:*)")]
    [InlineData("PowerShell(uv publish:*)")]
    [InlineData("Bash(poetry publish:*)")]
    [InlineData("Bash(gem push:*)")]
    [InlineData("Bash(docker push:*)")]
    [InlineData("Bash(cargo:*)")]
    [InlineData("Bash(gh release create:*)")]
    public void A_widening_that_would_let_a_publish_through_waits_for_the_person_while_no_publish_is_on(string rule)
    {
        PermissionRules.Save(_home, PermissionRules.SwitchDefault(PermissionRules.Load(_home), PermissionRules.NoPublishId, on: true));
        Propose("p8", "add", scope: "repository", name: "engine", list: "allow", rule: rule);

        var settled = ProposalSettlement.Settle(_home, "p8", Role, yes: true, null, At);

        Assert.Equal((SettleOutcome.Returned, WideningAllows.LetsPublishThrough), (settled.Outcome, settled.Why));
        Assert.Empty(PermissionRules.Load(_home).Repositories);
    }

    [Theory]
    [InlineData("Bash(npm run build:*)")]
    [InlineData("Bash(npm test:*)")]
    [InlineData("Bash(git status:*)")]
    [InlineData("PowerShell(Get-ChildItem:*)")]
    [InlineData("Edit(/src/**)")]
    public void A_widening_that_matches_no_publish_is_decided(string rule)
    {
        Propose("p9", "add", scope: "repository", name: "engine", list: "allow", rule: rule);

        Assert.Equal(SettleOutcome.Settled, ProposalSettlement.Settle(_home, "p9", Role, yes: true, null, At).Outcome);
        Assert.Equal([rule], PermissionRules.Load(_home).Repositories["engine"].Allow);
    }

    [Fact]
    public void The_publish_widening_is_decided_once_the_person_has_switched_no_publish_off()
    {
        Rules(file => PermissionRules.SwitchDefault(file, "no-publish", on: false));
        Propose("p10", "add", scope: "repository", name: "engine", list: "allow", rule: "Bash(npm publish:*)");

        Assert.Equal(SettleOutcome.Settled, ProposalSettlement.Settle(_home, "p10", Role, yes: true, null, At).Outcome);
    }

    [Fact]
    public void Every_form_no_publish_lists_is_a_rule_of_both_shell_tools_and_the_forms_are_never_handed_as_denies()
    {
        Assert.All(PublishForms.Rules, rule => Assert.Null(PermissionRules.Refusal(rule)));
        Assert.All(PublishForms.Rules, rule => Assert.True(PublishForms.Matches(rule), rule));
        foreach (var program in new[] { "npm", "pnpm", "yarn", "cargo", "dotnet nuget push", "twine", "gh release create", "uv", "poetry", "gem", "docker" })
        {
            Assert.Contains(PublishForms.Rules, rule => rule.StartsWith("Bash(" + program, StringComparison.Ordinal));
            Assert.Contains(PublishForms.Rules, rule => rule.StartsWith("PowerShell(" + program, StringComparison.Ordinal));
        }

        var handed = PermissionRules.Compose(PermissionRules.Load(_home), null, null);
        Assert.Empty(handed.Deny.Intersect(PublishForms.Rules));
    }

    [Theory]
    [InlineData("machine", null, "machine")]
    [InlineData("workspace", "games", "workspace:games")]
    [InlineData("repository", "engine", "repository:engine")]
    public void A_widenings_place_is_its_rules_reach(string scope, string? name, string place)
    {
        var change = new ProposedChange("add", Enum.Parse<RuleScope>(scope, ignoreCase: true), name, RuleList.Allow, "Bash(make:*)", null, null);

        Assert.Equal(place, WideningAllows.PlaceOf(change));
    }

    [Fact]
    public void A_defaults_switch_is_placed_at_the_machine_whatever_scope_it_names()
    {
        var change = new ProposedChange("default", RuleScope.Repository, "engine", null, null, "commit", true);

        Assert.Equal("machine", WideningAllows.PlaceOf(change));
    }

    [Fact]
    public void A_yes_whose_save_is_refused_leaves_the_proposal_waiting_not_settling()
    {
        File.WriteAllText(PermissionRules.PathOf(_home), """{ "repositories": { "engine": "x" } }""");
        var path = Propose("p1", "add", scope: "repository", name: "engine", list: "allow", rule: "Bash(npm run build:*)");

        Assert.Throws<DriverException>(() => ProposalSettlement.Settle(_home, "p1", RuleSettler.Person, yes: true, null, At));

        Assert.Equal("waiting", Read(path).GetProperty("state").GetString());
        File.WriteAllText(PermissionRules.PathOf(_home), "{}");
        var settled = ProposalSettlement.Settle(_home, "p1", RuleSettler.Person, yes: true, null, At);
        Assert.Equal(SettleOutcome.Settled, settled.Outcome);
    }
}
