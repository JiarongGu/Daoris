using System.Diagnostics;

namespace Daoris.Driver;

/// <param name="Considerations">Every open quest, with its verdict and reason — the plan, printable.</param>
/// <param name="Events">What actually happened this tick: sessions concluded, held, or refused.</param>
/// <param name="Progressed">
/// Whether any session was actually OPENED this tick. A planned start that held (dirty tree) or was
/// refused (raced) is not progress — and treating it as progress is an infinite loop: the same quest
/// would plan, hold, and plan again forever.
/// </param>
public sealed record TickReport(
    IReadOnlyList<Consideration> Considerations, IReadOnlyList<string> Events, bool Progressed)
{
    public bool PlannedAnything => Considerations.Any(c => c.Verdict == StartVerdict.Start);
}

/// <summary>
/// The loop D45 exists for: watch → plan → spawn → observe. One tick fetches a snapshot, plans it
/// (pure), and runs every planned start to its conclusion — sessions in different repositories run
/// together, which is the point of the whole direction.
/// </summary>
/// <remarks>
/// <para><b>The driver never writes quest state.</b> It opens and advances SESSION records through the
/// service's ledger; the spawned session claims and closes its own quest through its own connector
/// (D46 §3). A failure here therefore leaves the quest exactly where it was.</para>
///
/// <para><b>Everything here is observation.</b> The session's end is concluded from the exit code and
/// the quest's state (<see cref="Observation"/>); the evidence is what git says landed. Nothing is
/// taken from the session's word, because outside sessions have no word to give.</para>
/// </remarks>
public sealed class Driver(
    ServiceClient service, DriverConfig config, AdapterSet adapters, string home,
    SessionProcesses? processes = null, RemoteSyncSet? sync = null, SessionOutput? output = null)
{
    // Shared across the per-tick instances a watch loop constructs, so a control surface can reach
    // what is actually running; per-instance when nobody passes one, which no test has to care about.
    private readonly SessionProcesses _processes = processes ?? new SessionProcesses();

    /// <summary>One decision-and-execution round. Returns what happened, for whoever is watching.</summary>
    public async Task<TickReport> TickAsync(CancellationToken ct = default)
    {
        var events = new List<string>();

        // The sync runs before the snapshot, so this tick plans over a fresh mirror (D47 §9). Its
        // failure is an event, never a dead tick: records sync eventually and the next tick retries —
        // but a feed dying quietly looks exactly like a family with nothing to say, so the wall is named.
        if (sync is not null)
        {
            var synced = await sync.RunOnceAsync(ct).ConfigureAwait(false);
            if (synced.Problem is not null)
            {
                events.Add($"sync  {synced.Problem}");
            }

            // What the remote understood and deliberately did not take (D48 §6) — a stale or
            // branch feed. Reported as its own kind of line, because "the deployment kept a newer
            // view" is news about the family, not a fault in this machine.
            foreach (var note in synced.Notes) events.Add($"held  {note}");
        }

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var plan = Planner.Plan(snapshot, config);
        var progressed = false;

        var starts = plan.Where(c => c.Verdict == StartVerdict.Start).ToList();
        var runs = starts.Select(async start =>
        {
            var (line, opened) = await RunAsync(start, ct).ConfigureAwait(false);
            lock (events)
            {
                events.Add(line);
                progressed |= opened;
            }
        });
        await Task.WhenAll(runs).ConfigureAwait(false);

        return new TickReport(plan, events, progressed);
    }

    /// <summary>
    /// Tick until nothing progresses — the deterministic mode a gate drives. Sessions run inside
    /// their tick, so "no progress" means "nothing left that this driver may begin": a queue that is
    /// empty, held, or waiting on the person.
    /// </summary>
    public async Task<IReadOnlyList<TickReport>> RunUntilIdleAsync(CancellationToken ct = default)
    {
        var reports = new List<TickReport>();
        while (true)
        {
            var report = await TickAsync(ct).ConfigureAwait(false);
            reports.Add(report);
            if (!report.Progressed) return reports;
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
        }
    }

    private async Task<(string Line, bool Opened)> RunAsync(Consideration start, CancellationToken ct)
    {
        var quest = start.Quest;
        var root = start.Root!;

        // Clean tree, or nothing: uncommitted changes are somebody's work in flight (D46 §3). No
        // session record exists yet, so a hold here costs nothing and destroys nothing.
        var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
        if (!clean)
        {
            return ($"held  #{quest.Id} → {quest.To}: {detail}", false);
        }

        // The ledger judges the open — the same door any other client would use. A refusal here is
        // an answer (someone else got there first), not an error.
        var (sessionId, message) = await service.OpenSessionAsync(quest.Id, config.Adapter, ct).ConfigureAwait(false);
        if (sessionId is null)
        {
            return ($"refused  #{quest.Id} → {quest.To}: {message}", false);
        }

        try
        {
            var adapter = adapters.Resolve(config.Adapter);
            var target = new SessionTarget(
                quest.Id, quest.Title, quest.Body, quest.From, quest.To, root, service.BaseUrl);
            var info = adapter.Prepare(target, config.Commands.GetValueOrDefault(adapter.Name));

            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var before = await WorkingTree.HeadAsync(root, ct).ConfigureAwait(false);
            var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            using var process = Process.Start(info)
                ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            using var tracked = _processes.Track(sessionId, process);
            var capture = CaptureAsync(process, transcript, sessionId, ct);

            await service.AdvanceAsync(sessionId, "working", transcript: transcript, ct: ct).ConfigureAwait(false);

            var exitCode = await WaitAsync(process, ct).ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // The person's stop outranks the observation: a killed session leaves the same signals as
            // a crashed one, and only this flag knows whose decision the end was.
            var status = await service.QuestStatusAsync(quest.Id, ct).ConfigureAwait(false) ?? "Open";
            var conclusion = _processes.WasStopRequested(sessionId)
                ? new SessionConclusion("stopped", "the person stopped it.")
                : exitCode is int code
                    ? Observation.Conclude(code, status)
                    : new SessionConclusion("failed", $"timed out after {config.TimeoutMinutes} minutes and was killed.");

            var evidence = await WorkingTree.CommitsSinceAsync(root, before, ct).ConfigureAwait(false);
            await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct).ConfigureAwait(false);

            return ($"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}): {conclusion.Note}", true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver. WaitAsync already ended the process tree, so nothing
            // is orphaned — and the record must say so rather than sit at "working" forever. The write
            // rides an unbound token: the cancelled one would refuse the very report it caused.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "stopped",
                    note: "the driver was stopped while this ran; the session's process was ended with it.",
                    ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return ($"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.", true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The record must say what the driver saw, even when what it saw was its own failure —
            // an abandoned "starting" row reads as a session that never ends.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", note: error.Message, ct: ct).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return ($"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}", true);
        }
        finally
        {
            // However this ended, the stream is over. The buffer stays readable so whoever was
            // watching can see how it finished; the transcript remains the durable copy either way.
            // In the finally rather than the happy path, because a session that FAILED is the one
            // whose last lines someone most wants to read.
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// Exit code, or null when the timeout killed it. Either way the tree dies with it — a timeout
    /// AND a driver shutdown both end the process, because an orphaned agent session working a quest
    /// nobody is observing is the one thing worse than a failed one.
    /// </summary>
    private async Task<int?> WaitAsync(Process process, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(config.TimeoutMinutes));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw; // shutdown: the caller records "stopped"
            return null; // timeout: the caller records "failed"
        }
    }

    /// <summary>
    /// Both streams into one transcript file — diagnostic, never the record (D46 §4) — and, since
    /// D49 §2, teed into the live console as they pass.
    /// </summary>
    /// <remarks>
    /// One pump, two destinations, and the file is written FIRST: the durable copy must never be the
    /// thing that loses a line to an in-memory reader's problem. The console is optional because the
    /// headless driver has nobody to show it to — the buffer exists only where something reads it.
    /// </remarks>
    private async Task CaptureAsync(Process process, string transcript, string sessionId, CancellationToken ct)
    {
        await using var file = new StreamWriter(transcript, append: false);
        var stdout = PumpAsync(process.StandardOutput, file, sessionId, output, ct);
        var stderr = PumpAsync(process.StandardError, file, sessionId, output, ct);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
    }

    /// <summary>
    /// One stream into the transcript and the console. Internal rather than local so the tee itself is
    /// testable without a process: the property worth holding is that a line reaches BOTH.
    /// </summary>
    internal static async Task PumpAsync(
        TextReader reader, TextWriter file, string sessionId, SessionOutput? output, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            lock (file) file.WriteLine(line);
            output?.Append(sessionId, line);
        }
    }
}
