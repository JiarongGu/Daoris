using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w, the start proof (Part B's row w, O2-6): the catalogue's own answer for a start beside today's start plan, which it is not
/// wired to (row x does that). A catalogue with one added role reproduces the eight seeded roles' starts wherever the added role does not
/// reach, and each role's staffing is read from <c>role_staffing</c> through the store's own five fields.
/// </summary>
public sealed class RoleCatalogStartTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-catalog-start", Guid.NewGuid().ToString("N")[..8]);

    public RoleCatalogStartTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly RolePlace Machine = new();

    private static StartPlan Today(string work, DriverConfig config) =>
        new(work, config, null!, null!, new PassMemory()) { Subject = "quest:q1" };

    private static DriverConfig Staffed() =>
        ActorChoices.With(
            ActorChoices.With(DriverConfig.Empty with { Adapter = "claude", IntakeAdapter = "codex" }, "build", [new ActorChoice("codex", "gpt"), new ActorChoice("claude")]),
            "brainstorm", [new ActorChoice("claude"), new ActorChoice("codex")]);

    private static void SameAsToday(RoleCatalog catalog, RolePlace place, DriverConfig config)
    {
        foreach (var work in ParticipantWork.Codes)
        {
            var plan = Today(work, config);
            var found = catalog.RoleFor(work, place);
            Assert.Equal(plan.Role, found.Role!.Id);
            Assert.Equal(plan.Order, catalog.Order(work, place, config));
        }
    }

    [Fact]
    public void A_catalogue_with_one_added_role_reproduces_the_eight_seeded_roles_starts_beside_today_s_plan()
    {
        var config = Staffed() with { Actors = new Dictionary<string, IReadOnlyList<ActorChoice>> { ["designer"] = [new ActorChoice("opus-agent", "opus", "high")] } };
        var designer = RoleCatalogTests.Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"));

        var catalog = RoleCatalog.Of([RoleCatalogTests.Stored(designer)]);

        Assert.Equal(["build", "intake", "opinion", "helper", "partner", "brainstorm", "ask-daoris", "chat", "designer"], catalog.Slots.Select(slot => slot.Id));
        SameAsToday(catalog, Machine, config);
        SameAsToday(catalog, new RolePlace(Workspace: "w2"), config);
        SameAsToday(RoleCatalog.Of([]), new RolePlace(Workspace: "w1"), config);

        var inside = new RolePlace(Workspace: "w1");
        Assert.Equal("designer", catalog.RoleFor(ParticipantWork.Design, inside).Role!.Id);
        Assert.Equal([new ActorEffective("opus-agent", "opus", "high", "1")], catalog.Order(ParticipantWork.Design, inside, config));
        Assert.All(ParticipantWork.Codes.Where(work => work != ParticipantWork.Design), work => Assert.Equal(Today(work, config).Order, catalog.Order(work, inside, config)));
        Assert.Equal("build", Today(ParticipantWork.Design, config).Role);
    }

    [Fact]
    public void An_added_role_taking_a_seeded_work_in_a_workspace_leaves_the_machine_and_other_places_as_they_were()
    {
        var config = Staffed() with { Actors = new Dictionary<string, IReadOnlyList<ActorChoice>> { ["night"] = [new ActorChoice("night-agent")] } };
        var night = RoleCatalogTests.Working("night", ParticipantWork.Build, new RoleScope(RoleScope.Repository, "r1"));
        var catalog = RoleCatalog.Of([RoleCatalogTests.Stored(night)]);

        SameAsToday(catalog, Machine, config);
        SameAsToday(catalog, new RolePlace("w1", "r2"), config);
        Assert.Equal([new ActorEffective("night-agent", null, null, "1")], catalog.Order(ParticipantWork.Build, new RolePlace("w1", "r1"), config));
    }

    [Fact]
    public void Every_role_s_staffing_is_read_from_the_store_s_role_staffing_the_added_role_s_included()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude", "intakeAdapter": "codex", "actors": { "build": [ { "agent": "codex", "model": "gpt" } ], "chat": [ { "agent": "claude" } ] } }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        var designer = RoleCatalogTests.Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"));
        RoleCatalog.Write(DesktopStore.LocationIn(_home), RoleChange.Set(designer), () => StoreSettled.Yes);
        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("role_staffing", write => write.Execute(
                "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ('designer', 'set', '[{\"agent\":\"opus-agent\"}]', NULL, NULL, '2026-10-30T00:00:00.0000000+00:00')"));
        }

        var config = StaffingProjection.Config(staffing.Read()!);
        var catalog = RoleCatalog.Read(DesktopStore.LocationIn(_home));

        SameAsToday(catalog, Machine, config);
        Assert.Equal([new ActorEffective("codex", "gpt", null, "1")], catalog.Order(ParticipantWork.Build, Machine, config));
        Assert.Equal([new ActorEffective("opus-agent", null, null, "1")], catalog.Order(ParticipantWork.Design, new RolePlace(Workspace: "w1"), config));
        Assert.Equal([new ActorEffective("codex", null, null, "before")], catalog.Order(ParticipantWork.Intake, Machine, config));
    }
}
