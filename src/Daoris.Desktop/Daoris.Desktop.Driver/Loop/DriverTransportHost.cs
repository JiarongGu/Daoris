using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a <see cref="TransportRun"/> asks of the loop, answered from the driver that holds it (AGENTPROVIDER1m-i part 1): the run's wait, its
/// bounded capture, its watch's reading, its kept conversations and downloads are the driver's own, so the run repeats none of them. The run sits
/// in Agents and the driver above it, so this port is where the two meet.
/// </summary>
internal sealed class DriverTransportHost(Driver driver) : ITransportHost
{
    public string Home => driver.Context.Home;

    public SessionProcesses Processes => driver.Context.Processes;

    public MachineLog? Log => driver.Log;

    public TextReader Hearing(TextReader reader, string sessionId) => driver.Hearing(reader, sessionId);

    public Task<int?> WaitAsync(
        Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet, Action? quietEnding) =>
        driver.WaitAsync(process, ct, minutes, timingOut, quiet, quietEnding);

    public Task<bool> CaptureWithinAsync(Task capture, string sessionId) => driver.CaptureWithinAsync(capture, sessionId);

    public Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct) => driver.KeepHandedAsync(sessionId, info, ct);

    public void KeepConversation(string sessionId, string adapter, string conversation) =>
        driver.Context.Conversations.Keep(sessionId, adapter, conversation);

    public IReadOnlyList<string>? Command(string adapter) => driver.Context.Config.Commands.GetValueOrDefault(adapter);

    public ProcessStartInfo Prepared(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next) =>
        driver.Prepare(adapter, target, selection, next).Info;

    public TimeSpan LingerBound => Driver.LingerBound;

    public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => Driver.EndedLingeringAsync(process, Driver.LingerBound, ending);
}
