using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kit's door runner, the one way a door kind's work leaves the pass: its work runs under its start's authority and never the pass's,
/// once per start key, not while the drain says no, and a failure is told to its door.
/// </summary>
public sealed class NodeDoorRunsTests
{
    private static readonly StepAuthority Rebasing = StepAuthority.OfDoor("r-12", "rebase", 2);

    private sealed record Fixture(string Name) : NodeDoorStart
    {
        public override string Key => Name;
    }

    [Fact]
    public async Task Its_work_reads_its_start_s_authority_and_never_the_flow_it_was_started_from()
    {
        var runs = new NodeDoorRuns();
        StepAuthority? read = null;
        var started = false;
        await StepAuthority.RunAsync(StepAuthority.OfDoor("r-1", "pass", 1), () =>
        {
            started = runs.Start(new Fixture("k") { Authority = Rebasing }, _ =>
            {
                read = StepAuthority.Current;
                return Task.CompletedTask;
            }, _ => true);
            return Task.CompletedTask;
        });
        await runs.IdleAsync();

        Assert.True(started);
        Assert.Equal(Rebasing, read);
        Assert.Null(StepAuthority.Current);
    }

    [Fact]
    public async Task A_start_is_taken_once_by_its_key_and_not_while_the_drain_says_no()
    {
        var draining = false;
        var runs = new NodeDoorRuns(() => !draining);
        var held = new TaskCompletionSource();
        Assert.True(runs.Start(new Fixture("k") { Authority = Rebasing }, _ => held.Task, _ => true));
        Assert.False(runs.Start(new Fixture("k") { Authority = Rebasing }, _ => Task.CompletedTask, _ => true));

        draining = true;
        Assert.False(runs.Start(new Fixture("other") { Authority = Rebasing }, _ => Task.CompletedTask, _ => true));

        held.SetResult();
        await runs.IdleAsync();
        draining = false;
        Assert.True(runs.Start(new Fixture("k") { Authority = Rebasing }, _ => Task.CompletedTask, _ => true));
        await runs.IdleAsync();
    }

    [Fact]
    public async Task A_failure_is_told_to_its_door_and_the_key_let_go()
    {
        var runs = new NodeDoorRuns();
        Exception? told = null;
        Assert.True(runs.Start(new Fixture("k") { Authority = Rebasing }, _ => throw new IOException("the disk is gone"), error =>
        {
            told = error;
            return true;
        }));
        await runs.IdleAsync();

        Assert.Equal("the disk is gone", told?.Message);
        Assert.True(runs.Start(new Fixture("k") { Authority = Rebasing }, _ => Task.CompletedTask, _ => true));
        await runs.IdleAsync();
    }
}
