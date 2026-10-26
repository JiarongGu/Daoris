namespace Daoris.Driver.Tests;

/// <summary>
/// CONSOLE3a: what stops a session's background work, held by session while its protocol-door session
/// is open, so the page's route reaches it across the ticks and the conversations that own the sessions.
/// </summary>
public sealed class TaskStopsTests
{
    [Fact]
    public async Task A_stop_reaches_the_session_that_holds_the_task_by_its_id()
    {
        var processes = new SessionProcesses();
        var asked = new List<string>();
        using var held = processes.OpenTaskStops("c-1", (task, _) =>
        {
            asked.Add(task);
            return Task.FromResult(true);
        });

        Assert.True(await processes.StopTaskAsync("c-1", "t9", CancellationToken.None));
        Assert.Equal(["t9"], asked);
    }

    /// <summary>Null is "nothing on this registry holds that session's tasks", which is not "stopped nothing".</summary>
    [Fact]
    public async Task A_session_nothing_holds_answers_null_and_a_closed_one_is_let_go()
    {
        var processes = new SessionProcesses();
        Assert.Null(await processes.StopTaskAsync("c-1", "t9", CancellationToken.None));

        var held = processes.OpenTaskStops("c-1", (_, _) => Task.FromResult(false));
        Assert.False(await processes.StopTaskAsync("c-1", "t9", CancellationToken.None));

        held.Dispose();
        Assert.Null(await processes.StopTaskAsync("c-1", "t9", CancellationToken.None));
    }

    /// <summary>A session opened again replaces the old one, and the old one's close does not take the new away.</summary>
    [Fact]
    public async Task A_stale_registration_closing_leaves_the_current_one()
    {
        var processes = new SessionProcesses();
        var first = processes.OpenTaskStops("c-1", (_, _) => Task.FromResult(false));
        using var second = processes.OpenTaskStops("c-1", (_, _) => Task.FromResult(true));

        first.Dispose();
        Assert.True(await processes.StopTaskAsync("c-1", "t9", CancellationToken.None));
    }
}
