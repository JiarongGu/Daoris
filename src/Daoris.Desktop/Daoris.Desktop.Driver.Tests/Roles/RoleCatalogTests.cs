using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The catalogue of working roles read beside, the seed per id, each id read as
/// absent, set, removed or unreadable, and a role found by work and place. Nothing reads the catalogue: no start, screen or verb changes.
/// </summary>
public sealed class RoleCatalogTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-catalog", Guid.NewGuid().ToString("N")[..8]);

    public RoleCatalogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly RolePlace Machine = new();
    private static readonly RolePlace InWorkspace = new(Workspace: "w1");

    internal static RoleRecord Working(string id, string work, RoleScope? scope = null) =>
        new(id, AuthoredText.Of(id), RoleRecord.Always, [work], scope ?? RoleScope.Everywhere);

    internal static StoredRole Stored(RoleRecord role, long revision = 1) => new(role.Id, "set", role.Json(), revision);

    private static DriverConfig Staffed(string actor, params ActorChoice[] choices) =>
        ActorChoices.With(StaffedRoles.Config("claude"), actor, choices);

    [Fact]
    public void The_catalogue_s_work_vocabulary_is_the_participant_s_work_codes_but_the_supervisor_s()
    {
        Assert.Equal(ParticipantWork.Codes.Where(work => work != ParticipantWork.Supervise), RoleWork.Codes);
    }

    [Fact]
    public void The_seed_is_the_ten_working_roles_on_the_machine_one_per_work_design_taking_its_own()
    {
        var catalog = RoleCatalog.Of([]);

        Assert.Equal([.. ActorChoices.Ids, AppointedRoles.Supervisor, AppointedRoles.Steward], catalog.Slots.Select(slot => slot.Id));
        Assert.All(catalog.Slots, slot => Assert.Equal((RoleState.Seed, 0L), (slot.State, slot.Revision)));
        Assert.All(RoleCatalog.Seed, role => Assert.Equal(RoleScope.Everywhere, role.Scope));
        Assert.Equal(ActorChoices.Ids, RoleCatalog.Seed.Where(role => role.Filled == RoleRecord.Always).Select(role => role.Id));
        Assert.Equal(
            [("build", "build"), ("design", "design"), ("exact", "exact"), ("chat", "chat"), ("ask-daoris", "ask-daoris"), ("intake", "intake"), ("help", "helper"), ("partner", "partner"), ("review", "opinion"), ("brainstorm", "brainstorm")],
            RoleWork.Codes.Select(work => (work, catalog.RoleFor(work, Machine).Role!.Id)));
        Assert.Equal(["design"], RoleCatalog.Seed.Where(role => role.Takes.Contains(ParticipantWork.Design)).Select(role => role.Id));
    }

    [Fact]
    public void The_seed_is_the_shipped_data_and_its_working_ids_are_the_actors()
    {
        Assert.Equal([.. ActorChoices.Ids, AppointedRoles.Supervisor, AppointedRoles.Steward], RoleCatalog.Seed.Select(role => role.Id));
        Assert.Equal(
            [("build", "Build", "开发"), ("design", "Design", "设计"), ("exact", "Exact build", "精确开发"), ("intake", "Intake", "受理"), ("opinion", "Second opinion", "第二意见"), ("helper", "Helper", "助手"),
                ("partner", "Partner", "搭档"), ("brainstorm", "Brainstorm", "头脑风暴"), ("ask-daoris", "Ask Daoris", "问道衍"), ("chat", "Chat", "聊天"),
                ("supervisor", "Supervisor", "主管"), ("steward", "Steward", "管家")],
            RoleCatalog.Seed.Select(role => (role.Id, role.Name.In("en"), role.Name.In("zh"))));
        Assert.All(RoleCatalog.Seed.Where(role => role.Hold is null), role => Assert.Equal(ActorWords.Name(role.Id), role.Name.In("en")));
    }

    [Fact]
    public void Restoring_every_default_returns_the_seeded_roles_and_keeps_the_added_ones()
    {
        var db = DesktopStore.LocationIn(_home);
        var added = Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"));
        var changed = RoleCatalog.Seed[0] with { Name = AuthoredText.Of("Maker") };
        RoleCatalog.WriteAll(db, [RoleChange.Set(added), RoleChange.Set(changed), RoleChange.Remove("intake")], () => StoreSettled.Yes);
        var before = RoleCatalog.Read(db);
        Assert.Equal([RoleState.Set, RoleState.Removed], before.Slots.Where(slot => slot.Id is "build" or "intake").Select(slot => slot.State));

        RoleCatalog.Write(db, RoleChange.DefaultAll(), () => StoreSettled.Yes);

        var after = RoleCatalog.Read(db);
        Assert.Equal([.. ActorChoices.Ids, "supervisor", "steward", "designer"], after.Slots.Select(slot => slot.Id));
        Assert.All(after.Slots.Where(slot => slot.Id != "designer"), slot => Assert.Equal(RoleState.Seed, slot.State));
        Assert.Equal((RoleState.Set, added.Json()), (after.Slots[^1].State, after.Slots[^1].Role!.Json()));
        Assert.Null(after.Judge(RoleChange.DefaultAll()));
    }

    [Fact]
    public void The_seed_reproduces_today_s_every_start_case()
    {
        var catalog = RoleCatalog.Of([]);
        DriverConfig[] configs =
        [
            DriverConfig.Empty with { Actors = StaffedRoles.On("claude", "codex", "claude") },
            StaffedRoles.Config("claude"),
            Staffed("build", new ActorChoice("codex", "gpt"), new ActorChoice("claude")),
            Staffed("intake"),
            Staffed("brainstorm", new ActorChoice("claude"), new ActorChoice("codex")),
        ];

        foreach (var config in configs)
        {
            foreach (var work in RoleWork.Codes)
            {
                var role = StartPlan.RoleOf(work);
                Assert.Equal(ActorChoices.Effective(role, config), catalog.Order(work, Machine, config));
                Assert.Equal(ActorChoices.Effective(role, config, fence: ["codex"]), catalog.Order(work, Machine, config, fence: ["codex"]));
                Assert.Equal(ActorChoices.Effective(role, config, pick: "x"), catalog.Order(work, Machine, config, pick: "x"));
            }
        }

        var unreadable = DriverConfig.Empty with { ActorErrors = new Dictionary<string, string> { ["chat"] = "is not a list of choices" } };
        var held = Assert.Throws<ActorUnreadableException>(() => catalog.Order(ParticipantWork.Chat, Machine, unreadable));
        Assert.Equal("chat", held.Actor);
    }

    [Fact]
    public void An_id_reads_absent_set_removed_or_unreadable_and_each_keeps_the_others_apart()
    {
        var edited = Working("build", ParticipantWork.Build, new RoleScope(RoleScope.Workspace, "w1")) with { Name = AuthoredText.Of("Builder") };
        var catalog = RoleCatalog.Of(
        [
            Stored(edited, 3),
            new StoredRole("intake", "removed", "{}", 2),
            new StoredRole("chat", "set", "{\"id\":\"chat\"", 4),
        ]);

        var slots = catalog.Slots.ToDictionary(slot => slot.Id);
        Assert.Equal((RoleState.Set, 3L, "Builder"), (slots["build"].State, slots["build"].Revision, slots["build"].Role!.Name.AsWritten));
        Assert.Equal((RoleState.Removed, 2L, null), (slots["intake"].State, slots["intake"].Revision, slots["intake"].Role?.Id));
        Assert.Equal(RoleState.Unreadable, slots["chat"].State);
        Assert.StartsWith("does not read as JSON", slots["chat"].Error);
        Assert.Equal(
            [RoleState.Seed],
            slots.Where(slot => slot.Key is not ("build" or "intake" or "chat")).Select(slot => slot.Value.State).Distinct());
    }

    [Fact]
    public void A_mixed_record_an_appointed_one_and_an_unknown_work_read_as_unreadable_never_as_either_shape()
    {
        var mixed = """{"id":"build","name":{"en":"B","zh":"B"},"filled":"always","takes":["build"],"authority":["answer"],"scope":{"kind":"machine"}}""";
        var appointed = """{"id":"chat","name":{"en":"B","zh":"B"},"filled":"appointed","authority":["answer"],"scope":{"kind":"machine"}}""";
        var unknown = """{"id":"intake","name":{"en":"B","zh":"B"},"filled":"always","takes":["gardening"],"scope":{"kind":"machine"}}""";
        var catalog = RoleCatalog.Of([new("build", "set", mixed, 1), new("chat", "set", appointed, 1), new("intake", "set", unknown, 1)]);

        var slots = catalog.Slots.ToDictionary(slot => slot.Id);
        Assert.All(["build", "chat", "intake"], id => Assert.Equal((RoleState.Unreadable, null), (slots[id].State, slots[id].Role)));
        Assert.Equal("mixes the working and the appointed shape", slots["build"].Error);
        Assert.Equal("names no `kind` of `supervisor` or `steward`", slots["chat"].Error);
        Assert.Equal("takes `gardening`, a work this build does not know", slots["intake"].Error);
    }

    [Fact]
    public void An_unreadable_seeded_id_holds_the_starts_of_its_work_and_says_why()
    {
        var catalog = RoleCatalog.Of([new("build", "set", "{", 1)]);

        var held = catalog.RoleFor(ParticipantWork.Build, Machine);
        Assert.Null(held.Role);
        Assert.Contains("`build` does not read as JSON", held.Held);
        // Design takes its own seeded role, so an unreadable build holds nothing of it.
        Assert.Null(catalog.RoleFor(ParticipantWork.Design, Machine).Held);
        Assert.Null(catalog.RoleFor(ParticipantWork.Chat, Machine).Held);
    }

    [Fact]
    public void An_unreadable_added_record_holds_every_start_in_its_scope_until_it_reads_or_is_deleted_each_said()
    {
        var inWorkspace = new StoredRole("designer", "set", """{"id":"designer","scope":{"kind":"workspace","id":"w1"}}""", 1);
        var catalog = RoleCatalog.Of([inWorkspace]);

        foreach (var work in RoleWork.Codes)
        {
            var held = catalog.RoleFor(work, InWorkspace);
            Assert.Null(held.Role);
            Assert.Contains("`designer` has no name", held.Held);
            Assert.Null(catalog.RoleFor(work, Machine).Held);
            Assert.Null(catalog.RoleFor(work, new RolePlace(Workspace: "w2")).Held);
        }

        var everywhere = RoleCatalog.Of([new("designer", "set", "{", 1)]);
        Assert.All(RoleWork.Codes, work => Assert.NotNull(everywhere.RoleFor(work, Machine).Held));

        var read = RoleCatalog.Of([Stored(Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1")))]);
        Assert.Equal("designer", read.RoleFor(ParticipantWork.Design, InWorkspace).Role!.Id);
        Assert.Null(RoleCatalog.Of([]).RoleFor(ParticipantWork.Design, InWorkspace).Held);
    }

    [Fact]
    public void The_most_specific_scope_takes_a_work_and_two_roles_in_one_scope_hold_it()
    {
        var workspace = Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"));
        var repository = Working("drafter", ParticipantWork.Design, new RoleScope(RoleScope.Repository, "r1"));
        var catalog = RoleCatalog.Of([Stored(workspace), Stored(repository)]);

        Assert.Equal("drafter", catalog.RoleFor(ParticipantWork.Design, new RolePlace("w1", "r1")).Role!.Id);
        Assert.Equal("designer", catalog.RoleFor(ParticipantWork.Design, new RolePlace("w1", "r2")).Role!.Id);
        Assert.Equal("design", catalog.RoleFor(ParticipantWork.Design, Machine).Role!.Id);

        var twice = RoleCatalog.Of([Stored(workspace), Stored(workspace with { Id = "second" })]);
        var held = twice.RoleFor(ParticipantWork.Design, InWorkspace);
        Assert.Null(held.Role);
        Assert.Contains("Two roles take `design`", held.Held);
    }

    [Fact]
    public void A_removed_seeded_role_leaves_its_work_with_no_role_whose_starts_hold_said_and_an_off_role_reads_off()
    {
        var catalog = RoleCatalog.Of([new("intake", "removed", "{}", 2)]);

        // No role is not off; the work's starts hold until the person gives it one.
        var found = catalog.RoleFor(ParticipantWork.Intake, Machine);
        Assert.Equal((null, RoleCatalog.NoRole(ParticipantWork.Intake)), (found.Role, found.Held));
        var held = Assert.Throws<DriverException>(() => catalog.Order(ParticipantWork.Intake, Machine, DriverConfig.Empty with { Actors = StaffedRoles.On("claude", "codex") }));
        Assert.Equal(RoleCatalog.NoRole(ParticipantWork.Intake), held.Message);
        Assert.Empty(RoleCatalog.Of([]).Order(ParticipantWork.Intake, Machine, ActorChoices.With(DriverConfig.Empty, "intake", [])));
    }

    [Fact]
    public void The_catalogue_reads_the_store_s_roles_and_the_seed_where_there_is_no_store_or_an_older_one()
    {
        var db = DesktopStore.LocationIn(_home);
        Assert.All(RoleCatalog.Read(db).Slots, slot => Assert.Equal(RoleState.Seed, slot.State));

        using (var older = DesktopStore.Open(db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(9)], Settled = () => StoreSettled.Yes }))
        {
            older.Write("probe", _ => { });
        }

        Assert.All(RoleCatalog.Read(db).Slots, slot => Assert.Equal(RoleState.Seed, slot.State));

        var added = Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"));
        using (var store = DesktopStore.Open(db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("roles", write => write.Execute(
                "INSERT INTO roles (id, state, record, revision, at) VALUES ($id, 'set', $record, 7, '2026-10-30T00:00:00.0000000+00:00')",
                ("$id", added.Id), ("$record", added.Json())));
        }

        var read = RoleCatalog.Read(db);
        Assert.Equal(["build", "design", "exact", "intake", "opinion", "helper", "partner", "brainstorm", "ask-daoris", "chat", "supervisor", "steward", "designer"], read.Slots.Select(slot => slot.Id));
        Assert.Equal((RoleState.Set, 7L, added.Json()), (read.Slots[^1].State, read.Slots[^1].Revision, read.Slots[^1].Role!.Json()));
    }

    /// <summary>A step's named role is found by its id at its scope, never by the work, and walks its own order.</summary>
    [Fact]
    public void A_named_role_is_found_at_its_scope_and_a_design_step_takes_a_role_that_takes_build()
    {
        var checker = Working("checker", ParticipantWork.Review, new RoleScope(RoleScope.Workspace, "w1"));
        var maker = Working("maker", ParticipantWork.Build, new RoleScope(RoleScope.Workspace, "w1"));
        var catalog = RoleCatalog.Of([Stored(checker, 2), Stored(maker)]);

        Assert.Equal(("checker", null), (catalog.Named("checker", ParticipantWork.Review, InWorkspace).Role?.Id, catalog.Named("checker", ParticipantWork.Review, InWorkspace).Held));
        Assert.Equal("opinion", catalog.Named("opinion", ParticipantWork.Review, InWorkspace).Role?.Id);
        Assert.Equal("maker", catalog.Named("maker", ParticipantWork.Design, InWorkspace).Role?.Id);
        Assert.Equal("build", catalog.Named("build", ParticipantWork.Design, Machine).Role?.Id);

        var config = DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>> { ["checker"] = [new ActorChoice("codex", "gpt"), new ActorChoice("claude")] },
        };
        Assert.Equal(ActorChoices.Effective("checker", config), catalog.Order("checker", ParticipantWork.Review, InWorkspace, config));
        Assert.Equal(ActorChoices.Effective("checker", config, fence: ["claude"]), catalog.Order("checker", ParticipantWork.Review, InWorkspace, config, fence: ["claude"]));
    }

    [Fact]
    public void A_named_role_that_cannot_take_the_step_holds_it_said_and_never_falls_to_another_role()
    {
        const string Here = "Agents → Roles, or `daoris role`";
        var appointed = """{"id":"lead","name":{"en":"Lead","zh":"Lead"},"kind":"supervisor","filled":"appointed","authority":["answer"],"scope":{"kind":"machine"}}""";
        var catalog = RoleCatalog.Of(
        [
            Stored(Working("checker", ParticipantWork.Review, new RoleScope(RoleScope.Workspace, "w1"))),
            new StoredRole("broken", "set", "{", 1),
            new StoredRole("lead", "set", appointed, 1),
            new StoredRole("intake", "removed", "{}", 2),
        ]);

        (string Role, string Work, RolePlace Place, string Said)[] held =
        [
            ("nobody", ParticipantWork.Build, Machine, $"No role `nobody` is in the catalogue, so the starts naming it hold until it is added: {Here}."),
            ("intake", ParticipantWork.Intake, Machine, $"No role `intake` is in the catalogue, so the starts naming it hold until it is added: {Here}."),
            ("broken", ParticipantWork.Build, Machine, $"The role `broken` {catalog.Slots.Single(slot => slot.Id == "broken").Error}, so the starts naming it hold until it reads or is deleted."),
            ("supervisor", ParticipantWork.Build, Machine, "`supervisor` is an appointed role, which holds acts and takes no work, so the starts naming it hold until they name a working role."),
            ("lead", ParticipantWork.Build, Machine, "`lead` is an appointed role, which holds acts and takes no work, so the starts naming it hold until they name a working role."),
            ("checker", ParticipantWork.Review, new RolePlace(Workspace: "w2"), $"The role `checker` does not work here: its scope is the workspace `w1`, so the starts naming it hold until it is scoped here: {Here}."),
            ("checker", ParticipantWork.Build, InWorkspace, $"The role `checker` does not take `build`, so the starts naming it hold until it does: {Here}."),
            ("opinion", ParticipantWork.Design, Machine, $"The role `opinion` does not take `design`, so the starts naming it hold until it does: {Here}."),
        ];

        foreach (var (role, work, place, said) in held)
        {
            Assert.Equal((null, said), (catalog.Named(role, work, place).Role, catalog.Named(role, work, place).Held));
            Assert.Equal(said, Assert.Throws<DriverException>(() => catalog.Order(role, work, place, StaffedRoles.Config("claude"))).Message);
        }

        // Another role's record that does not read holds the work's own search, never a step naming a role that reads.
        Assert.NotNull(catalog.RoleFor(ParticipantWork.Review, Machine).Held);
        Assert.Equal("opinion", catalog.Named("opinion", ParticipantWork.Review, Machine).Role?.Id);
    }
}
