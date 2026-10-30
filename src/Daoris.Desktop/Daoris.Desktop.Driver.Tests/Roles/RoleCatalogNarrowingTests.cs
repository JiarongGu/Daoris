using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w, the projection over scoped roles (the foundation design §19.15, Part B's row w; <i>O5-F3</i>, <i>C6-1.7</i>, <i>O7-F8</i>):
/// where Intake's or Ask Daoris's work is off, removed or unreadable in any scope, or an added record's work does not read, the file's entry
/// is <c>[]</c> and its key absent, so an older build starts neither; the store's own keys are never narrowed, so the newer build's machine
/// Intake still starts on its agent.
/// </summary>
public sealed class RoleCatalogNarrowingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-catalog-narrow", Guid.NewGuid().ToString("N")[..8]);

    private static readonly RolePlace Machine = new();

    private static readonly RolePlace InWorkspace = new(Workspace: "w1");

    public RoleCatalogNarrowingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static DriverConfig Store(params (string Role, ActorChoice[] Choices)[] entries) =>
        DriverConfig.Empty with
        {
            Adapter = "claude", IntakeAdapter = "codex", HelperAdapter = "claude",
            Actors = entries.ToDictionary(entry => entry.Role, entry => (IReadOnlyList<ActorChoice>)entry.Choices),
        };

    private static RoleCatalog Catalog(params StoredRole[] rows) => RoleCatalog.Of(rows);

    private static RoleRecord Added(string id, string work) =>
        RoleCatalogTests.Working(id, work, new RoleScope(RoleScope.Workspace, "w1"));

    /// <summary>The file's paths the projection writes, read as an older build reads them.</summary>
    private static DriverConfig OlderBuild(DriverConfig store, RoleCatalog catalog) =>
        StaffingProjection.Config(StaffingProjection.Project(store, new Dictionary<string, string>(), null, catalog.Narrowed(store)));

    [Fact]
    public void A_workspace_s_Intake_off_with_the_machine_s_on_starts_no_intake_in_an_older_build_and_the_newer_machine_intake_still_starts()
    {
        var catalog = Catalog(RoleCatalogTests.Stored(Added("w-intake", ParticipantWork.Intake)));
        var store = Store(("intake", [new ActorChoice("codex")]), ("w-intake", []));

        Assert.Equal(["intake"], catalog.Narrowed(store));
        var older = OlderBuild(store, catalog);
        Assert.False(StaffingProjection.Starts("intake", older));
        Assert.Empty(older.Actors["intake"]);
        Assert.Null(older.IntakeAdapter);
        Assert.True(StaffingProjection.Starts("ask-daoris", older));

        Assert.Equal([new ActorEffective("codex", null, null, "1")], catalog.Order(ParticipantWork.Intake, Machine, store));
        Assert.Empty(catalog.Order(ParticipantWork.Intake, InWorkspace, store));
    }

    [Fact]
    public void With_the_machine_s_Intake_as_before_the_store_s_key_is_not_narrowed_and_the_newer_machine_intake_starts_on_its_agent()
    {
        var catalog = Catalog(RoleCatalogTests.Stored(Added("w-intake", ParticipantWork.Intake)));
        var store = Store(("w-intake", []));

        var older = OlderBuild(store, catalog);

        Assert.False(StaffingProjection.Starts("intake", older));
        Assert.Equal("codex", store.IntakeAdapter);
        Assert.Equal([new ActorEffective("codex", null, null, "before")], catalog.Order(ParticipantWork.Intake, Machine, store));
    }

    [Fact]
    public void A_work_removed_scoped_away_or_unreadable_in_any_scope_is_narrowed_and_a_working_one_is_not()
    {
        var store = Store(("w-intake", [new ActorChoice("codex")]));
        Assert.Empty(Catalog().Narrowed(store));
        Assert.Empty(Catalog(RoleCatalogTests.Stored(Added("w-intake", ParticipantWork.Intake))).Narrowed(store));

        Assert.Equal(["ask-daoris"], Catalog(new StoredRole("ask-daoris", "removed", "{}", 1)).Narrowed(store));
        var rescoped = RoleCatalog.Seed.Single(role => role.Id == "intake") with { Scope = new RoleScope(RoleScope.Workspace, "w1") };
        Assert.Equal(["intake"], Catalog(RoleCatalogTests.Stored(rescoped)).Narrowed(store));
        Assert.Equal(["intake"], Catalog(new StoredRole("intake", "set", "{", 1)).Narrowed(store));
        Assert.Equal(["ask-daoris", "intake"], Catalog(RoleCatalogTests.Stored(Added("w-intake", ParticipantWork.Intake)), new StoredRole("x-role", "set", "{", 1)).Narrowed(store).Order());
    }

    [Fact]
    public void A_workspace_scoped_added_record_whose_work_does_not_read_narrows_both_and_leaves_build_and_the_machine_intake()
    {
        var catalog = Catalog(new StoredRole("designer", "set", """{"id":"designer","scope":{"kind":"workspace","id":"w1"}}""", 1));
        var store = Store(("build", [new ActorChoice("codex", "gpt")]));

        var older = OlderBuild(store, catalog);

        Assert.False(StaffingProjection.Starts("intake", older));
        Assert.False(StaffingProjection.Starts("ask-daoris", older));
        Assert.Equal([new ActorChoice("codex", "gpt")], older.Actors["build"]);
        Assert.Equal("claude", older.Adapter);
        Assert.Equal([new ActorEffective("codex", null, null, "before")], catalog.Order(ParticipantWork.Intake, Machine, store));
        Assert.Throws<DriverException>(() => catalog.Order(ParticipantWork.Intake, InWorkspace, store));
    }

    // ——— Through the staffing's write and check, on a home.

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private string Db => DesktopStore.LocationIn(_home);

    private RoleStaffing Begin()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, """{ "adapter": "claude", "intakeAdapter": "codex", "helperAdapter": "claude" }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        return staffing;
    }

    private DriverConfig OnFile => DriverConfig.Load(File_);

    private void Narrow()
    {
        RoleCatalog.Write(Db, RoleChange.Set(Added("w-intake", ParticipantWork.Intake)), () => StoreSettled.Yes);
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("role_staffing", write => write.Execute(
            "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ('w-intake', 'set', '[]', NULL, NULL, '2026-10-30T00:00:00.0000000+00:00')"));
    }

    [Fact]
    public void A_staffing_write_projects_the_narrowing_into_the_file_and_keeps_the_added_role_s_row_and_the_store_s_own_keys()
    {
        var staffing = Begin();
        Narrow();

        staffing.Set("chat", [new ActorChoice("claude")]);

        Assert.Empty(OnFile.Actors["intake"]);
        Assert.Null(OnFile.IntakeAdapter);
        Assert.Equal("claude", OnFile.HelperAdapter);
        var read = staffing.Read()!;
        Assert.Equal("codex", read.IntakeAdapter);
        Assert.Empty(read.Actors["w-intake"]);
        Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
    }

    [Fact]
    public void A_record_that_stops_reading_outside_a_write_moves_the_projection_which_the_next_check_writes_and_back()
    {
        var staffing = Begin();
        Narrow();

        var narrowed = staffing.Check(StaffingCheckAt.Look);

        Assert.Equal(StaffingChecks.Written, narrowed.How);
        Assert.Empty(OnFile.Actors["intake"]);
        Assert.Null(OnFile.IntakeAdapter);
        Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Empty(staffing.Found());

        RoleCatalog.Write(Db, RoleChange.Default("w-intake"), () => StoreSettled.Yes);

        Assert.Equal(StaffingChecks.Written, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Equal("codex", OnFile.IntakeAdapter);
        Assert.False(OnFile.Actors.ContainsKey("intake"));
        Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Empty(staffing.Found());
    }

    [Fact]
    public void An_older_build_turning_Intake_back_on_in_a_narrowed_file_is_found_and_the_file_narrowed_again()
    {
        var staffing = Begin();
        Narrow();
        staffing.Check(StaffingCheckAt.Look);
        File.WriteAllText(File_, """{ "adapter": "claude", "intakeAdapter": "codex", "helperAdapter": "claude" }""");

        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Start).How);

        Assert.Single(staffing.Found());
        Assert.Null(OnFile.IntakeAdapter);
        Assert.Empty(OnFile.Actors["intake"]);
    }
}
