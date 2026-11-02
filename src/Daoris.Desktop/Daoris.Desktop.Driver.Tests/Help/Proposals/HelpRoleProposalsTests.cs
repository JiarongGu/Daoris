using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>role</c> proposal (FOUNDATION1y-i, the foundation design §2.7; O2-11): a change of the role catalogue judged by the
/// role door's judge before the person sees it, every code refused at this door as at the screen's and the terminal's, a field a set
/// leaves out kept from the role, and a change made since the revision read refused rather than overwritten.
/// </summary>
public sealed class HelpRoleProposalsTests : HelpProposalsFixture, IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "help-role", Guid.NewGuid().ToString("N")[..8]);

    public HelpRoleProposalsTests()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        System.IO.File.WriteAllText(file, """{ "adapter": "claude-code" }""");
        Staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Staffing.ImportFile();
    }

    void IDisposable.Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing { get; }

    private HelpMachineFacts Machine => Facts with { Home = _home };

    private static HelpProposal Role(string door, string id, string? press = null) =>
        Of("role", door, id, null) with { RolePress = press is null ? null : JsonDocument.Parse(press).RootElement.Clone() };

    [Fact]
    public void A_set_is_judged_by_the_role_door_and_its_card_says_the_change_and_the_terminal_twin()
    {
        var plan = HelpProposals.Plan(
            Role("set", "designer", """{ "name": { "en": "Designer", "zh": "设计师" }, "takes": ["design"], "choices": [{ "agent": "claude-code", "model": "opus", "effort": "high" }], "revision": 0 }"""),
            DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Contains("Set the role `designer` (Designer · 设计师): it takes design on this machine, on claude-code · opus · high.", plan.Describe);
        Assert.Equal("daoris driver role set designer --name \"Designer\" --name-in zh=\"设计师\" --takes design --scope machine \"claude-code opus high\"", plan.Terminal);
        Assert.Equal(RoleSuggestion.Designer.Role.Json(), Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Json());
    }

    /// <summary>I18NOPEN1 (D222): a role proposed in one language, or in a third, plans, and its card and terminal twin say the name as given.</summary>
    [Fact]
    public void A_role_named_in_one_language_or_a_third_plans_and_its_card_says_it()
    {
        var one = HelpProposals.Plan(Role("set", "designer", """{ "name": "Designer", "takes": ["design"], "choices": [{ "agent": "claude-code", "model": "opus", "effort": "high" }] }"""), DriverConfig.Empty, Machine);
        Assert.Null(one.Refusal);
        Assert.Contains("Set the role `designer` (Designer): it takes design on this machine", one.Describe);
        Assert.Equal("daoris driver role set designer --name \"Designer\" --takes design --scope machine \"claude-code opus high\"", one.Terminal);

        var third = HelpProposals.Plan(Role("set", "designer", """{ "name": { "fr": "Concepteur" }, "takes": ["design"], "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, Machine);
        Assert.Null(third.Refusal);
        Assert.Contains("(Concepteur)", third.Describe);
        Assert.StartsWith("daoris driver role set designer --name-in fr=\"Concepteur\" --takes design", third.Terminal);
        Assert.Equal("""{"fr":"Concepteur"}""", Assert.IsType<RoleChange.SetRole>(third.Role!.Change).Role.Name.Json());

        Assert.NotNull(HelpProposals.Plan(Role("set", "designer", """{ "name": " ", "takes": ["design"], "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, Machine).Refusal);
    }

    [Fact]
    public void Every_judge_code_is_refused_at_this_door_with_the_judge_s_sentence()
    {
        var seeded = RoleCatalog.Of([]);
        Assert.Equal(seeded.Judge(RoleChange.Remove("build"))!.Said, HelpProposals.Plan(Role("remove", "build"), DriverConfig.Empty, Machine).Refusal);
        Assert.Equal(
            seeded.Judge(RoleChange.Set(new RoleRecord("steward", AuthoredText.Of("S"), RoleRecord.Always, ["build"], RoleScope.Everywhere)))!.Said,
            HelpProposals.Plan(Role("set", "steward", """{ "name": { "en": "S", "zh": "S" }, "takes": ["build"] }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("is not a work", HelpProposals.Plan(Role("set", "lead", """{ "name": { "en": "L", "zh": "L" }, "takes": ["gardening"] }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("cannot be off", HelpProposals.Plan(Role("set", "night", """{ "name": { "en": "N", "zh": "N" }, "takes": ["build"], "scope": { "kind": "workspace", "id": "w1" } }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("already takes", HelpProposals.Plan(Role("set", "chatty", """{ "name": { "en": "C", "zh": "C" }, "takes": ["chat"], "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, Machine).Refusal);
    }

    /// <summary>STAFFPOLICY1b: a policy rides a set as a preset or a record, is judged by the policy's judge, and is applied by the person's press with the revision read.</summary>
    [Fact]
    public void A_policy_proposed_with_a_set_is_judged_described_and_applied_by_the_press_alone()
    {
        Staffing.Set("build", [new ActorChoice("claude-code", "sonnet", "high"), new ActorChoice("claude-code", "opus", "high")]);
        var config = StaffingProjection.Config(Staffing.Read()!);

        var preset = HelpProposals.Plan(Role("set", "build", """{ "policy": "even", "policyRevision": 0 }"""), config, Machine);

        Assert.Null(preset.Refusal);
        Assert.Contains("Policy, Build: First that can run → Spread by weight (1:1); on a limit: next by weight; at most 3 moves a day.", preset.Describe);
        Assert.Contains("This can spend differently.", preset.Describe);
        Assert.Contains("&& daoris driver role policy build --preset first --rule weight", preset.Terminal);
        Assert.Equal(StaffPolicy.Code, Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(Preset: "cheapest"))).Code);

        Assert.Null(Staffing.Policy("build").Policy);
        RoleDoor.Apply(Staffing, preset.Role!.Change, preset.Role.Choices, preset.Role.Read, preset.Role.Policy);
        Assert.Equal((StaffPolicyRead.Set, 1L), (Staffing.Policy("build").State, Staffing.Policy("build").Revision));

        var stale = HelpProposals.Plan(Role("set", "build", """{ "policy": "first", "policyRevision": 0 }"""), config, Machine);
        Assert.Contains("changed since you read it", stale.Refusal);
        var unenforced = HelpProposals.Plan(Role("set", "build", """{ "policy": { "rule": "order", "choices": [], "moves": { "model-refused": "hold" } }, "policyRevision": 1 }"""), config, Machine);
        Assert.Contains("does not enforce yet", unenforced.Refusal);
    }

    /// <summary>STAFFPOLICY1g: the card says the policy before and after, a stale one as having run in order, and a revision moved since is refused at Apply.</summary>
    [Fact]
    public void The_card_says_the_policy_before_and_after_and_a_revision_moved_since_is_refused_at_apply()
    {
        Staffing.Set("build", [new ActorChoice("claude-code", "sonnet", "high"), new ActorChoice("claude-code", "opus", "high")]);
        var config = StaffingProjection.Config(Staffing.Read()!);
        var first = HelpProposals.Plan(Role("set", "build", """{ "policy": "even", "policyRevision": 0 }"""), config, Machine);
        Assert.Contains("Policy, Build: First that can run → Spread by weight (1:1); on a limit: next by weight; at most 3 moves a day.", first.Describe);
        var duplicate = HelpProposals.Plan(Role("set", "build", """{ "policy": "weighted", "policyRevision": 0 }"""), config, Machine);
        RoleDoor.Apply(Staffing, first.Role!.Change, first.Role.Choices, first.Role.Read, first.Role.Policy);

        var weighted = HelpProposals.Plan(Role("set", "build", """{ "policy": "weighted", "policyRevision": 1 }"""), config, Machine);
        Assert.Contains("Policy, Build: Spread by weight (1:1); on a limit: next by weight; at most 3 moves a day → Spread by weight (1:1); on a limit: next by weight; at most 3 moves a day. This can spend differently.", weighted.Describe);

        // The card of the same read, pressed after the policy moved, is refused whole and the stored policy stays as it was.
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, duplicate.Role!.Change, duplicate.Role.Choices, duplicate.Role.Read, duplicate.Role.Policy));
        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Equal(1L, Staffing.Policy("build").Revision);
        Assert.Equal(StaffRules.Weight, Staffing.Policy("build").Policy!.Rule);

        var cleared = HelpProposals.Plan(Role("set", "build", """{ "policy": null, "policyRevision": 1 }"""), config, Machine);
        Assert.Contains("→ First that can run (its policy is cleared).", cleared.Describe);
    }

    [Fact]
    public void A_set_of_a_seeded_role_naming_its_choices_alone_keeps_its_record_and_sets_its_order()
    {
        var plan = HelpProposals.Plan(Role("set", "build", """{ "choices": [{ "agent": "claude-code", "model": "sonnet", "effort": "high" }] }"""), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(RoleCatalog.Seed[0].Json(), Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Json());
        Assert.Equal([new ActorChoice("claude-code", "sonnet", "high")], plan.Role.Choices);
    }

    /// <summary>DEFAULTCONFIG1b's <i>Restore all role defaults</i>: <c>default-all</c> names no role, so the target and a revision read are not its.</summary>
    [Fact]
    public void Every_seeded_role_given_back_its_seed_names_no_role_and_its_card_says_the_terminal_twin()
    {
        RoleDoor.Apply(Staffing, RoleChange.Remove("intake"));

        var plan = HelpProposals.Plan(Role("default-all", "*", """{ "revision": 0 }"""), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.IsType<RoleChange.DefaultAllRoles>(plan.Role!.Change);
        Assert.Null(plan.Role.Read);
        Assert.Equal("daoris driver role default --all", plan.Terminal);
        Assert.Contains("Give every seeded role back its seed", plan.Describe);
        Assert.Contains("added roles are kept", plan.Describe);
    }

    [Fact]
    public void A_change_made_since_the_revision_read_is_refused()
    {
        RoleDoor.Apply(Staffing, RoleChange.Remove("intake"));

        Assert.Contains("changed since it was read (revision 0, now 1)", HelpProposals.Plan(Role("default", "intake", """{ "revision": 0 }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Null(HelpProposals.Plan(Role("default", "intake", """{ "revision": 1 }"""), DriverConfig.Empty, Machine).Refusal);
    }
}
