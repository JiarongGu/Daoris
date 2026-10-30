using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w (the foundation design §2.7, the judge's census): <c>RoleCatalog.Judge</c> refuses alike for every door with one closed set
/// of codes, an appointed record before anything else, and the catalogue's one write keeps absent, set, removed and unreadable apart.
/// </summary>
public sealed class RoleCatalogJudgeTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-catalog-judge", Guid.NewGuid().ToString("N")[..8]);

    public RoleCatalogJudgeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Db => DesktopStore.LocationIn(_home);

    private static readonly RoleCatalog Seeded = RoleCatalog.Of([]);

    private static RoleRecord Working(string id, string work, RoleScope? scope = null) => RoleCatalogTests.Working(id, work, scope);

    private static RoleScope Workspace(string id = "w1") => new(RoleScope.Workspace, id);

    private static string? Code(RoleRefusal? refusal) => refusal?.Code;

    [Fact]
    public void An_appointed_record_is_refused_before_anything_else_of_it_is_judged()
    {
        var appointed = new RoleRecord("supervisor!", "", "", RoleRecord.Appointed, ["gardening"], new RoleScope("planet", "x"), ["answer"]);

        Assert.Equal("appointed-not-configurable", Code(Seeded.Judge(RoleChange.Set(appointed))));
        Assert.Equal("appointed-not-configurable", Code(Seeded.Judge(RoleChange.Set(appointed with { Id = "steward" }))));
        Assert.Equal("appointed-not-configurable", Code(Seeded.Judge(RoleChange.Set(Working("a", "build") with { Filled = RoleRecord.Appointed }))));
    }

    [Fact]
    public void A_mixed_shape_an_unknown_work_and_a_reserved_id_are_refused_each_by_its_code()
    {
        Assert.Equal("mixed-shape", Code(Seeded.Judge(RoleChange.Set(Working("a", "design") with { Authority = ["answer"] }))));
        Assert.Equal("unknown-work", Code(Seeded.Judge(RoleChange.Set(Working("a", "gardening")))));
        Assert.Equal("reserved-id", Code(Seeded.Judge(RoleChange.Set(Working("steward", "design")))));
        Assert.Equal("reserved-id", Code(Seeded.Judge(RoleChange.Remove("supervisor"))));
        Assert.Equal("bad-record", Code(Seeded.Judge(RoleChange.Set(Working("Bad Id", "design")))));
        Assert.Equal("bad-record", Code(Seeded.Judge(RoleChange.Set(Working("a", "design") with { Takes = [] }))));
        Assert.Equal("bad-record", Code(Seeded.Judge(RoleChange.Set(Working("a", "design") with { NameZh = " " }))));
        Assert.Equal("bad-record", Code(Seeded.Judge(RoleChange.Set(Working("a", "design", new RoleScope(RoleScope.Workspace))))));
        Assert.Equal("bad-record", Code(Seeded.Judge(RoleChange.Default("nobody"))));
        Assert.Null(Seeded.Judge(RoleChange.Set(Working("designer", "design", Workspace()))));
    }

    [Fact]
    public void Removing_or_scoping_away_the_role_of_a_work_that_may_not_be_off_is_refused()
    {
        foreach (var id in ActorChoices.Ids.Where(id => !ActorChoices.MayBeOff(id)))
        {
            var seed = RoleCatalog.Seed.Single(role => role.Id == id);
            Assert.Equal("may-not-be-off", Code(Seeded.Judge(RoleChange.Remove(id))));
            Assert.Equal("may-not-be-off", Code(Seeded.Judge(RoleChange.Set(seed with { Scope = Workspace() }))));
            Assert.Null(Seeded.Judge(RoleChange.Set(seed with { NameEn = "Renamed" })));
        }

        foreach (var id in ActorChoices.Ids.Where(ActorChoices.MayBeOff))
        {
            var seed = RoleCatalog.Seed.Single(role => role.Id == id);
            Assert.Null(Seeded.Judge(RoleChange.Remove(id)));
            Assert.Null(Seeded.Judge(RoleChange.Set(seed with { Scope = Workspace() })));
        }

        var moved = Working("builder", ParticipantWork.Build);
        var catalog = RoleCatalog.Of([RoleCatalogTests.Stored(moved), RoleCatalogTests.Stored(RoleCatalog.Seed[0] with { Scope = Workspace() })]);
        Assert.Equal("may-not-be-off", Code(catalog.Judge(RoleChange.Remove("builder"))));
    }

    [Fact]
    public void Two_roles_taking_one_work_in_one_scope_are_refused_and_in_two_scopes_are_not()
    {
        var designer = Working("designer", ParticipantWork.Design, Workspace());
        var catalog = RoleCatalog.Of([RoleCatalogTests.Stored(designer)]);

        Assert.Equal("two-roles-one-work", Code(catalog.Judge(RoleChange.Set(Working("drafter", ParticipantWork.Design, Workspace())))));
        Assert.Equal("two-roles-one-work", Code(Seeded.Judge(RoleChange.Set(Working("second", ParticipantWork.Chat)))));
        Assert.Null(catalog.Judge(RoleChange.Set(Working("drafter", ParticipantWork.Design, Workspace("w2")))));
        Assert.Null(catalog.Judge(RoleChange.Set(designer with { NameEn = "Designer" })));
        Assert.Null(catalog.Judge(RoleChange.Set(Working("elsewhere", ParticipantWork.Chat, Workspace()))));
    }

    [Fact]
    public void A_staffing_is_judged_by_the_work_the_way_the_actors_are()
    {
        foreach (var id in ActorChoices.Ids)
        {
            var work = RoleCatalog.Seed.Single(role => role.Id == id).Takes[0];
            IReadOnlyList<ActorChoice>?[] orders =
            [
                null, [], [new("a")], [new("a"), new("b")], [new("a"), new("a", "m")], [new("a"), new("a")], [new(" a")],
                [new("a"), new("b"), new("c"), new("d"), new("e")],
            ];
            foreach (var order in orders)
            {
                Assert.Equal(ActorChoices.Judge(id, order), RoleCatalog.Judge(work, order)?.Said);
            }
        }

        Assert.Equal("may-not-be-off", Code(RoleCatalog.Judge(ParticipantWork.Build, [])));
        Assert.Null(RoleCatalog.Judge(ParticipantWork.Intake, []));
        Assert.Equal("choices", Code(RoleCatalog.Judge(ParticipantWork.Brainstorm, [new("a"), new("a", "m")])));
        Assert.Equal("unknown-work", Code(RoleCatalog.Judge("gardening", [new("a")])));
        Assert.Equal("may-not-be-off", Code(RoleCatalog.Judge(ParticipantWork.Design, [])));
        Assert.Null(RoleCatalog.Judge(ParticipantWork.Design, [new("a")]));

        var both = Working("both", ParticipantWork.Design) with { Takes = [ParticipantWork.Design, ParticipantWork.Brainstorm] };
        Assert.Equal("choices", Code(RoleCatalog.Judge(both, [new("a"), new("a", "m")])));
        Assert.Null(RoleCatalog.Judge(both, [new("a"), new("b")]));
    }

    [Fact]
    public void Every_refusal_a_door_can_get_is_in_the_closed_set()
    {
        Assert.Equal(
            [
                "appointed-not-configurable", "mixed-shape", "unknown-work", "reserved-id", "bad-record", "may-not-be-off", "two-roles-one-work", "choices",
            ],
            RoleCatalog.Codes);
    }

    private IReadOnlyList<RoleSlot> Slots() => RoleCatalog.Read(Db).Slots;

    private IReadOnlyList<(string Id, long Revision, string State)> Revisions()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null
            ? []
            : reader.Query("SELECT id, revision, state FROM role_revisions ORDER BY id, revision", row => (row.GetString(0), row.GetInt64(1), row.GetString(2)));
    }

    [Fact]
    public void A_first_edit_a_partial_edit_and_the_last_deletion_keep_absent_set_removed_and_unreadable_apart()
    {
        var settled = () => StoreSettled.Yes;

        RoleCatalog.Write(Db, RoleChange.Set(RoleCatalog.Seed[0] with { NameEn = "Builder" }), settled);
        var first = Slots().ToDictionary(slot => slot.Id);
        Assert.Equal((RoleState.Set, 1L, "Builder"), (first["build"].State, first["build"].Revision, first["build"].Role!.NameEn));
        Assert.All(first.Values.Where(slot => slot.Id != "build"), slot => Assert.Equal(RoleState.Seed, slot.State));

        RoleCatalog.Write(Db, RoleChange.Remove("intake"), settled);
        var partial = Slots().ToDictionary(slot => slot.Id);
        Assert.Equal(RoleState.Set, partial["build"].State);
        Assert.Equal((RoleState.Removed, 1L), (partial["intake"].State, partial["intake"].Revision));
        Assert.Equal(RoleState.Seed, partial["chat"].State);

        using (var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = settled }))
        {
            store.Write("roles", write => write.Execute("UPDATE roles SET record = '{' WHERE id = 'build'"));
        }

        Assert.Equal(RoleState.Unreadable, Slots().Single(slot => slot.Id == "build").State);
        Assert.Equal(RoleState.Removed, Slots().Single(slot => slot.Id == "intake").State);

        RoleCatalog.Write(Db, RoleChange.Default("build"), settled);
        RoleCatalog.Write(Db, RoleChange.Default("intake"), settled);

        Assert.All(Slots(), slot => Assert.Equal((RoleState.Seed, 0L), (slot.State, slot.Revision)));
        Assert.Equal([("build", 1L, "set"), ("build", 2L, "default"), ("intake", 1L, "removed"), ("intake", 2L, "default")], Revisions());
    }

    [Fact]
    public void A_refused_change_writes_nothing_and_a_role_made_again_keeps_counting_its_revisions()
    {
        var settled = () => StoreSettled.Yes;
        var designer = Working("designer", ParticipantWork.Design, Workspace());

        var refused = Assert.Throws<DriverException>(() => RoleCatalog.Write(Db, RoleChange.Set(designer with { Filled = RoleRecord.Appointed }), settled));
        Assert.Contains("appointed", refused.Message);
        Assert.Empty(Revisions());

        RoleCatalog.Write(Db, RoleChange.Set(designer), settled);
        RoleCatalog.Write(Db, RoleChange.Remove("designer"), settled);
        RoleCatalog.Write(Db, RoleChange.Set(designer), settled);

        Assert.Equal(3L, Slots().Single(slot => slot.Id == "designer").Revision);
        Assert.Equal([("designer", 1L, "set"), ("designer", 2L, "deleted"), ("designer", 3L, "set")], Revisions());
    }
}
