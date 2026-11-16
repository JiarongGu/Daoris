using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every kind's leave climbs the reporting line (D304 point 3): each leave records the level that made it (<see cref="LineLeaves"/>), and
/// <see cref="ReportingLine.Climb"/> with that map says where the subject waits now. A leave by a level below the machine passes the subject to
/// the machine's supervisor; only an owner-class bound reaches the owner role; other help the machine's supervisor leaves ends as gave up.
/// </summary>
public sealed class LineLeavesTests : IDisposable
{
    private const string Engine = "engine";

    private static readonly RolePlace AtEngine = new(Repository: Engine);
    private static readonly SupervisorSubject Asked = SupervisorSubject.Question("s1", "q1");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-line-leaves-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static RoleSlot Supervisor(string id, RoleScope scope, string? holder)
    {
        var role = new RoleRecord(id, AuthoredText.Of(id), RoleRecord.Appointed, [], scope, new RoleHold(AppointedRoles.Supervisor, [.. SupervisorKinds.Seeded], holder));
        return new RoleSlot(id, RoleState.Set, role, null, 1, scope, Appointed: true);
    }

    private static ReportingLine Line(string? machineHolder = RoleHolders.Agent) => ReportingLine.Of(
        [Supervisor("engine-supervisor", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent), Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, machineHolder)],
        SupervisorKinds.Question, AtEngine);

    private static LineLeave Left(string level, string bound = "unrecorded") => new(At, level, level == LineLevels.Machine ? AppointedRoles.Supervisor : "engine-supervisor", bound, "the brief does not say");

    [Fact]
    public void A_question_the_repositorys_supervisor_left_waits_on_the_machines_supervisor_never_the_person()
    {
        IReadOnlyList<LineLeave> leaves = [Left(LineLevels.Repository)];

        var waits = LineClimb.Wait(Line(), leaves);

        Assert.Equal((LineLevels.Machine, LineHolders.Agent), (waits.At!.Level, waits.At.Holder));
        Assert.Equal([LinePasses.Left], waits.Passed.Select(pass => pass.Code));
        Assert.Equal(new RolePlace(), LineClimb.Place(Line(), AtEngine, leaves));
    }

    [Fact]
    public void Help_the_machines_supervisor_left_too_ends_as_gave_up_listed_to_nobody()
    {
        IReadOnlyList<LineLeave> leaves = [Left(LineLevels.Repository), Left(LineLevels.Machine)];

        var waits = LineClimb.Wait(Line(), leaves);

        Assert.True(waits.GaveUp);
        Assert.StartsWith("gave up: the repository's supervisor: the brief does not say", waits.Said(), StringComparison.Ordinal);
        Assert.Null(LineClimb.Place(Line(), AtEngine, leaves));
    }

    [Fact]
    public void An_owner_class_bound_passes_every_level_to_the_owner_role()
    {
        IReadOnlyList<LineLeave> leaves = [Left(LineLevels.Repository, OwnerClass.Money)];

        var waits = LineClimb.Wait(Line(), leaves);

        Assert.Equal(LineLevels.Owner, waits.At!.Level);
        Assert.Null(LineClimb.Place(Line(), AtEngine, leaves));
    }

    [Fact]
    public void A_machines_level_held_by_nobody_ends_the_climb_as_gave_up()
    {
        Assert.Null(LineClimb.Place(Line(machineHolder: null), AtEngine, [Left(LineLevels.Repository)]));
    }

    [Fact]
    public void A_subject_nobody_left_stays_at_its_place()
    {
        Assert.Equal(AtEngine, LineClimb.Place(Line(), AtEngine, []));
    }

    [Fact]
    public void The_journal_keeps_each_leave_by_its_subject_and_level()
    {
        var journal = new LineLeaves(_home);
        journal.Keep(Asked, Left(LineLevels.Repository));
        journal.Keep(SupervisorSubject.Question("s1", "q2"), Left(LineLevels.Machine));

        var read = journal.Of(Asked);

        Assert.Equal([LineLevels.Repository], read.Select(leave => leave.Level));
        Assert.Equal(("engine-supervisor", "unrecorded"), (read[0].Role, read[0].Bound));
        Assert.Empty(journal.Of(SupervisorSubject.Question("s2", "q1")));
    }
}
