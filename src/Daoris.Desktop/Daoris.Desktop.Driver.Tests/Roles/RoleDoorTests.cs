using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's one door into the role catalogue, which the screen's
/// <c>SET_ROLE</c>, <c>daoris role</c> and Ask Daoris's <c>role_propose</c> each call. Every judge's code is refused with nothing
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
        // The seeded Design role takes design machine-wide; these cases add a role taking it, so it is removed first.
        RoleCatalog.Write(Staffing.Location, RoleChange.Remove("design"), () => StoreSettled.Yes);
        Assert.Null(HomeMigration.Run(_home, [new SupervisorStep(() => null)]).Failed);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing { get; }

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    internal static RoleRecord Designer(RoleScope? scope = null) =>
        new("designer", AuthoredText.FromTags([new("en", "Designer"), new("zh", "设计师")])!, RoleRecord.Always, [RoleWork.Design], scope ?? RoleScope.Everywhere);

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
            (RoleChange.Set(Designer(new RoleScope(RoleScope.Workspace, "w1")) with { Id = "lead", Filled = RoleRecord.Appointed, Takes = [], Hold = new RoleHold(AppointedRoles.Supervisor, SupervisorKinds.Seeded, RoleHolders.Terminal) }), Opus, "appointed-not-configurable"),
            (RoleChange.Set(Designer() with { Id = "lead", Filled = RoleRecord.Appointed, Takes = [], Hold = new RoleHold(AppointedRoles.Supervisor, SupervisorKinds.Seeded, RoleHolders.Terminal) }), null, "two-roles-one-work"),
            (RoleChange.Set(Designer() with { Id = "lead", Hold = new RoleHold(AppointedRoles.Supervisor, ["question.answer"], RoleHolders.Rules) }), Opus, "mixed-shape"),
            (RoleChange.Set(Designer() with { Id = "lead", Takes = ["gardening"] }), Opus, "unknown-work"),
            (RoleChange.Set(Designer() with { Id = "steward" }), Opus, "mixed-shape"),
            (RoleChange.Remove(AppointedRoles.Supervisor), null, "may-not-be-off"),
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

    /// <summary>The supervisor and the steward are staffed through the one door, their holder and window in the record, no staffing row.</summary>
    [Fact]
    public void The_supervisor_and_the_steward_are_staffed_through_the_role_door_and_read_back()
    {
        var supervisor = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor);
        var steward = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Steward);
        var place = new RoleRecord("supervisor-engine", AuthoredText.Of("Engine's supervisor"), RoleRecord.Appointed, [], new RoleScope(RoleScope.Repository, "engine"),
            new RoleHold(AppointedRoles.Supervisor, SupervisorKinds.Seeded, RoleHolders.Rules));

        RoleDoor.ApplyAll(Staffing, [
            (RoleChange.Set(supervisor with { Hold = supervisor.Hold! with { Holder = RoleHolders.Terminal, Window = 12 } }), null, null),
            (RoleChange.Set(steward with { Hold = steward.Hold! with { Holder = RoleHolders.Terminal } }), null, null),
            (RoleChange.Set(place), null, null),
        ]);

        var catalogue = Catalogue();
        var held = catalogue.Slots.Single(slot => slot.Id == AppointedRoles.Supervisor);
        Assert.Equal((RoleState.Set, RoleHolders.Terminal, 12), (held.State, held.Role!.Hold!.Holder, held.Role.Hold.Window));
        Assert.Equal(RoleHolders.Terminal, AppointedRoles.StewardOf(catalogue.HoldingActs)!.Hold!.Holder);
        Assert.Equal("supervisor-engine", catalogue.Supervising(SupervisorKinds.Question, new RolePlace(Repository: "engine")).Role!.Id);
        Assert.DoesNotContain(AppointedRoles.Supervisor, Config().Actors.Keys);
        Assert.Equal(RoleHolders.Terminal, RoleDoor.Rows(catalogue, Config()).Single(row => row.Id == AppointedRoles.Supervisor).Hold!.Holder);

        RoleDoor.Apply(Staffing, RoleChange.Default(AppointedRoles.Steward));
        Assert.Null(AppointedRoles.StewardOf(Catalogue().HoldingActs)!.Hold!.Holder);
    }

    [Fact]
    public void A_supervisor_s_record_is_refused_not_converted_until_the_home_s_step_is_recorded_and_written_after()
    {
        File.Delete(DaorisHome.Entry(_home, HomeEntry.Migration));
        var (home, staffing) = (_home, Staffing);
        var gatekeeper = new RoleRecord("gatekeeper", AuthoredText.Of("Gatekeeper"), RoleRecord.Appointed, [], new RoleScope(RoleScope.Workspace, "w1"),
            new RoleHold(AppointedRoles.Supervisor, [], RoleHolders.Rules) { Decides = [SupervisorKinds.Allow] });

        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(staffing, RoleChange.Set(gatekeeper)));
        Assert.Equal((SupervisorStep.NotConverted, SupervisorStep.NotConvertedSaid), (refused.Code, refused.Message));
        Assert.Contains(SupervisorStep.NotConverted, RoleCatalog.Codes);

        HomeMigration.Run(home, [new SupervisorStep(() => null)]);
        RoleDoor.Apply(staffing, RoleChange.Set(gatekeeper));
        Assert.Equal([SupervisorKinds.Allow], RoleCatalog.Read(staffing.Location).Slots.Single(slot => slot.Id == "gatekeeper").Role!.Hold!.Decides);
    }

    /// <summary>A role is named in one language, or in a third, and an old pair reads unchanged; each is shown in the viewer's language, else as written.</summary>
    [Fact]
    public void A_role_named_in_one_language_or_a_third_is_saved_and_read_back_and_an_old_pair_reads_unchanged()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Designer() with { Name = AuthoredText.Of("Architect") }), Opus);
        RoleDoor.Apply(Staffing, RoleChange.Set(Designer(new RoleScope(RoleScope.Workspace, "w1")) with { Id = "relecteur", Name = AuthoredText.FromTags([new("fr", "Relecteur")])! }), Opus);

        var rows = RoleDoor.Rows(Catalogue(), Config()).ToDictionary(row => row.Id);
        Assert.Equal(("Architect", "Architect"), (rows["designer"].Name!.In("zh"), rows["designer"].Name!.In("fr")));
        Assert.Equal(("Relecteur", "Relecteur", """{"fr":"Relecteur"}"""), (rows["relecteur"].Name!.In("fr"), rows["relecteur"].Name!.In("en"), rows["relecteur"].Name!.Json()));

        // The record a build before this wrote, { en, zh } both, is a valid record as it stands.
        var (old, why) = RoleRecord.Parse("designer", """{"id":"designer","name":{"en":"Designer","zh":"设计师"},"filled":"always","takes":["design"],"scope":{"kind":"machine"}}""");
        Assert.Null(why);
        Assert.Equal(("设计师", "Designer"), (old!.Name.In("zh-TW"), old.Name.In("en")));
        Assert.Contains("\"name\":{\"en\":\"Designer\",\"zh\":\"设计师\"}", old.Json());

        // A press carries the name as the page sends it: one text, or a map of tags to texts.
        using var press = System.Text.Json.JsonDocument.Parse("""{"change":"set","id":"lecteur","role":{"name":{"fr":"Lecteur"},"takes":["review"]}}""");
        var (change, _, _) = RoleDoor.Read(press.RootElement);
        Assert.Equal("""{"fr":"Lecteur"}""", Assert.IsType<RoleChange.SetRole>(change).Role.Name.Json());
        using var blank = System.Text.Json.JsonDocument.Parse("""{"change":"set","id":"lecteur","role":{"name":"  ","takes":["review"]}}""");
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleDoor.Read(blank.RootElement).Change, null));
        Assert.Equal("bad-record", refused.Code);
    }

    [Fact]
    public void An_added_role_keeps_its_record_and_its_staffing_a_change_a_new_revision_and_its_removal_both_gone()
    {
        Assert.Equal(1L, RoleDoor.Apply(Staffing, RoleChange.Set(Designer()), Opus));

        var slot = Catalogue().Slots.Single(each => each.Id == "designer");
        Assert.Equal((RoleState.Set, "设计师"), (slot.State, slot.Role!.Name.In("zh")));
        Assert.Equal([new ActorEffective("claude", "opus", "high", "1")], Catalogue().Order(RoleWork.Design, new RolePlace(), Config()));

        Assert.Equal(2L, RoleDoor.Apply(Staffing, RoleChange.Set(Designer() with { Name = AuthoredText.Of("Architect") }), null, read: 1));
        Assert.Equal("Architect", Catalogue().Slots.Single(each => each.Id == "designer").Role!.Name.AsWritten);
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

        // The store keeps Intake's staffing, so giving the work a role again starts it as before.
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
