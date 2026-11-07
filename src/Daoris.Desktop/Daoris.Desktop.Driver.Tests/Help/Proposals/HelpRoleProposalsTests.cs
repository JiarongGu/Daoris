using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>role</c> proposal: a change of the role catalogue judged by the
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
        // Every home holds the seeded machine rules, so a preset's moves are judged beside the tries rule a home always has.
        SupervisorRuleBook.SeedFresh(_home, () => StoreSettled.Yes);
        // The seeded Design role takes design machine-wide; the cases below add one that does, so it is removed first.
        RoleCatalog.Write(DesktopStore.LocationIn(_home), RoleChange.Remove("design"), () => StoreSettled.Yes);
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
        Assert.Equal("daoris role set designer --name \"Designer\" --name-in zh=\"设计师\" --takes design --scope machine \"claude-code opus high\"", plan.Terminal);
        Assert.Equal("designer", Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Id);
    }

    /// <summary>A role proposed in one language, or in a third, plans, and its card and terminal twin say the name as given.</summary>
    [Fact]
    public void A_role_named_in_one_language_or_a_third_plans_and_its_card_says_it()
    {
        var one = HelpProposals.Plan(Role("set", "designer", """{ "name": "Designer", "takes": ["design"], "choices": [{ "agent": "claude-code", "model": "opus", "effort": "high" }] }"""), DriverConfig.Empty, Machine);
        Assert.Null(one.Refusal);
        Assert.Contains("Set the role `designer` (Designer): it takes design on this machine", one.Describe);
        Assert.Equal("daoris role set designer --name \"Designer\" --takes design --scope machine \"claude-code opus high\"", one.Terminal);

        var third = HelpProposals.Plan(Role("set", "designer", """{ "name": { "fr": "Concepteur" }, "takes": ["design"], "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, Machine);
        Assert.Null(third.Refusal);
        Assert.Contains("(Concepteur)", third.Describe);
        Assert.StartsWith("daoris role set designer --name-in fr=\"Concepteur\" --takes design", third.Terminal);
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

    /// <summary>A policy rides a set as a preset or a record, is judged by the policy's judge, and is applied by the person's press with the revision read.</summary>
    [Fact]
    public void A_policy_proposed_with_a_set_is_judged_described_and_applied_by_the_press_alone()
    {
        Staffing.Set("build", [new ActorChoice("claude-code", "sonnet", "high"), new ActorChoice("claude-code", "opus", "high")]);
        var config = StaffingProjection.Config(Staffing.Read()!);

        var preset = HelpProposals.Plan(Role("set", "build", """{ "policy": "even", "policyRevision": 0 }"""), config, Machine);

        Assert.Null(preset.Refusal);
        Assert.Contains("Policy, Build: First that can run → Spread by weight (1:1). This can spend differently.", preset.Describe);
        Assert.Contains("This can spend differently.", preset.Describe);
        Assert.Contains("&& daoris role policy build --preset first --rule weight", preset.Terminal);
        Assert.Equal(StaffPolicy.Code, Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(Preset: "cheapest"))).Code);

        Assert.Null(Staffing.Policy("build").Policy);
        RoleDoor.Apply(Staffing, preset.Role!.Change, preset.Role.Choices, preset.Role.Read, preset.Role.Policy);
        Assert.Equal((StaffPolicyRead.Set, 1L), (Staffing.Policy("build").State, Staffing.Policy("build").Revision));

        var stale = HelpProposals.Plan(Role("set", "build", """{ "policy": "first", "policyRevision": 0 }"""), config, Machine);
        Assert.Contains("changed since you read it", stale.Refusal);
        // A policy keeps no moves: the line's rules hold them, so a record that names some is read without them, never refused for them.
        var unstored = HelpProposals.Plan(Role("set", "build", """{ "policy": { "rule": "order", "choices": [], "moves": { "model-refused": "hold" } }, "policyRevision": 1 }"""), config, Machine);
        Assert.Null(unstored.Refusal);
        Assert.DoesNotContain("model-refused", unstored.Describe);
    }

    /// <summary>The card says the policy before and after, a stale one as having run in order, and a revision moved since is refused at Apply.</summary>
    [Fact]
    public void The_card_says_the_policy_before_and_after_and_a_revision_moved_since_is_refused_at_apply()
    {
        Staffing.Set("build", [new ActorChoice("claude-code", "sonnet", "high"), new ActorChoice("claude-code", "opus", "high")]);
        var config = StaffingProjection.Config(Staffing.Read()!);
        var first = HelpProposals.Plan(Role("set", "build", """{ "policy": "even", "policyRevision": 0 }"""), config, Machine);
        Assert.Contains("Policy, Build: First that can run → Spread by weight (1:1).", first.Describe);
        var duplicate = HelpProposals.Plan(Role("set", "build", """{ "policy": "weighted", "policyRevision": 0 }"""), config, Machine);
        RoleDoor.Apply(Staffing, first.Role!.Change, first.Role.Choices, first.Role.Read, first.Role.Policy);

        var weighted = HelpProposals.Plan(Role("set", "build", """{ "policy": "weighted", "policyRevision": 1 }"""), config, Machine);
        Assert.Contains("Policy, Build: Spread by weight (1:1) → Spread by weight (1:1). This can spend differently.", weighted.Describe);

        // The card of the same read, pressed after the policy moved, is refused whole and the stored policy stays unchanged.
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, duplicate.Role!.Change, duplicate.Role.Choices, duplicate.Role.Read, duplicate.Role.Policy));
        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Equal(1L, Staffing.Policy("build").Revision);
        Assert.Equal(StaffRules.Weight, Staffing.Policy("build").Policy!.Rule);

        var cleared = HelpProposals.Plan(Role("set", "build", """{ "policy": null, "policyRevision": 1 }"""), config, Machine);
        Assert.Contains("→ First that can run (its policy is cleared).", cleared.Describe);
    }

    /// <summary>A default or a set of new choices clears or re-keys a policy it does not name, and the card says so.</summary>
    [Fact]
    public void A_change_that_clears_or_rekeys_a_standing_policy_says_so_on_the_card()
    {
        Staffing.Set("build", [new ActorChoice("claude-code", "sonnet", "high"), new ActorChoice("claude-code", "opus", "high")]);
        var config = StaffingProjection.Config(Staffing.Read()!);
        var even = HelpProposals.Plan(Role("set", "build", """{ "policy": "even", "policyRevision": 0 }"""), config, Machine);
        RoleDoor.Apply(Staffing, even.Role!.Change, even.Role.Choices, even.Role.Read, even.Role.Policy);

        var quiet = HelpProposals.Plan(Role("default", "intake"), config, Machine);
        Assert.DoesNotContain("Policy,", quiet.Describe);
        var given = HelpProposals.Plan(Role("default", "build"), config, Machine);
        Assert.Contains("Policy, Build: Spread by weight (1:1) → First that can run (its policy is cleared).", given.Describe);
        Assert.Contains("Policy, Build:", HelpProposals.Plan(Role("default-all", "*"), config, Machine).Describe);
        var rekeyed = HelpProposals.Plan(Role("set", "build", """{ "choices": [{ "agent": "claude-code", "model": "opus", "effort": "high" }] }"""), config, Machine);
        Assert.Contains("Policy, Build: Spread by weight (1:1) →", rekeyed.Describe);
    }

    /// <summary>In a workspace's conversation a change is judged by the role's own scope, and the whole-seed door reaches every workspace.</summary>
    [Fact]
    public void A_scoped_conversation_refuses_a_change_to_a_role_scoped_elsewhere_and_a_default_of_every_role()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(new RoleRecord("night", AuthoredText.Of("Night"), RoleRecord.Always, ["build"], new RoleScope(RoleScope.Workspace, "other"))),
            [new ActorChoice("claude-code", "sonnet", "high")]);
        var mine = Machine with { Circle = "mine" };

        Assert.Contains("workspace `other`", HelpProposals.Plan(Role("remove", "night"), DriverConfig.Empty, mine).Refusal);
        Assert.Contains("workspace `other`", HelpProposals.Plan(Role("default", "night"), DriverConfig.Empty, mine).Refusal);
        Assert.Contains("workspace `other`", HelpProposals.Plan(Role("set", "night", """{ "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, mine).Refusal);
        Assert.Contains("every workspace at once", HelpProposals.Plan(Role("default-all", "*"), DriverConfig.Empty, mine).Refusal);
        Assert.Null(HelpProposals.Plan(Role("default-all", "*"), DriverConfig.Empty, Machine).Refusal);
        Assert.Null(HelpProposals.Plan(Role("default", "intake"), DriverConfig.Empty, mine).Refusal);
    }

    [Fact]
    public void A_set_of_a_seeded_role_naming_its_choices_alone_keeps_its_record_and_sets_its_order()
    {
        var plan = HelpProposals.Plan(Role("set", "build", """{ "choices": [{ "agent": "claude-code", "model": "sonnet", "effort": "high" }] }"""), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(RoleCatalog.Seed[0].Json(), Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Json());
        Assert.Equal([new ActorChoice("claude-code", "sonnet", "high")], plan.Role.Choices);
    }

    /// <summary><i>Restore all role defaults</i>: <c>default-all</c> names no role, so the target and a revision read are not its.</summary>
    [Fact]
    public void Every_seeded_role_given_back_its_seed_names_no_role_and_its_card_says_the_terminal_twin()
    {
        RoleDoor.Apply(Staffing, RoleChange.Remove("intake"));

        var plan = HelpProposals.Plan(Role("default-all", "*", """{ "revision": 0 }"""), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.IsType<RoleChange.DefaultAllRoles>(plan.Role!.Change);
        Assert.Null(plan.Role.Read);
        Assert.Equal("daoris role default --all", plan.Terminal);
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

    /// <summary>
    /// <c>role_propose</c>'s rules, a <c>set</c> of the supervisor naming the place, judged by
    /// the rules' judge, said before and after on the card with the terminal's twin, and applied by the person's press alone.
    /// </summary>
    [Fact]
    public void CONTEXTDRIVE1d_the_supervisor_s_rules_are_judged_described_and_applied_by_the_press_alone()
    {
        const string Press = """{ "place": "repository:daoris", "rulesRevision": 0, "rules": [{ "count": "answers-alone", "at": 3, "do": "hold" }] }""";
        var store = new SupervisorRuleStore(_home, () => StoreSettled.Yes);
        store.Write("machine", 0, [new SupervisorRule("m-t", "tries", "episode", 5, "hold", null, "machine", null)], "test", new SupervisorRuleWorld(["build"]));
        var plan = HelpProposals.Plan(Role("set", "supervisor", Press), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris role rules --place repository:daoris --clear all --set answers-alone=3:hold", plan.Terminal);
        // The card carries the rules as fields, which the page words in its catalogue.
        Assert.Equal("repository:daoris", plan.Rules!.Place);
        Assert.Empty(plan.Rules.Before);
        Assert.Equal(("answers-alone", 3, "hold"), (plan.Rules.After.Single().Count, plan.Rules.After.Single().At, plan.Rules.After.Single().Do));
        Assert.Equal(0, store.Of("repository:daoris").Revision);

        RoleDoor.Apply(Staffing, plan.Role!.Change);
        Assert.Equal(3, Assert.Single(store.Of("repository:daoris").Rules).At);
        Assert.Contains("(revision)", HelpProposals.Plan(Role("set", "supervisor", Press), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("carries a place's `rules` and `rulesRevision` alone", HelpProposals.Plan(Role("set", "supervisor", """{ "takes": ["build"], "place": "machine", "rulesRevision": 1, "rules": [] }"""), DriverConfig.Empty, Machine).Refusal);
    }

    private void Converted() => HomeMigration.Run(_home, [new SupervisorStep(() => null)]);

    private const string Gatekeeper =
        """{ "kind": "supervisor", "name": { "en": "Gatekeeper", "zh": "守门人" }, "decides": ["allow", "landing"], "authority": ["allow.once", "landing.accept"], "holder": "terminal", "window": 10, "scope": { "kind": "workspace", "id": "w1" }, "revision": 0 }""";

    /// <summary>
    /// A supervisor of kinds is proposed with its kinds, acts, holder and window, judged by the role door's judge, said on the card as
    /// fields with the terminal's twin, and applied only by the person's press through that door.
    /// </summary>
    [Fact]
    public void A_supervisor_of_kinds_is_judged_described_and_applied_by_the_press_alone()
    {
        Converted();
        var plan = HelpProposals.Plan(Role("set", "gatekeeper", Gatekeeper), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "daoris role set gatekeeper --name \"Gatekeeper\" --name-in zh=\"守门人\" --scope workspace:w1 --decides allow,landing --acts allow.once,landing.accept --holder terminal --window 10",
            plan.Terminal);
        Assert.Contains("Set the supervisor `gatekeeper` (Gatekeeper · 守门人): it decides allow, landing in workspace `w1`, held by a terminal", plan.Describe);
        var view = plan.Supervisor!;
        Assert.Equal(("gatekeeper", "workspace", "w1"), (view.Id, view.Scope.Kind, view.Scope.Id));
        Assert.Equal(["Gatekeeper", "守门人"], view.Names);
        Assert.Equal(["allow", "landing"], view.Hold.Decides);
        Assert.Equal(["allow.once", "landing.accept"], view.Hold.Acts);
        Assert.Equal((RoleHolders.Terminal, 10), (view.Hold.Holder, view.Hold.Window));
        Assert.Null(view.Before);
        Assert.DoesNotContain(RoleCatalog.Read(Staffing.Location).Slots, slot => slot.Id == "gatekeeper");

        RoleDoor.Apply(Staffing, plan.Role!.Change, plan.Role.Choices, plan.Role.Read, plan.Role.Policy, RoleDoor.Doors.Proposal);
        var held = RoleCatalog.Read(Staffing.Location).Slots.Single(slot => slot.Id == "gatekeeper").Role!.Hold!;
        Assert.Equal(["allow", "landing"], held.Decides);
        Assert.Equal(["allow.once", "landing.accept"], held.Acts);
        Assert.Equal((RoleHolders.Terminal, 10), (held.Holder, held.Window));
    }

    /// <summary>A generic supervisor names no kind, a field a set leaves out keeps the role's own, and a new one is held by the person's rules with every act off.</summary>
    [Fact]
    public void A_generic_supervisor_names_no_kind_and_a_set_leaving_a_field_out_keeps_the_roles_own()
    {
        Converted();
        var generic = HelpProposals.Plan(
            Role("set", "wide", """{ "kind": "supervisor", "name": "Wide", "decides": [], "scope": { "kind": "workspace", "id": "w1" } }"""), DriverConfig.Empty, Machine);

        Assert.Null(generic.Refusal);
        Assert.Contains("--decides every --acts \"\" --holder rules --window 5", generic.Terminal);
        Assert.Contains("it decides every decision no other supervisor here takes", generic.Describe);
        Assert.Contains("granting no act, so it leaves every decision to you", generic.Describe);

        RoleDoor.Apply(Staffing, RoleChange.Set(new RoleRecord("gatekeeper", AuthoredText.Of("Gatekeeper"), RoleRecord.Appointed, [], new RoleScope(RoleScope.Workspace, "w1"),
            new RoleHold(AppointedRoles.Supervisor, ["allow.once"], RoleHolders.Terminal, 10) { Decides = [SupervisorKinds.Allow] })));
        var longer = HelpProposals.Plan(Role("set", "gatekeeper", """{ "kind": "supervisor", "window": 20, "revision": 1 }"""), DriverConfig.Empty, Machine);

        Assert.Null(longer.Refusal);
        Assert.Equal(("allow.once", RoleHolders.Terminal, 20), (longer.Supervisor!.Hold.Acts.Single(), longer.Supervisor.Hold.Holder, longer.Supervisor.Hold.Window));
        Assert.Equal(10, longer.Supervisor.Before!.Window);
        Assert.Equal("Gatekeeper", Assert.Single(longer.Supervisor.Names));
    }

    /// <summary>A proposal outside the supervisor's shape is refused with the role door's own sentence, every one before the person sees it.</summary>
    [Fact]
    public void A_supervisor_outside_the_shape_is_refused_by_the_role_door_s_judge()
    {
        Converted();
        const string Scope = """, "scope": { "kind": "workspace", "id": "w1" }""";
        RoleDoor.Apply(Staffing, RoleChange.Set(new RoleRecord("gatekeeper", AuthoredText.Of("Gatekeeper"), RoleRecord.Appointed, [], new RoleScope(RoleScope.Workspace, "w1"),
            new RoleHold(AppointedRoles.Supervisor, [], RoleHolders.Rules) { Decides = [SupervisorKinds.Allow] })));
        string? Refused(string id, string press) => HelpProposals.Plan(Role("set", id, press), DriverConfig.Empty, Machine).Refusal;

        Assert.Contains("is no kind of decision", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["cooking"], "holder": "rules" """ + Scope + "}"));
        Assert.Contains("is an act of a kind `cook` does not decide", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["allow"], "authority": ["landing.accept"], "holder": "rules" """ + Scope + "}"));
        Assert.Contains("is no act of the supervisor's", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["allow"], "authority": ["allow.fry"], "holder": "rules" """ + Scope + "}"));
        Assert.Contains("held by an agent", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["landing"], "holder": "agent" """ + Scope + "}"));
        Assert.Contains("held from 1 to 60 minutes", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["landing"], "window": 99 """ + Scope + "}"));
        Assert.Contains("never a staffing of choices", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["landing"], "choices": [{ "agent": "claude-code" }] """ + Scope + "}"));
        Assert.Contains("takes no work", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["landing"], "takes": ["build"] """ + Scope + "}"));
        Assert.Contains("a supervisor's alone", Refused("cook", """{ "kind": "steward", "name": "C", "decides": ["landing"] """ + Scope + "}"));
        Assert.Contains("allow", Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": ["allow"] """ + Scope + "}"));
        Assert.Contains("every decision", Refused("wide", """{ "kind": "supervisor", "name": "W", "decides": [] }"""));
        Assert.NotNull(Refused("cook", """{ "kind": "supervisor", "name": "C", "decides": "allow" """ + Scope + "}"));
        Assert.NotNull(Refused("supervisor", """{ "kind": "supervisor", "name": "S", "decides": ["allow"] }"""));
    }

    [Fact]
    public void A_supervisor_is_refused_until_the_home_is_converted()
    {
        var plan = HelpProposals.Plan(Role("set", "gatekeeper", Gatekeeper), DriverConfig.Empty, Machine);

        Assert.Equal(SupervisorConversion.NotConvertedSaid, plan.Refusal);
    }
}
