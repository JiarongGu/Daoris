using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a <see cref="TransportRun"/> asks of the loop, answered from the driver that holds it (AGENTPROVIDER1m-i): the run's wait, its bounded
/// capture, its watch's reading, its kept conversations and downloads and its processes' lineage brackets are the driver's own, so the run
/// repeats none of them. The run sits in Agents and the driver above it, so this port is where the two meet.
/// </summary>
internal sealed class DriverTransportHost(Driver driver) : ITransportHost
{
    public string Home => driver.Context.Home;

    public SessionProcesses Processes => driver.Context.Processes;

    public MachineLog? Log => driver.Log;

    public TextReader Hearing(TextReader reader, string sessionId) => driver.Hearing(reader, sessionId);

    public string Bracketed(string sessionId, ProcessStartInfo info, string? moved) => Driver.Bracketed(Home, sessionId, info, moved);

    public Task<Process?> SpawnedAsync(string proc, Func<Task<Process?>> spawn) => Driver.Spawned(proc, spawn);

    public Task<int?> WaitAsync(
        string sessionId, string proc, Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet,
        Action? quietEnding) =>
        driver.WaitedAsync(sessionId, proc, driver.WaitAsync(process, ct, minutes, timingOut, quiet, quietEnding), ct);

    public Task<bool> DrainedAsync(string sessionId, string proc, Task capture) => driver.DrainedAsync(sessionId, proc, capture);

    public void Ended(string sessionId, string proc, int? exit) => driver.Ended(sessionId, proc, exit);

    public Task AbandonedAsync(string sessionId, string proc, Process process, Task capture) =>
        Driver.AbandonedAsync(
            process, capture, sessionId, driver.Log, driver.Context.Output,
            exitRead: () => ChildLineage.Exited(Home, sessionId, proc, clean: false));

    public Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct) => driver.KeepHandedAsync(sessionId, info, ct);

    public void KeepConversation(string sessionId, string adapter, string conversation) =>
        driver.Context.Conversations.Keep(sessionId, adapter, conversation);

    public IReadOnlyList<string>? Command(string adapter) => driver.Context.Config.Commands.GetValueOrDefault(adapter);

    public ProcessStartInfo Prepared(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next) =>
        driver.Prepare(adapter, target, selection, next).Info;

    public TimeSpan LingerBound => Driver.LingerBound;

    public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => Driver.EndedLingeringAsync(process, Driver.LingerBound, ending);
}
