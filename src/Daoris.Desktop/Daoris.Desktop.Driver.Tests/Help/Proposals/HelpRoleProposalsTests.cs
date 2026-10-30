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
        Assert.Equal("daoris driver role set designer --name \"Designer\" --zh \"设计师\" --takes design --scope machine \"claude-code opus high\"", plan.Terminal);
        Assert.Equal(RoleSuggestion.Designer.Role.Json(), Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Json());
    }

    [Fact]
    public void Every_judge_code_is_refused_at_this_door_with_the_judge_s_sentence()
    {
        var seeded = RoleCatalog.Of([]);
        Assert.Equal(seeded.Judge(RoleChange.Remove("build"))!.Said, HelpProposals.Plan(Role("remove", "build"), DriverConfig.Empty, Machine).Refusal);
        Assert.Equal(
            seeded.Judge(RoleChange.Set(new RoleRecord("steward", "S", "S", RoleRecord.Always, ["build"], RoleScope.Everywhere)))!.Said,
            HelpProposals.Plan(Role("set", "steward", """{ "name": { "en": "S", "zh": "S" }, "takes": ["build"] }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("is not a work", HelpProposals.Plan(Role("set", "lead", """{ "name": { "en": "L", "zh": "L" }, "takes": ["gardening"] }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("cannot be off", HelpProposals.Plan(Role("set", "night", """{ "name": { "en": "N", "zh": "N" }, "takes": ["build"], "scope": { "kind": "workspace", "id": "w1" } }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Contains("already takes", HelpProposals.Plan(Role("set", "chatty", """{ "name": { "en": "C", "zh": "C" }, "takes": ["chat"], "choices": [{ "agent": "claude-code" }] }"""), DriverConfig.Empty, Machine).Refusal);
    }

    [Fact]
    public void A_set_of_a_seeded_role_naming_its_choices_alone_keeps_its_record_and_sets_its_order()
    {
        var plan = HelpProposals.Plan(Role("set", "build", """{ "choices": [{ "agent": "claude-code", "model": "sonnet", "effort": "high" }] }"""), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(RoleCatalog.Seed[0].Json(), Assert.IsType<RoleChange.SetRole>(plan.Role!.Change).Role.Json());
        Assert.Equal([new ActorChoice("claude-code", "sonnet", "high")], plan.Role.Choices);
    }

    [Fact]
    public void A_change_made_since_the_revision_read_is_refused()
    {
        RoleDoor.Apply(Staffing, RoleChange.Remove("intake"));

        Assert.Contains("changed since it was read (revision 0, now 1)", HelpProposals.Plan(Role("default", "intake", """{ "revision": 0 }"""), DriverConfig.Empty, Machine).Refusal);
        Assert.Null(HelpProposals.Plan(Role("default", "intake", """{ "revision": 1 }"""), DriverConfig.Empty, Machine).Refusal);
    }
}
