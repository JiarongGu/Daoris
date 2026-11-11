using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's step that gives <i>Second check</i> its first choices (Codex, then Claude Code · opus · high, each 40 starts a day) once,
/// in one transaction, and never where the person staffed or changed the role or placed a supervisor of their own deciding the kind.
/// </summary>
public sealed class SecondCheckStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "second-check-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Store => DesktopStore.LocationIn(_home);

    private RoleStaffing Staffing => new(_home, () => StoreSettled.Yes);

    private IReadOnlyList<ActorChoice>? Choices() =>
        Staffing.Read() is { } read && read.Actors.TryGetValue(SecondCheck.Role, out var choices) ? choices : null;

    private HomeMigrationRun Run()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new SecondCheckStep().Run(run);
        return run;
    }

    [Fact]
    public void A_fresh_home_is_given_the_first_choices_with_their_day_s_budget_and_a_second_run_writes_nothing()
    {
        var first = Run();

        Assert.Equal(SecondCheck.Choices, Choices());
        var policy = Staffing.Policy(SecondCheck.Role).Policy!;
        Assert.Equal(StaffRules.Order, policy.Rule);
        Assert.All(SecondCheck.Choices, choice => Assert.Equal(40, policy.Of(StaffKey.Of(choice))!.Budget!.Starts));
        Assert.NotEmpty(first.Changed);
        Assert.Equal(RoleHolders.Rules, RoleCatalog.Read(Store).Slots.Single(slot => slot.Id == SecondCheck.Role).Role!.Hold!.Holder);

        var again = Run();

        Assert.Empty(again.Changed);
        Assert.Equal(1, Staffing.Policy(SecondCheck.Role).Revision);
    }

    [Fact]
    public void The_person_s_own_staffing_of_the_role_is_kept()
    {
        Staffing.ImportFile();
        IReadOnlyList<ActorChoice> own = [new("claude-code", "sonnet")];
        Staffing.SetAdded(SecondCheck.Role, own);

        var run = Run();

        Assert.Equal(own, Choices());
        Assert.Empty(run.Changed);
    }

    [Fact]
    public void A_role_the_person_turned_off_or_removed_is_given_nothing()
    {
        var seeded = RoleCatalog.Seed.Single(role => role.Id == SecondCheck.Role);
        RoleCatalog.Write(Store, RoleChange.Set(seeded with { Hold = seeded.Hold! with { Window = 7 } }), () => StoreSettled.Yes);

        Assert.Empty(Run().Changed);
        Assert.Null(Choices());

        RoleCatalog.Write(Store, RoleChange.Remove(SecondCheck.Role), () => StoreSettled.Yes);
        Assert.Empty(Run().Changed);
        Assert.Null(Choices());
    }

    [Fact]
    public void A_supervisor_of_the_person_s_own_deciding_the_kind_leaves_the_seeded_one_unstaffed()
    {
        RoleCatalog.Write(Store, RoleChange.Remove(SecondCheck.Role), () => StoreSettled.Yes);
        var own = new RoleRecord("my-reader", AuthoredText.Of("My reader"), RoleRecord.Appointed, [], RoleScope.Everywhere,
            new RoleHold(AppointedRoles.Supervisor, ["ask-daoris.report"], RoleHolders.Terminal) { Decides = [SupervisorKinds.AskDaoris] });
        RoleCatalog.Write(Store, RoleChange.Set(own), () => StoreSettled.Yes);

        Assert.Empty(Run().Changed);
        Assert.Null(Choices());
    }

    [Fact]
    public void The_step_is_one_of_the_home_s_migration()
    {
        Assert.Contains(HomeMigration.Steps, step => step is SecondCheckStep && step.Name == "second-check");
    }
}
