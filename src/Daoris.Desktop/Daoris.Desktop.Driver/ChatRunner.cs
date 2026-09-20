using System.Diagnostics;

namespace Daoris.Driver;

/// <param name="SessionId">The record's id, when one was opened — null when nothing started.</param>
/// <param name="Message">What to tell the person: the ledger's sentence, or why nothing began.</param>
public sealed record ChatStart(string? SessionId, string Message);

/// <summary>
/// A conversation with an agent in one repository (D49 §3) — the session D46 built, entered by a
/// person instead of planned from a quest.
/// </summary>
/// <remarks>
/// <para><b>Nothing here is a chat loop.</b> Daoris pipes text and observes; the harness carries the
/// model and the conversation (D24, `model-decoupling`). This class spawns a process, relays the
/// person's lines into it, keeps the transcript and the console exactly as the driven path does, and
/// concludes the record from what it can see. A Daoris-owned loop calling a model API was rejected in
/// the design: it would duplicate what every harness already is, and produce sessions with no
/// doctrine path into them.</para>
///
/// <para><b>A chat may open on a dirty tree</b>, where a driven session may not (D46 §3). The
/// clean-tree rule exists because an unattended agent would entangle itself with somebody's work in
/// flight; in a conversation the person IS present, and it is their work in flight.</para>
///
/// <para><b>The lock is the ledger's</b>, not this class's: one active session per repository,
/// judged in one place for every door. This asks, and reports the refusal verbatim.</para>
/// </remarks>
public sealed class ChatRunner(
    ServiceClient service,
    AdapterSet adapters,
    string home,
    SessionProcesses processes,
    SessionOutput? output = null)
{
    /// <summary>
    /// Open a chat and put a harness behind it. The record is the service's; the process is this
    /// machine's, and never leaves it (D46 §7).
    /// </summary>
    /// <param name="onEnded">
    /// Called when the conversation ends, with the state the record took. The caller decides what that
    /// becomes — a console line, an IPC event, nothing at all.
    /// </param>
    public async Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (!resolved.Interactive)
        {
            // Asked BEFORE the record is opened: a session record for a conversation that could never
            // have happened would hold the repository and explain nothing.
            return new(
                null,
                $"the `{resolved.Name}` adapter does not hold conversations — it spawns a harness that "
                + "takes its target once and runs to completion.");
        }

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var root = snapshot.Repositories
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase))
            ?.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return new(
                null,
                $"`{repository}` has no checkout on this machine — a conversation runs IN a working "
                + "tree. `daoris connect` from inside it, or add it from Projects.");
        }

        var (sessionId, message) = await service.OpenChatAsync(repository, resolved.Name, ct)
            .ConfigureAwait(false);
        if (sessionId is null) return new(null, message);

        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

        Process process;
        try
        {
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);
            process = Process.Start(resolved.PrepareChat(
                new ChatTarget(repository, root, service.BaseUrl),
                config.Commands.GetValueOrDefault(resolved.Name)))
                ?? throw new DriverException($"the {resolved.Name} adapter's process did not start");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The record exists and must say what happened, or it sits at `starting` forever holding
            // the repository — the same rule the driven path follows.
            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
            return new(null, error.Message);
        }

        // The conversation outlives this call: the person types, the harness answers, and the record
        // moves when the process does. Watched on an unbound token deliberately — a chat is not ended
        // by the request that started it.
        _ = WatchAsync(sessionId, process, transcript, onEnded);

        return new(sessionId, message);
    }

    /// <summary>Send a person's message to a live chat. False when there is nothing listening.</summary>
    public bool Say(string sessionId, string message) => processes.Send(sessionId, message);

    private async Task WatchAsync(
        string sessionId, Process process, string transcript, Func<string, string, Task>? onEnded)
    {
        using var tracked = processes.Track(sessionId, process);
        try
        {
            var capture = Driver.CaptureAsync(process, transcript, sessionId, output, CancellationToken.None);
            await service.AdvanceAsync(sessionId, "working", transcript: transcript).ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // The person's stop outranks the observation, exactly as for a driven session: a killed
            // process leaves the same signals as one that ended on its own, and only this flag knows
            // whose decision it was. Otherwise a conversation simply ended — `completed`, with no
            // evidence claim beyond whatever its commits already say.
            var stopped = processes.WasStopRequested(sessionId);
            await Conclude(
                sessionId,
                stopped ? "stopped" : "completed",
                stopped
                    ? "the person ended the conversation."
                    : "the conversation ended; its commits are its record.",
                onEnded).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    private async Task Conclude(
        string sessionId, string state, string note, Func<string, string, Task>? onEnded)
    {
        try
        {
            await service.AdvanceAsync(sessionId, state, note: note).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException)
        {
            // Best-effort by construction: the host may already be gone on the same shutdown, and the
            // first failure is the report.
        }

        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, state).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }
}
