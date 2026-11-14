using System.Diagnostics;
using System.Runtime.CompilerServices;
using Daoris.Driver;
using Daoris.Ports.Ambient;
using Daoris.Ports.Processes;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The root of a unit test process: it chooses the ports the library reads, once, before any test runs, and chooses fakes. The desktop
/// store's engine is <see cref="MemoryUnitOfWork"/> over the scratch files a test's home holds; the machine is <see cref="Processes"/>,
/// four places and this test process the one that runs, and its seal <see cref="ScriptedSeal"/>, so the unit tier names no adapter; the
/// admission of each start is the application's own git policy, which these tests prove.
/// </summary>
internal static class TestPorts
{
    /// <summary>
    /// The scripted machine every test of this process shares. A process it did not make is answered by the machine's own liveness,
    /// compiled in: a bridge while the Process classes, whose sessions and shells are real processes, still run in this project; it
    /// goes when they leave it for the rehearsal tier.
    /// </summary>
    internal static ScriptedProcesses Processes { get; } = new ScriptedProcesses(4)
        .Live(Environment.ProcessId, Created())
        .Beyond((id, created) => Daoris.Adapters.Machine.ProcessLiveness.Alive(id, created.UtcTicks));

    [ModuleInitializer]
    internal static void Choose() => AmbientPorts.Set(new PortSet
    {
        Clock = new MovedClock(),
        Store = MemoryUnitOfWork.Engines,
        Admission = Processes,
        Processes = Host,
        Liveness = Processes,
        StartPolicy = GitStartAdmission.Admit,
        Seal = ScriptedSeal.Port,
        Git = Git,
        SshKeys = SshKeys.Convert,
        Repository = Repository,
    });

    /// <summary>The repository every test reaches unless its flow chose a memory one: the driver's own over the git a flow reaches.</summary>
    internal static FlowRepository Repository { get; } = new(GitRepository.Shared);

    /// <summary>The machine's host over the scripted places, whose starts are made by the stand-in a flow chose (<see cref="ProcessStarts.Within"/>) and else by the runtime.</summary>
    internal static IProcessHost Host { get; } = new Daoris.Adapters.Machine.MachineProcessHost(
        Processes, GitStartAdmission.Admit, new Daoris.Adapters.Machine.ProcessOwner(OwnedProcesses.Own, OwnedProcesses.End),
        info =>
        {
            if (GitStartInputs.Placed is { } asked) GitStartInputs.Hand(info, asked);
            return (ProcessStarts.Standing ?? Process.Start)(info);
        }) { Timed = ProcessStarts.Told };

    internal static FlowGit Git { get; } = new(new Daoris.Adapters.Git.GitProcessRuns(Host, GitRunPolicy.Shared));
    internal static ScriptedSshKeys SshKeys { get; } = new();

    private static DateTimeOffset Created()
    {
        using var self = Process.GetCurrentProcess();
        return new DateTimeOffset(self.StartTime.ToUniversalTime(), TimeSpan.Zero);
    }
}
