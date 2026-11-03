using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1d (the context counters design §4.3, §4.4): the supervisor's rules through the role door. For the id <c>supervisor</c> a press
/// carries a place's <c>rules</c> and <c>rulesRevision</c> alone, judged by the rules' judge inside the role door's one transaction: a home not
/// reached refuses <c>convert-first</c>, a moved revision <c>revision</c>, and any other field is the appointed shape.
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
    public void CONTEXTDRIVE1d_a_home_not_reached_refuses_a_rule_convert_first_and_writes_nothing()
    {
        var press = Read("""{ "id": "supervisor", "place": "repository:daoris", "rulesRevision": 0, "rules": [{ "count": "answers-alone", "at": 3, "do": "hold" }] }""");

        var refused = Assert.Throws<RuleRefusedException>(() => RoleDoor.Apply(Staffing, press.Change));

        Assert.Equal("convert-first", refused.Code);
        Assert.Empty(Store.All());
        Assert.False(Store.Reached());
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
    [InlineData("""{ "id": "supervisor", "change": "remove" }""")]
    [InlineData("""{ "id": "supervisor", "change": "set", "role": { "name": "Supervisor", "takes": ["build"] } }""")]
    public void CONTEXTDRIVE1d_any_other_field_of_the_supervisor_s_press_is_appointed_not_configurable(string json)
    {
        using var press = JsonDocument.Parse(json);

        Assert.Equal("appointed-not-configurable", Assert.Throws<RoleRefusedException>(() => RoleDoor.ReadPress(press.RootElement)).Code);
    }

    [Fact]
    public void CONTEXTDRIVE1d_set_roles_writes_a_role_and_a_place_s_rules_in_one_transaction_and_a_refused_rule_writes_neither()
    {
        Reach();
        var designer = new RolePress(RoleChange.Set(RoleDoorTests.Designer()), [new ActorChoice("claude", "opus", "high")]);
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
