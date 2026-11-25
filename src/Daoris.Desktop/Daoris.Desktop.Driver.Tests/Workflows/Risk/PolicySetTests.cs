using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// <c>daoris settings risk set</c> and the window's act table write a row at the revision they read, on the owner's own press, and journal it
/// naming the door; an agent's set is a proposal (<see cref="PolicyChanges.Propose"/>), a working session's press and a keyless one change nothing.
/// </summary>
public sealed class PolicySetTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "policy-set", Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private PolicyChanges Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, "{}");
        return new PolicyChanges(_home, () => Now, new OwnerTurns(_ => []), new PlaceCastFiles(_home, () => true));
    }

    private static readonly PolicyEdit Raise = new("repository/Aurora", "spend.money", RiskClasses.Owner, null, null);

    private RiskPolicy Policy() => RiskPolicy.Of(DriverConfig.Load(ConfigPath));

    [Fact]
    public void The_owners_press_writes_the_row_at_the_revision_it_read_and_the_change_journal_names_the_door()
    {
        var door = Open();
        var read = Policy().Revision;

        var set = door.Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Window, [Raise], read);

        Assert.True(set.Done, set.Said);
        var row = Assert.Single(Policy().Rows["repository/Aurora"]);
        Assert.Equal((RiskActs.SpendMoney, RiskClasses.Owner), (row.Kind, row.Class));
        var line = Assert.Single(PolicyChangeJournal.Read(_home));
        Assert.Equal((PolicyChangeLine.Window, read, Policy().Revision), (line.Door, line.RevisionBefore, line.RevisionAfter));
        Assert.Contains(line.Shifts, shift => shift is { Act: "spend.money", Effect: PolicyShift.Opens });
    }

    [Fact]
    public void A_policy_that_moved_since_it_was_read_is_stale_and_nothing_is_written()
    {
        var door = Open();

        var set = door.Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Cli, [Raise], "not-the-revision");

        Assert.Equal((false, PolicyRefusals.Stale), (set.Done, set.Code));
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
        Assert.Empty(PolicyChangeJournal.Read(_home));
    }

    [Fact]
    public void A_working_sessions_press_and_a_keyless_one_change_nothing_and_policy_change_stays_fixed()
    {
        var door = Open();
        var read = Policy().Revision;

        Assert.Equal(PolicyRefusals.Agent, door.Set(PolicyChannel.Agent("s-1"), PolicyChangeLine.Window, [Raise], read).Code);
        Assert.Equal(PolicyRefusals.Keyless, door.Set(PolicyChannel.Keyless, PolicyChangeLine.Window, [Raise], read).Code);
        Assert.Equal(PolicyRefusals.Keyless, door.Set(PolicyChannel.Of("s-ask"), PolicyChangeLine.Window, [Raise], read).Code);
        Assert.Equal(PolicyRefusals.Fixed, door.Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Window, [new PolicyEdit("machine", "policy.change", RiskClasses.None, null, null)], read).Code);
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void A_cast_is_set_through_the_cast_door_and_journaled()
    {
        var door = Open();

        var set = door.Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Cli, [PolicyEdit.Cast("machine", "supervisor", "person")], Policy().Revision);

        Assert.True(set.Done, set.Said);
        Assert.Equal("person", new PlaceCastFiles(_home, () => true).Book().At("supervisor", null, null, PlaceRiskBook.Unwritten.At(null, null)).Holder);
        Assert.Equal(PolicyChangeLine.Cli, Assert.Single(PolicyChangeJournal.Read(_home)).Door);
    }
}
