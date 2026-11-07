using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The supervisor's rules through the role door. For the id <c>supervisor</c> a press
/// carries a place's <c>rules</c> and <c>rulesRevision</c> alone, judged by the rules' judge inside the role door's one transaction: a home not
/// seeded takes a rule with no conversion asked, a moved revision is <c>revision</c>, and any other field is the appointed shape.
/// </summary>
public sealed class SupervisorRuleDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-rule-door", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorRuleDoorTests()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude", "intakeAdapter": "codex", "actors": { "build": [ { "agent": "claude", "model": "sonnet" } ] } }""");
        Staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, Staffing.ImportFile().How);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing { get; }

    private SupervisorRuleStore Store => new(_home, () => StoreSettled.Yes);

    private const string Seeded = """
        [{ "id": "m-aa", "count": "answers-alone", "at": 2, "do": "hold" },
         { "id": "m-f", "count": "failures", "at": 3, "do": "hold" },
         { "id": "m-t", "count": "tries", "at": 5, "do": "hold" }]
        """;

    private void Reach() =>
        Assert.Equal(1, Store.Write("machine", 0, Press($$"""{ "id": "supervisor", "place": "machine", "rulesRevision": 0, "rules": {{Seeded}} }""").Rules, "test",
            new SupervisorRuleWorld(["build", "chat", "helper"])).Revision);

    private static SupervisorRulesPress Press(string json)
    {
        using var press = JsonDocument.Parse(json);
        return Assert.IsType<RoleChange.SetRules>(RoleDoor.ReadPress(press.RootElement).Change).Press;
    }

    private static RolePress Read(string json)
    {
        using var press = JsonDocument.Parse(json);
        return RoleDoor.ReadPress(press.RootElement.Clone());
    }

    [Fact]
    public void CONTEXTDRIVE1r_a_repository_s_chain_is_read_from_the_store_in_every_process_and_answered_in_ROLES()
    {
        Assert.True(SupervisorRuleBook.SeedFresh(_home, () => StoreSettled.Yes));
        // The look keeps the registry's workspaces; an empty registry keeps what it kept.
        Assert.True(new RuleWiderPlaceStore(_home, () => StoreSettled.Yes).Keep([("engine", "w1"), ("atlas", null)]));
        Assert.False(new RuleWiderPlaceStore(_home, () => StoreSettled.Yes).Keep([]));
        Assert.False(new RuleWiderPlaceStore(_home, () => StoreSettled.Yes).Keep([("engine", "w1")]));
        Assert.Equal(1, Store.Write("workspace:w1", 0, Press("""{ "id": "supervisor", "place": "workspace:w1", "rulesRevision": 0, "rules": [{ "count": "moves", "at": 3, "do": "ask" }] }""").Rules,
            "test", SupervisorRuleDoor.World(_home)).Revision);
        Assert.Equal(1, Store.Write("repository:engine", 0, Press("""{ "id": "supervisor", "place": "repository:engine", "rulesRevision": 0, "rules": [{ "count": "limits", "at": 1, "do": "weighted", "roles": ["build"] }] }""").Rules,
            "test", SupervisorRuleDoor.World(_home)).Revision);

        // A terminal's own process reads the same chain: a machine write is judged on engine's chain through w1, and stands.
        var machine = Store.Of("machine");
        var write = Press($$"""{ "id": "supervisor", "place": "machine", "rulesRevision": {{machine.Revision}}, "rules": [{ "count": "answers-alone", "at": 3, "do": "hold" }, { "count": "failures", "at": 3, "do": "hold" }, { "count": "tries", "at": 5, "do": "hold" }] }""");
        Assert.Null(SupervisorRuleDoor.Judge(_home, write));

        var rows = SupervisorRuleRows.Of(_home);
        Assert.Equal(["workspace:w1"], rows.Wider["repository:engine"]);
        Assert.False(rows.Wider.ContainsKey("repository:atlas"));
    }

    [Fact]
    public void CONTEXTDRIVE1l_a_seeded_home_takes_a_rule_with_no_conversion_asked()
    {
        Assert.True(SupervisorRuleBook.SeedFresh(_home,() => StoreSettled.Yes));
        var press = Read("""{ "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0, "rules": [{ "count": "answers-alone", "at": 3, "do": "hold" }] }""");

        RoleDoor.Apply(Staffing, press.Change);

        Assert.Equal(1, Store.Of("repository:daoris").Revision);
        Assert.DoesNotContain("convert-first", SupervisorRules.Codes);
    }

    [Fact]
    public void CONTEXTDRIVE1d_a_press_is_kept_whole_with_the_next_revision_and_a_moved_revision_is_refused_with_nothing_written()
    {
        Reach();
        var first = Read("""{ "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0, "rules": [{ "count": "answers-alone", "at": 3, "do": "hold", "roles": ["build"] }] }""");

        Assert.Equal(1, RoleDoor.Apply(Staffing, first.Change, door: RoleDoor.Doors.Terminal));

        var kept = Store.Of("repository:daoris");
        var rule = Assert.Single(kept.Rules);
        Assert.Equal((1L, "r-1", "in-a-row", 3, "repository:daoris", "build"), (kept.Revision, rule.Id, rule.Window, rule.At, rule.Place, rule.Roles!.Single()));

        var stale = Read("""{ "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0, "rules": [] }""");
        Assert.Equal("revision", Assert.Throws<RuleRefusedException>(() => RoleDoor.Apply(Staffing, stale.Change)).Code);
        Assert.Single(Store.Of("repository:daoris").Rules);
    }

    [Theory]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "takes": ["build"] }, "place": "machine", "rulesRevision": 0, "rules": [] }""")]
    [InlineData("""{ "id": "supervisor", "choices": [{ "agent": "claude" }], "place": "machine", "rulesRevision": 0, "rules": [] }""")]
    [InlineData("""{ "id": "supervisor", "policy": "even", "place": "machine", "rulesRevision": 0, "rules": [] }""")]
    [InlineData("""{ "id": "supervisor", "revision": 2, "place": "machine", "rulesRevision": 0, "rules": [] }""")]
    [InlineData("""{ "id": "supervisor", "change": "remove", "place": "machine" }""")]
    public void CONTEXTDRIVE1d_a_rules_press_carrying_a_role_s_field_is_a_mixed_shape(string json)
    {
        using var press = JsonDocument.Parse(json);

        Assert.Equal("mixed-shape", Assert.Throws<RoleRefusedException>(() => RoleDoor.ReadPress(press.RootElement)).Code);
    }

    /// <summary>The supervisor's own record is a role's press, judged as every role's is.</summary>
    [Theory]
    [InlineData("""{ "id": "supervisor", "change": "remove" }""", "may-not-be-off")]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "takes": ["build"] } }""", "mixed-shape")]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "filled": "appointed", "kind": "supervisor", "authority": ["question.answer", "course.go-on", "limit.move"], "holder": "agent" } }""", "appointed-not-configurable")]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "filled": "appointed", "kind": "supervisor", "authority": ["question.answer", "course.go-on", "limit.move"], "holder": "terminal" }, "choices": [{ "agent": "claude" }] }""", "appointed-not-configurable")]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "filled": "appointed", "kind": "supervisor", "authority": ["question.answer", "course.go-on", "limit.move"], "holder": "terminal", "window": 12 } }""", null)]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "filled": "appointed", "kind": "supervisor", "authority": ["answer"], "holder": "terminal" } }""", "bad-record")]
    public void ROLEONE1_the_supervisor_s_own_press_is_a_role_s_press_its_judge_reads(string json, string? code)
    {
        using var press = JsonDocument.Parse(json);
        var read = RoleDoor.ReadPress(press.RootElement);

        Assert.Equal(code, RoleDoor.Judge(RoleCatalog.Of([]), read.Change, read.Choices)?.Code);
    }

    [Fact]
    public void CONTEXTDRIVE1d_set_roles_writes_a_role_and_a_place_s_rules_in_one_transaction_and_a_refused_rule_writes_neither()
    {
        Reach();
        var designer = new RolePress(RoleChange.Set(RoleDoorTests.Designer(new RoleScope(RoleScope.Workspace, "w1"))),[new ActorChoice("claude", "opus", "high")]);
        var unbounded = Read("""{ "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0, "rules": [{ "count": "quiet", "at": 1, "do": "next" }] }""");

        Assert.Equal("moves-unbounded", Assert.Throws<RuleRefusedException>(() => RoleDoor.ApplyAll(Staffing, [designer, unbounded])).Code);
        Assert.DoesNotContain(RoleCatalog.Read(Staffing.Location).Slots, slot => slot.Id == "designer");
        Assert.Equal(0, Store.Of("repository:daoris").Revision);

        var bounded = Read("""
            { "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0,
              "rules": [{ "count": "quiet", "at": 1, "do": "next" }, { "count": "moves", "at": 3, "do": "ask" }] }
            """);
        var revisions = RoleDoor.ApplyAll(Staffing, [designer, bounded]);

        Assert.Equal(1, revisions[1]);
        Assert.Contains(RoleCatalog.Read(Staffing.Location).Slots, slot => slot.Id == "designer");
        Assert.Equal(["quiet", "moves"], Store.Of("repository:daoris").Rules.Select(rule => rule.Count));
    }

    /// <summary>A preset on a reached home writes its choices into the policy and its limit move and bound as rules in the same press.</summary>
    [Fact]
    public void CONTEXTDRIVE1e_a_preset_on_a_reached_home_writes_its_moves_as_rules_naming_the_role()
    {
        Reach();
        Staffing.Set("build", [new ActorChoice("claude", "sonnet", "high"), new ActorChoice("claude", "opus", "high")]);

        RoleDoor.Apply(Staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(Preset: StaffPresets.Even));

        var saved = Staffing.Policy("build").Policy!;
        Assert.Equal((StaffRules.Weight, 0, (int?)null), (saved.Rule, saved.Moves.Count, saved.Most));
        var machine = Store.Of("machine");
        Assert.Equal(2L, machine.Revision);
        Assert.Contains(machine.Rules, rule => rule is { Count: "limits", Do: "weighted" } && rule.Roles!.Single() == "build");
        Assert.Contains(machine.Rules, rule => rule is { Count: "moves", At: 3, Do: "ask" } && rule.Roles!.Single() == "build");
    }

    [Theory]
    [InlineData("""{ "id": "supervisor", "rulesRevision": 0, "rules": [] }""", "place")]
    [InlineData("""{ "id": "supervisor", "place": "machine", "rules": [] }""", "revision")]
    [InlineData("""{ "id": "supervisor", "place": "machine", "rulesRevision": 0 }""", "count")]
    [InlineData("""{ "id": "supervisor", "place": "machine", "rulesRevision": 0, "rules": [{ "count": "tries", "do": "hold" }] }""", "at")]
    [InlineData("""{ "id": "supervisor", "place": "machine", "rulesRevision": 0, "rules": [{ "count": "tries", "at": 5, "do": "hold", "roles": "build" }] }""", "roles")]
    public void CONTEXTDRIVE1d_rules_that_do_not_read_are_refused_by_the_judge_s_code(string json, string code)
    {
        using var press = JsonDocument.Parse(json);

        Assert.Equal(code, Assert.Throws<RuleRefusedException>(() => RoleDoor.ReadPress(press.RootElement)).Code);
    }
}
