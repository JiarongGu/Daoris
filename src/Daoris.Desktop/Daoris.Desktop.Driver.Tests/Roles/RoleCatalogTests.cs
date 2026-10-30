using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w (the foundation design §2.7, Part B's row w): the catalogue of working roles read beside, the seed per id, each id read as
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
        new(id, id, id, RoleRecord.Always, [work], scope ?? RoleScope.Everywhere);

    internal static StoredRole Stored(RoleRecord role, long revision = 1) => new(role.Id, "set", role.Json(), revision);

    private static DriverConfig Staffed(string actor, params ActorChoice[] choices) =>
        ActorChoices.With(DriverConfig.Empty with { Adapter = "claude" }, actor, choices);

    [Fact]
    public void The_seed_is_the_eight_working_roles_on_the_machine_one_per_work_and_no_role_takes_design()
    {
        var catalog = RoleCatalog.Of([]);

        Assert.Equal(ActorChoices.Ids, catalog.Slots.Select(slot => slot.Id));
        Assert.All(catalog.Slots, slot => Assert.Equal((RoleState.Seed, 0L), (slot.State, slot.Revision)));
        Assert.All(RoleCatalog.Seed, role => Assert.Equal((RoleRecord.Always, RoleScope.Everywhere), (role.Filled, role.Scope)));
        Assert.Equal(
            [("build", "build"), ("chat", "chat"), ("ask-daoris", "ask-daoris"), ("intake", "intake"), ("help", "helper"), ("partner", "partner"), ("review", "opinion"), ("brainstorm", "brainstorm")],
            ParticipantWork.Codes.Where(work => work != ParticipantWork.Design).Select(work => (work, catalog.RoleFor(work, Machine).Role!.Id)));
        Assert.DoesNotContain(RoleCatalog.Seed, role => role.Takes.Contains(ParticipantWork.Design));
        Assert.Equal("build", catalog.RoleFor(ParticipantWork.Design, Machine).Role!.Id);
    }

    [Fact]
    public void The_seed_reproduces_today_s_every_start_case()
    {
        var catalog = RoleCatalog.Of([]);
        DriverConfig[] configs =
        [
            DriverConfig.Empty with { Adapter = "claude", IntakeAdapter = "codex", HelperAdapter = "claude" },
            DriverConfig.Empty with { Adapter = "claude" },
            Staffed("build", new ActorChoice("codex", "gpt"), new ActorChoice("claude")),
            Staffed("intake") with { IntakeAdapter = null },
            Staffed("brainstorm", new ActorChoice("claude"), new ActorChoice("codex")),
        ];

        foreach (var config in configs)
        {
            foreach (var work in ParticipantWork.Codes)
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
        var edited = Working("build", ParticipantWork.Build, new RoleScope(RoleScope.Workspace, "w1")) with { NameEn = "Builder" };
        var catalog = RoleCatalog.Of(
        [
            Stored(edited, 3),
            new StoredRole("intake", "removed", "{}", 2),
            new StoredRole("chat", "set", "{\"id\":\"chat\"", 4),
        ]);

        var slots = catalog.Slots.ToDictionary(slot => slot.Id);
        Assert.Equal((RoleState.Set, 3L, "Builder"), (slots["build"].State, slots["build"].Revision, slots["build"].Role!.NameEn));
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
        Assert.Equal("is an appointed role, which this build does not configure", slots["chat"].Error);
        Assert.Equal("takes `gardening`, a work this build does not know", slots["intake"].Error);
    }

    [Fact]
    public void An_unreadable_seeded_id_holds_the_starts_of_its_work_and_says_why()
    {
        var catalog = RoleCatalog.Of([new("build", "set", "{", 1)]);

        var held = catalog.RoleFor(ParticipantWork.Build, Machine);
        Assert.Null(held.Role);
        Assert.Contains("`build` does not read as JSON", held.Held);
        Assert.NotNull(catalog.RoleFor(ParticipantWork.Design, Machine).Held);
        Assert.Null(catalog.RoleFor(ParticipantWork.Chat, Machine).Held);
    }

    [Fact]
    public void An_unreadable_added_record_holds_every_start_in_its_scope_until_it_reads_or_is_deleted_each_said()
    {
        var inWorkspace = new StoredRole("designer", "set", """{"id":"designer","scope":{"kind":"workspace","id":"w1"}}""", 1);
        var catalog = RoleCatalog.Of([inWorkspace]);

        foreach (var work in ParticipantWork.Codes)
        {
            var held = catalog.RoleFor(work, InWorkspace);
            Assert.Null(held.Role);
            Assert.Contains("`designer` has no name in English and in Chinese", held.Held);
            Assert.Null(catalog.RoleFor(work, Machine).Held);
            Assert.Null(catalog.RoleFor(work, new RolePlace(Workspace: "w2")).Held);
        }

        var everywhere = RoleCatalog.Of([new("designer", "set", "{", 1)]);
        Assert.All(ParticipantWork.Codes, work => Assert.NotNull(everywhere.RoleFor(work, Machine).Held));

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
        Assert.Equal("build", catalog.RoleFor(ParticipantWork.Design, Machine).Role!.Id);

        var twice = RoleCatalog.Of([Stored(workspace), Stored(workspace with { Id = "second" })]);
        var held = twice.RoleFor(ParticipantWork.Design, InWorkspace);
        Assert.Null(held.Role);
        Assert.Contains("Two roles take `design`", held.Held);
    }

    [Fact]
    public void A_removed_seeded_role_leaves_its_work_with_no_role_and_an_off_role_reads_off()
    {
        var catalog = RoleCatalog.Of([new("intake", "removed", "{}", 2)]);

        var found = catalog.RoleFor(ParticipantWork.Intake, Machine);
        Assert.Equal((null, null), (found.Role, found.Held));
        Assert.Empty(catalog.Order(ParticipantWork.Intake, Machine, DriverConfig.Empty with { IntakeAdapter = "codex" }));
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
        Assert.Equal(["build", "intake", "opinion", "helper", "partner", "brainstorm", "ask-daoris", "chat", "designer"], read.Slots.Select(slot => slot.Id));
        Assert.Equal((RoleState.Set, 7L, added.Json()), (read.Slots[^1].State, read.Slots[^1].Revision, read.Slots[^1].Role!.Json()));
    }
}
