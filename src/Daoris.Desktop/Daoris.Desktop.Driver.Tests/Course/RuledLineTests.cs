using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1p (the context drive read, R1): a line's role and place for its rules are resolved once, so the answers-alone hold, the
/// line's facts and the planner read one rule for a line under a scoped added role, whose record's <c>actor</c> is the seeded id.
/// </summary>
public sealed class RuledLineTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ruled-line", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly PriorSession Record = new("s1", "trees/default/engine/s-1", "failed")
    {
        Quest = "q1", Actor = "build", Adapter = "acp-stub", Repository = "engine", Said = [],
    };

    private void ScopedRole()
    {
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.True(staffing.ImportFile().How is StaffingImported.Done or StaffingImported.Already);
        var scoped = RoleCatalog.Seed[0] with { Id = "engine-build", Scope = new RoleScope(RoleScope.Repository, "engine") };
        RoleDoor.Apply(staffing, RoleChange.Set(scoped), [new ActorChoice("acp-stub", "sonnet", "low")]);
        SupervisorRuleBook.SeedFresh(_home, () => StoreSettled.Yes);
        SupervisorRule Rule(string count, int at) => new(count, count, SupervisorRules.Windows[count], at, SupervisorRules.Hold, null, "repository:engine", ["engine-build"]);
        Assert.True(new SupervisorRuleStore(_home, () => StoreSettled.Yes).Write(
            "repository:engine", 0, [Rule(SupervisorRules.AnswersAlone, 4), Rule(SupervisorRules.Failures, 5)], "test", SupervisorRuleDoor.World(_home)).Stored);
    }

    [Fact]
    public void A_line_under_a_scoped_added_role_is_read_by_one_rule_at_the_hold_and_the_planner()
    {
        ScopedRole();
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal) { ["s1"] = Record };

        var line = RuledLine.Of(_home, Record, "q1");
        Assert.Equal(("engine-build", "engine"), (line.Role, line.Repository));

        var hold = AnswersAloneHold.For(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), new GoOnMarks(_home), _home);
        Assert.Equal(4, hold.At("s1", records));

        var staff = new StaffLedger(_home, () => StoreSettled.Yes);
        var snapshot = new Snapshot([new QuestView("q1", "ask #a1", "engine", "The work", "", "Taken")], [], [], new Dictionary<string, int> { ["q1"] = 1 })
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Record },
        };
        Assert.Equal(5, StaffSnapshotCourse.FailuresAt(staff, snapshot)["q1"]);
        Assert.Equal(5, SupervisorRuleBook.FailuresAt(line.In(SupervisorRuleBook.Read(_home))));
    }
}
