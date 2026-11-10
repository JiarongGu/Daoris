using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's migration converts the stored supervisors to grants by kind (<c>answer</c> granted as today's three, <c>defer</c> dropped)
/// in <c>roles</c> and every revision, and gives each decision file its <c>kind</c>, but only once the home's newest build is kept as
/// this one and read back; until the step is recorded, nothing is converted and the role door refuses a supervisor's record.
/// </summary>
public sealed class SupervisorStepTests : IDisposable
{
    private const string Version = "9.9.9";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Store => DesktopStore.LocationIn(_home);

    private string Decisions => DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix);

    /// <summary>A home as an older build left it: the supervisor granted <c>answer</c> and <c>defer</c> in two revisions, and two decision files.</summary>
    private string Older()
    {
        var seeded = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor).Json();
        var old = seeded.Replace("\"question.answer\",\"course.go-on\",\"limit.move\"", "\"answer\",\"defer\"", StringComparison.Ordinal);
        Assert.NotEqual(seeded, old);
        RoleCatalog.Write(Store, RoleChange.Remove("design"), () => StoreSettled.Yes);
        using (var store = DesktopStore.Open(Store, StoreProcess.Options(() => StoreSettled.Yes)))
        {
            store.Write("roles", write =>
            {
                const string At = "2026-11-01T09:00:00.000Z";
                write.Execute("INSERT INTO roles (id, state, record, revision, at) VALUES ('supervisor', 'set', $record, 2, $at)", ("$record", old), ("$at", At));
                write.Execute("INSERT INTO role_revisions (id, revision, state, record, at) VALUES ('supervisor', 1, 'set', $record, $at)", ("$record", old), ("$at", At));
                write.Execute("INSERT INTO role_revisions (id, revision, state, record, at) VALUES ('supervisor', 2, 'set', $record, $at)", ("$record", old), ("$at", At));
            });
        }

        Directory.CreateDirectory(Decisions);
        File.WriteAllText(Path.Combine(Decisions, "d1.json"), """{"decision":"d1","session":"s1","question":{"id":"q-1"}}""");
        File.WriteAllText(Path.Combine(Decisions, "d2.json"), """{"id":"d2","session":"s1","course":{"point":"p-1"}}""");
        return old;
    }

    private IReadOnlyList<string> Records()
    {
        using var read = DesktopStoreReader.Open(Store)!;
        return [.. read.Query("SELECT record FROM roles WHERE id = 'supervisor' UNION ALL SELECT record FROM role_revisions WHERE id = 'supervisor'", row => row.GetString(0))];
    }

    private string KindOf(string file) => System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Decisions, file)))!["kind"]!.GetValue<string>();

    [Fact]
    public void The_step_converts_roles_every_revision_and_the_decision_files_once_the_home_s_newest_build_is_kept()
    {
        Older();

        var migrated = HomeMigration.Run(_home, [new SupervisorStep(() => Version)]);

        Assert.Null(migrated.Failed);
        Assert.Equal([SupervisorStep.StepName], migrated.Ran);
        Assert.True(SupervisorConversion.Converted(_home));
        Assert.Equal(Version, HomeLastBuild.Read(_home));
        Assert.All(Records(), record => Assert.Contains("\"authority\":[\"question.answer\",\"course.go-on\",\"limit.move\"]", record, StringComparison.Ordinal));
        Assert.Equal(SupervisorKinds.Seeded, RoleCatalog.Read(Store).Slots.Single(slot => slot.Id == AppointedRoles.Supervisor).Role!.Hold!.Acts);
        Assert.Equal((SupervisorKinds.Question, SupervisorKinds.Course), (KindOf("d1.json"), KindOf("d2.json")));

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new SupervisorStep(() => Version).Run(again);
        Assert.Empty(again.Changed);
    }

    [Fact]
    public void A_newest_build_that_is_not_kept_leaves_the_step_unrecorded_and_nothing_converted_until_a_start_whose_write_holds()
    {
        var old = Older();
        var kept = DaorisHome.Entry(_home, HomeEntry.LastBuild);
        Directory.CreateDirectory(kept);

        var failed = HomeMigration.Run(_home, [new SupervisorStep(() => Version)]);

        Assert.Equal(SupervisorStep.StepName, failed.Failed?.Step);
        Assert.False(SupervisorConversion.Converted(_home));
        Assert.All(Records(), record => Assert.Equal(old, record));
        Assert.False(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Decisions, "d1.json")))!.AsObject().ContainsKey("kind"));

        Directory.Delete(kept);
        Assert.Null(HomeMigration.Run(_home, [new SupervisorStep(() => Version)]).Failed);
        Assert.True(SupervisorConversion.Converted(_home));
        Assert.All(Records(), record => Assert.DoesNotContain("\"defer\"", record, StringComparison.Ordinal));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is SupervisorStep && step.Name == SupervisorStep.StepName);
    }
}
