using System.Diagnostics;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine.Tests;

/// <summary>What only the machine's host does: hand each process to the owner that contains it, and say how long a start waited and took.</summary>
public sealed class MachineProcessHostTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static ProcessSpec Exits() => new("cmd.exe", ["/d", "/c", "exit 0"], "Git", AdmissionClasses.Background);

    [Fact]
    public async Task A_started_process_is_handed_to_the_owner_with_its_purpose_before_its_caller_holds_it()
    {
        var taken = new List<(int Id, string Purpose)>();
        var host = new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null, (process, purpose) =>
        {
            taken.Add((process.Id, purpose));
            return true;
        });

        await using var run = await host.StartAsync(Exits(), CancellationToken.None);
        await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Equal([(run.Id, "Git")], taken);
    }

    [Fact]
    public async Task A_start_is_timed_in_its_wait_for_a_place_and_its_creation()
    {
        var told = new List<(long Waited, long Created)>();
        var host = new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null) { Timed = (waited, created) => told.Add((waited, created)) };

        await using var run = await host.StartAsync(Exits(), CancellationToken.None);
        await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        var only = Assert.Single(told);
        Assert.True(only.Created > 0 && only.Waited >= 0);
    }

    [Fact]
    public async Task A_process_that_holds_its_pipe_past_its_exit_is_not_waited_for_beyond_the_grace()
    {
        // The shell exits at once; the ping it left behind holds the shell's pipes for a minute.
        var host = new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null);
        var spec = new ProcessSpec("cmd.exe", ["/d", "/c", "start /b ping -n 60 127.0.0.1 & echo said"], "Git", AdmissionClasses.Background);
        await using var run = await host.StartAsync(spec, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Equal(0, end.Exit);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20));
        await run.StopAsync(CancellationToken.None);
    }
}
