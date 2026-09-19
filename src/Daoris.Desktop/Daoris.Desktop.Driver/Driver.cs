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
public sealed class Driver(ServiceClient service, DriverConfig config, AdapterSet adapters, string home)
{
    /// <summary>One decision-and-execution round. Returns what happened, for whoever is watching.</summary>
    public async Task<TickReport> TickAsync(CancellationToken ct = default)
    {
        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var plan = Planner.Plan(snapshot, config);
        var events = new List<string>();
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
            var capture = CaptureAsync(process, transcript, ct);

            await service.AdvanceAsync(sessionId, "working", transcript: transcript, ct: ct).ConfigureAwait(false);

            var exitCode = await WaitAsync(process, ct).ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            var status = await service.QuestStatusAsync(quest.Id, ct).ConfigureAwait(false) ?? "Open";
            var conclusion = exitCode is int code
                ? Observation.Conclude(code, status)
                : new SessionConclusion("failed", $"timed out after {config.TimeoutMinutes} minutes and was killed.");

            var evidence = await WorkingTree.CommitsSinceAsync(root, before, ct).ConfigureAwait(false);
            await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct).ConfigureAwait(false);

            return ($"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}): {conclusion.Note}", true);
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
    }

    /// <summary>Exit code, or null when the timeout killed it. The tree dies with it — no orphans.</summary>
    private async Task<int?> WaitAsync(Process process, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(config.TimeoutMinutes));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Both streams into one transcript file — diagnostic, never the record (D46 §4).</summary>
    private static async Task CaptureAsync(Process process, string transcript, CancellationToken ct)
    {
        await using var file = new StreamWriter(transcript, append: false);
        var stdout = PumpAsync(process.StandardOutput, file, ct);
        var stderr = PumpAsync(process.StandardError, file, ct);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);

        static async Task PumpAsync(StreamReader reader, StreamWriter file, CancellationToken ct)
        {
            while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                lock (file) file.WriteLine(line);
            }
        }
    }
}
