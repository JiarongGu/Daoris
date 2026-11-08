using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every record carrying an <c>actor</c> is held to a role taking its
/// work, by the revision of that role the start kept on its binding; a role taking <c>design</c> is admitted on a record whose marks read
/// <c>build</c>. Where the marks and the role would differ the marks decide the work.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RoleCensusTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-census", Guid.NewGuid().ToString("N")[..8]);

    public RoleCensusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static RoleRecord? Seeded(string id, long revision) => revision == 0 ? RoleCatalog.Seed.FirstOrDefault(role => role.Id == id) : null;

    [Fact]
    public void Every_seeded_work_s_record_is_held_to_its_own_seeded_role()
    {
        foreach (var work in ParticipantWork.Codes.Where(work => work is not (ParticipantWork.Design or ParticipantWork.Supervise)))
        {
            Assert.Null(RoleCensus.Check(work, StartPlan.RoleOf(work), null, Seeded));
        }

        var gatekeeper = new RoleRecord("gatekeeper", AuthoredText.Of("Gatekeeper"), RoleRecord.Appointed, [], RoleScope.Everywhere,
            new RoleHold(AppointedRoles.Supervisor, [], RoleHolders.Agent) { Decides = [SupervisorKinds.Question] });
        RoleRecord? Held(string id, long revision) => id == "gatekeeper" && revision == 4 ? gatekeeper : revision == 5 ? gatekeeper with { Hold = gatekeeper.Hold! with { Holder = RoleHolders.Terminal } } : null;
        Assert.Null(RoleCensus.Check(ParticipantWork.Supervise, "gatekeeper", ("gatekeeper", 4), Held));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Supervise, "gatekeeper", ("gatekeeper", 5), Held));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Supervise, "gatekeeper", null, Held));

        Assert.NotNull(RoleCensus.Check(ParticipantWork.Build, "intake", null, Seeded));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Build, null, null, Seeded));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Build, "designer", null, Seeded));
    }

    [Fact]
    public void A_role_taking_design_is_admitted_on_a_record_whose_marks_read_build_and_a_role_that_takes_neither_is_not()
    {
        var designer = RoleCatalogTests.Working("designer", ParticipantWork.Design);
        var chatty = RoleCatalogTests.Working("chatty", ParticipantWork.Chat);
        RoleRecord? At(string id, long revision) => id switch { "designer" when revision == 2 => designer, "chatty" when revision == 1 => chatty, _ => Seeded(id, revision) };

        Assert.Null(RoleCensus.Check(ParticipantWork.Build, "build", ("designer", 2), At));
        Assert.Contains("chatty", RoleCensus.Check(ParticipantWork.Build, "build", ("chatty", 1), At));
    }

    [Fact]
    public void A_record_is_held_by_the_revision_its_start_kept_and_a_revision_not_kept_fails()
    {
        var before = RoleCatalogTests.Working("builder", ParticipantWork.Build);
        var after = RoleCatalogTests.Working("builder", ParticipantWork.Chat);
        RoleRecord? At(string id, long revision) => id == "builder" ? revision == 2 ? before : revision == 3 ? after : null : Seeded(id, revision);

        Assert.Null(RoleCensus.Check(ParticipantWork.Build, "build", ("builder", 2), At));
        Assert.NotNull(RoleCensus.Check(ParticipantWork.Build, "build", ("builder", 3), At));
        Assert.Contains("revision 9", RoleCensus.Check(ParticipantWork.Build, "build", ("builder", 9), At));
    }

    [Fact]
    public void The_store_gives_each_revision_a_role_held_and_the_seed_at_revision_0()
    {
        var location = DesktopStore.LocationIn(_home);
        // The seeded Design role takes design machine-wide, so an added one waits until it is removed.
        RoleCatalog.Write(location, RoleChange.Remove("design"), () => StoreSettled.Yes);
        RoleCatalog.Write(location, new RoleChange.SetRole(RoleCatalogTests.Working("designer", ParticipantWork.Design)), () => StoreSettled.Yes);
        RoleCatalog.Write(location, new RoleChange.SetRole(RoleCatalogTests.Working("designer", ParticipantWork.Design, new RoleScope(RoleScope.Workspace, "w1"))), () => StoreSettled.Yes);

        Assert.Equal(RoleScope.Everywhere, RoleCatalog.RecordAt(location, "designer", 1)!.Scope);
        Assert.Equal(RoleScope.Workspace, RoleCatalog.RecordAt(location, "designer", 2)!.Scope.Kind);
        Assert.Null(RoleCatalog.RecordAt(location, "designer", 3));
        Assert.Equal("build", RoleCatalog.RecordAt(location, "build", 0)!.Id);
    }
}
