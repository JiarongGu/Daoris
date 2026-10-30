using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1y-i (the foundation design §2.7, Part B's row y-i): the person's one door into the role catalogue, which the screen's
/// <c>SET_ROLE</c>, <c>daoris driver role</c> and Ask Daoris's <c>role_propose</c> each call. Every judge's code is refused with nothing
/// written; an added role keeps its record and its staffing; a removed role's work holds its starts, said; a running session goes on.
/// </summary>
public sealed class RoleDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-door", Guid.NewGuid().ToString("N")[..8]);

    public RoleDoorTests()
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

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    internal static RoleRecord Designer(RoleScope? scope = null) =>
        new("designer", "Designer", "设计师", RoleRecord.Always, [RoleWork.Design], scope ?? RoleScope.Everywhere);

    private static readonly IReadOnlyList<ActorChoice> Opus = [new ActorChoice("claude", "opus", "high")];

    private RoleCatalog Catalogue() => RoleCatalog.Read(Staffing.Location);

    private DriverConfig Config() => StaffingProjection.Config(Staffing.Read()!);

    [Fact]
    public void Every_judge_code_is_refused_at_the_door_and_nothing_is_written()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Designer()), Opus);
        var before = Catalogue().Slots.Select(slot => (slot.Id, slot.State, slot.Revision)).ToList();
        var staffed = Config().Actors.Keys.Order(StringComparer.Ordinal).ToList();

        (RoleChange Change, IReadOnlyList<ActorChoice>? Choices, string Code)[] cases =
        [
            (RoleChange.Set(Designer() with { Id = "lead", Filled = RoleRecord.Appointed }), Opus, "appointed-not-configurable"),
            (RoleChange.Set(Designer() with { Id = "lead", Authority = ["answer"] }), Opus, "mixed-shape"),
            (RoleChange.Set(Designer() with { Id = "lead", Takes = ["gardening"] }), Opus, "unknown-work"),
            (RoleChange.Set(Designer() with { Id = "steward" }), Opus, "reserved-id"),
            (RoleChange.Set(Designer() with { Id = "Lead!" }), Opus, "bad-record"),
            (RoleChange.Remove("build"), null, "may-not-be-off"),
            (RoleChange.Set(Designer() with { Id = "second" }), Opus, "two-roles-one-work"),
            (RoleChange.Set(Designer() with { Id = "night", Takes = [RoleWork.Build], Scope = new RoleScope(RoleScope.Workspace, "w1") }), [], "may-not-be-off"),
            (RoleChange.Set(Designer() with { Id = "night", Scope = new RoleScope(RoleScope.Workspace, "w1") }), [.. Enumerable.Repeat(Opus[0], 5)], "choices"),
        ];

        foreach (var (change, choices, code) in cases)
        {
            var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, change, choices));
            Assert.Equal(code, refused.Code);
        }

        Assert.Equal(before, Catalogue().Slots.Select(slot => (slot.Id, slot.State, slot.Revision)).ToList());
        Assert.Equal(staffed, Config().Actors.Keys.Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void An_added_role_keeps_its_record_and_its_staffing_a_change_a_new_revision_and_its_removal_both_gone()
    {
        Assert.Equal(1L, RoleDoor.Apply(Staffing, RoleChange.Set(Designer()), Opus));

        var slot = Catalogue().Slots.Single(each => each.Id == "designer");
        Assert.Equal((RoleState.Set, "设计师"), (slot.State, slot.Role!.NameZh));
        Assert.Equal([new ActorEffective("claude", "opus", "high", "1")], Catalogue().Order(RoleWork.Design, new RolePlace(), Config()));

        Assert.Equal(2L, RoleDoor.Apply(Staffing, RoleChange.Set(Designer() with { NameEn = "Architect" }), null, read: 1));
        Assert.Equal("Architect", Catalogue().Slots.Single(each => each.Id == "designer").Role!.NameEn);
        Assert.Equal(Opus, Config().Actors["designer"]);

        var stale = Assert.Throws<DriverException>(() => RoleDoor.Apply(Staffing, RoleChange.Remove("designer"), read: 1));
        Assert.Contains("changed since", stale.Message);

        Assert.Equal(3L, RoleDoor.Apply(Staffing, RoleChange.Remove("designer"), read: 2));
        Assert.DoesNotContain(Catalogue().Slots, each => each.Id == "designer");
        Assert.False(Config().Actors.ContainsKey("designer"));
        Assert.Equal("build", Catalogue().RoleFor(RoleWork.Design, new RolePlace()).Role!.Id);
    }

    [Fact]
    public void A_new_added_role_naming_no_choices_is_off_where_its_work_may_be_off_and_refused_where_it_may_not()
    {
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Designer())));
        Assert.Equal("may-not-be-off", refused.Code);

        var helper = Designer(new RoleScope(RoleScope.Workspace, "w1")) with { Id = "w-intake", Takes = [RoleWork.Intake] };
        RoleDoor.Apply(Staffing, RoleChange.Set(helper));
        Assert.Empty(Config().Actors["w-intake"]);
    }

    [Fact]
    public void A_removed_role_s_work_holds_its_starts_said_and_the_file_written_off_for_an_older_build()
    {
        RoleDoor.Apply(Staffing, RoleChange.Remove("intake"));

        var catalogue = Catalogue();
        Assert.Equal(RoleCatalog.NoRole(RoleWork.Intake), catalogue.RoleFor(RoleWork.Intake, new RolePlace()).Held);
        var held = Assert.Throws<DriverException>(() =>
            new StartPlan(RoleWork.Intake, Config(), null!, null!, new PassMemory(), catalogue: catalogue) { Subject = "ask:a1" });
        Assert.Equal(RoleCatalog.NoRole(RoleWork.Intake), held.Message);

        // The store keeps Intake's staffing, so giving the work a role again starts it as before; the file says off for an older build.
        Assert.Equal("codex", Config().IntakeAdapter);
        Assert.Contains("\"intake\":[]", File.ReadAllText(File_).Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);

        RoleDoor.Apply(Staffing, RoleChange.Default("intake"));
        Assert.Equal("intake", Catalogue().RoleFor(RoleWork.Intake, new RolePlace()).Role!.Id);
    }

    [Fact]
    public void A_running_session_goes_on_untouched_by_its_role_s_removal()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Designer()), Opus);
        var record = new PriorSession("s1", null, "working") { Actor = "build", Adapter = "claude", Asked = new ActorAsked("claude", new ActorValue("entry", "opus", true), new ActorValue("entry", "high", true)) };

        RoleDoor.Apply(Staffing, RoleChange.Remove("designer"));

        var after = Catalogue();
        Assert.Equal("claude", StartPlan.ContinuedOn(record, Config(), RoleWork.Design, bound: "designer", catalogue: after));
        Assert.Equal("claude", StartPlan.ContinuedOn(record with { Asked = null, Adapter = null }, Config(), RoleWork.Design, bound: "designer", catalogue: after));
    }

    [Fact]
    public void No_settings_grant_reaches_a_role_or_an_actor()
    {
        Assert.DoesNotContain(ConfiguredAccess.SettingsBuilt, setting => setting.Contains("role", StringComparison.OrdinalIgnoreCase)
            || setting.Contains("actor", StringComparison.OrdinalIgnoreCase) || setting.Contains("staff", StringComparison.OrdinalIgnoreCase));
    }
}
