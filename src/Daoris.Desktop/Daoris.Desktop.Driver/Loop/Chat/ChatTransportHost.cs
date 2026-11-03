using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a conversation's <see cref="TransportRun"/> asks of the runner that holds it (AGENTPROVIDER1m-i, ONEWAY1h-iii): the conversation's own
/// waits, its kept conversations and downloads, and its process's lineage bracket, as every start's. A conversation is never ended on time and
/// no watch hears it, so its exit is waited for and its capture read to their ends, and its streams are read as they are.
/// </summary>
internal sealed class ChatTransportHost(
    string home, SessionProcesses processes, ServiceClient service, HarnessConversations conversations, DriverConfig config, SessionOutput? output)
    : ITransportHost
{
    public string Home => home;

    public SessionProcesses Processes => processes;

    public MachineLog? Log => null;

    public TextReader Hearing(TextReader reader, string sessionId) => reader;

    public string Bracketed(string sessionId, ProcessStartInfo info, string? moved) => Driver.Bracketed(home, sessionId, info, moved);

    public Task<Process?> SpawnedAsync(string proc, Func<Task<Process?>> spawn) => Driver.Spawned(proc, spawn);

    /// <summary>The exit, told to the token set (COWORKVIEW1a); a conversation hands no bound, so none is kept here.</summary>
    public async Task<int?> WaitAsync(
        string sessionId, string proc, Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet,
        Action? quietEnding)
    {
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        LineageTokens.Current.Exited(proc);
        return process.ExitCode;
    }

    /// <summary>The capture read to its end; one that faults writes the process's end unclean before the fault leaves, as a start's does.</summary>
    public async Task<bool> DrainedAsync(string sessionId, string proc, Task capture)
    {
        try
        {
            await capture.ConfigureAwait(false);
            return true;
        }
        catch
        {
            ChildLineage.Exited(home, sessionId, proc, clean: false);
            throw;
        }
    }

    /// <summary>Its end once its exit was read and its capture drained: clean where it exited 0 and nobody stopped it (FABRIC-C1a).</summary>
    public void Ended(string sessionId, string proc, int? exit) =>
        ChildLineage.End(home, sessionId, clean: exit == 0 && !processes.WasStopRequested(sessionId), DateTimeOffset.UtcNow, proc);

    public Task AbandonedAsync(string sessionId, string proc, Process process, Task capture) =>
        Driver.AbandonedAsync(process, capture, sessionId, log: null, output, exitRead: () => ChildLineage.Exited(home, sessionId, proc, clean: false));

    public Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct) =>
        DownloadKeeping.KeepHandedAsync(service, home, sessionId, info, ct);

    public void KeepConversation(string sessionId, string adapter, string conversation) => conversations.Keep(sessionId, adapter, conversation);

    public IReadOnlyList<string>? Command(string adapter) => config.Commands.GetValueOrDefault(adapter);

    /// <summary>Never asked: a conversation goes on through a start of its own (MSG1c), and its run is handed no selection to go on with.</summary>
    public ProcessStartInfo Prepared(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next) =>
        throw new NotSupportedException("a conversation goes on through a start of its own, never through its run's go-on.");

    public TimeSpan LingerBound => Driver.LingerBound;

    public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => Driver.EndedLingeringAsync(process, Driver.LingerBound, ending);
}
