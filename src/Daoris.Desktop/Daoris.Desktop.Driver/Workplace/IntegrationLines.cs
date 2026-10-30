using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// One event of a line's run (the integration design §5.2's last column): its code, the line, and the candidate, request and outcome it is of.
/// Told to the log by the transport; a code and its facts, never a sentence.
/// </summary>
public sealed record IntegrationEvent(string Code, string Repository, string Line)
{
    public string? Candidate { get; init; }

    public string? Request { get; init; }

    public string? Outcome { get; init; }
}

/// <summary>The codes a line's run tells (§5.2), one per step and its outcomes.</summary>
public static class IntegrationEventCodes
{
    public const string Queued = "integration.queued";
    public const string Formed = "integration.formed";
    public const string Replayed = "integration.replayed";
    public const string Resolved = "integration.resolved";
    public const string Conflict = "integration.conflict";
    public const string Gates = "integration.gates";
    public const string Judged = "integration.judged";
    public const string Promoted = "integration.promoted";
    public const string Held = "integration.held";
    public const string Settled = "integration.settled";
    public const string Tidied = "integration.tidied";
    public const string Closed = "integration.closed";
    public const string HandedBack = "integration.handed-back";
    public const string Stopped = "integration.stopped";

    /// <summary>The event a step's end tells, by its step and outcome; null for an attempt a crash cut, which tells nothing until it ends.</summary>
    public static string? Of(IntegrationStep step) => step.Outcome switch
    {
        null or IntegrationStepOutcomes.Interrupted => null,
        IntegrationStepOutcomes.HandedBack or IntegrationStepOutcomes.Split => HandedBack,
        IntegrationStepOutcomes.Stopped => Stopped,
        IntegrationStepOutcomes.Held when step.Step == IntegrationStepNames.Promote => Held,
        _ => step.Step switch
        {
            IntegrationStepNames.Form => Formed,
            IntegrationStepNames.Replay => Replayed,
            IntegrationStepNames.Resolve => Resolved,
            IntegrationStepNames.Conflict => Conflict,
            IntegrationStepNames.Gates => Gates,
            IntegrationStepNames.Judge => Judged,
            IntegrationStepNames.Promote => Promoted,
            IntegrationStepNames.Settle => Settled,
            IntegrationStepNames.Tidy => Tidied,
            IntegrationStepNames.Close => Closed,
            _ => null,
        },
    };
}

/// <summary>What one line's run did: its words, the events of the steps it ended, and whether any request closed.</summary>
internal sealed record IntegrationLineRun(IReadOnlyList<string> Said, IReadOnlyList<IntegrationEvent> Events, bool Closed);

/// <summary>
/// One line's run to rest (INTEGRATE1g; the integration design §5.1, §5.2): a candidate formed and replayed, then its verdict, promotion,
/// settle, tidy and close, and the next candidate after it, until the line has nothing it can move now (nothing queued, a candidate held or
/// waiting, git unable to say). The transport runs it off the pass; nothing here waits on a timer.
/// </summary>
/// <remarks>
/// <para><b>A standing candidate</b> is gone on with where this process holds its claim, or takes it from a dead run (as the come-up's sweep
/// does); one a live run holds is left to that run. One line runs once at a time in this process, so a press and the transport never form
/// beside each other; the store's claim fences every other process.</para>
/// </remarks>
internal sealed class IntegrationLines(IntegrationStore store, string treesRoot)
{
    /// <summary>The most candidates one run forms, so a line whose candidates keep forming again still yields to the next look.</summary>
    internal const int Rounds = 16;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Running = new(StringComparer.OrdinalIgnoreCase);

    private readonly IntegrationReplays _replays = new(store, treesRoot);
    private readonly IntegrationPromotes _promotes = new(store, treesRoot);
    private readonly IntegrationSweeps _sweeps = new(store, treesRoot);

    /// <summary>Run <paramref name="asks"/>' line to rest; the events are those of the steps it ended.</summary>
    public async Task<IntegrationLineRun> RunAsync(IntegrationSweepAsks asks, CancellationToken ct = default)
    {
        var repository = asks.Replay.Repository;
        var line = asks.Replay.Line;
        var one = Running.GetOrAdd($"{store.Location}\n{repository}\n{line}", _ => new SemaphoreSlim(1, 1));
        await one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ToRestAsync(asks, repository, line, ct).ConfigureAwait(false);
        }
        finally
        {
            one.Release();
        }
    }

    private async Task<IntegrationLineRun> ToRestAsync(IntegrationSweepAsks asks, string repository, string line, CancellationToken ct)
    {
        var said = new List<string>();
        var events = new List<IntegrationEvent>();
        var known = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
        var closed = false;
        var open = store.Open().Where(request => Same(request, repository, line)).Select(request => request.Id).ToHashSet(StringComparer.Ordinal);

        for (var round = 0; round < Rounds; round++)
        {
            string? candidate = null;
            try
            {
                if (store.StandingFor(repository, line) is { } standing)
                {
                    candidate = Seen(standing.Id, known);
                    if (Claim(standing) is not { } claim)
                    {
                        said.Add($"candidate {standing.Id} stands for `{line}` under a live run: it is that run's to go on with.");
                        break;
                    }

                    var went = await _sweeps.GoOnAsync(claim, asks, ct).ConfigureAwait(false);
                    said.Add($"candidate {standing.Id}: {went.Said}");
                }
                else
                {
                    var formed = await _replays.RunAsync(asks.Replay, ct).ConfigureAwait(false);

                    // A candidate this run formed: every step it holds is this run's to tell.
                    if (formed.Candidate is { } made)
                    {
                        if (formed.Claim is not null) known.TryAdd(made.Id, []);
                        candidate = Seen(made.Id, known);
                    }
                    if (formed.Kind is IntegrationReplayRuns.Idle or IntegrationReplayRuns.Unread)
                    {
                        if (formed.Kind == IntegrationReplayRuns.Unread) said.Add(formed.Said);
                        break;
                    }

                    said.Add(formed.Said);
                    if (formed is { Kind: IntegrationReplayRuns.Replayed, Claim: { } claim, Candidate.Stands: true }
                        && store.Candidate(claim.Candidate) is { Stands: true })
                    {
                        var promoted = await _promotes.RunAsync(claim, asks.Promote, ct).ConfigureAwait(false);
                        said.Add(promoted.Said);
                    }
                }
            }
            catch (IntegrationClaimLostException lost)
            {
                said.Add(lost.Message);
                break;
            }
            finally
            {
                if (candidate is not null) events.AddRange(Ended(candidate, known[candidate], repository, line));
            }

            // A candidate still standing is held or waits: the next look tries it again.
            if (candidate is null || store.Candidate(candidate) is { Stands: true }) break;
        }

        foreach (var id in open)
        {
            if (store.Request(id) is { ClosedAt: not null }) closed = true;
        }

        return new IntegrationLineRun(said, events, closed);
    }

    /// <summary>The claim this run goes on under: its own where this process holds it, else taken from a dead run; null where a live run holds it.</summary>
    private IntegrationClaim? Claim(IntegrationCandidate standing) =>
        standing.ClaimRun == store.Run
            ? new IntegrationClaim(standing.Id, standing.ClaimRun, standing.ClaimGeneration)
            : store.Take(standing.Id, standing.ClaimGeneration).Claim;

    /// <summary>The candidate's steps already ended, kept the first time it is met, so only the steps this run ends are told.</summary>
    private string Seen(string candidate, Dictionary<string, HashSet<long>> known)
    {
        if (!known.ContainsKey(candidate)) known[candidate] = [.. store.Steps(candidate).Where(step => !step.Open).Select(step => step.Id)];
        return candidate;
    }

    private IEnumerable<IntegrationEvent> Ended(string candidate, HashSet<long> known, string repository, string line)
    {
        foreach (var step in store.Steps(candidate).Where(step => !step.Open && known.Add(step.Id)))
        {
            if (IntegrationEventCodes.Of(step) is { } code)
            {
                yield return new IntegrationEvent(code, repository, line) { Candidate = candidate, Request = step.Request, Outcome = step.Outcome };
            }
        }
    }

    private static bool Same(IntegrationRequest request, string repository, string line) =>
        string.Equals(request.Repository, repository, StringComparison.OrdinalIgnoreCase) && request.Line == line;
}
