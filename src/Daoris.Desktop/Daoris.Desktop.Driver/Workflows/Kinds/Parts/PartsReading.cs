namespace Daoris.Driver;

/// <summary>What an ask's plan requires and which of it has landed on the ask's branch, as the pass read it.</summary>
/// <param name="Required">Every part of the standing revision of the ask's own plan: a part a revision dropped is no longer named.</param>
/// <param name="Landed">The parts whose request promoted onto the ask's branch (<see cref="TrainParts.Landed"/>).</param>
public sealed record PartsReport(string Ask, int Revision, IReadOnlyList<string> Required, IReadOnlySet<string> Landed)
{
    public IReadOnlyDictionary<string, string> Repositories { get; init; } = new Dictionary<string, string>();

    /// <summary>The quest the plan gave each part, whose run is the part's: a part with none yet is not named.</summary>
    public IReadOnlyDictionary<string, string> Quests { get; init; } = new Dictionary<string, string>();

    /// <summary>What each part waits on before it starts: another part's receipt, by the plan's <c>after</c> edges naming one.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PartWait>> Waits { get; init; } = new Dictionary<string, IReadOnlyList<PartWait>>();

    /// <summary>One repository's delivery waits for its parts, and for any the plan gives to none, which land nowhere yet.</summary>
    public PartsReport For(string repository) =>
        this with
        {
            Required = [.. Required.Where(part => !Repositories.TryGetValue(part, out var at) || string.Equals(at, repository, StringComparison.OrdinalIgnoreCase))],
        };

    /// <summary>The required parts not landed yet.</summary>
    public IReadOnlyList<string> Waiting => [.. Required.Where(part => !Landed.Contains(part))];

    /// <summary>Every required part landed; a plan with no part has none to deliver and reaches nothing.</summary>
    public bool Reached => Required.Count > 0 && Waiting.Count == 0;
}

/// <summary>The reports a pass reads, one per ask the train holds a request for.</summary>
public static class PartsReports
{
    /// <summary>
    /// Each ask the integration holds a part's request for, with its plan as the service answers it; an ask whose plan is not read is left out,
    /// so its <c>parts</c> waits and is never reached on a guess.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, PartsReport>> ReadAsync(
        IntegrationStore store, ITrainPlans plans, CancellationToken ct, IEnumerable<string>? runAsks = null)
    {
        var parts = new TrainParts(store);
        var read = new Dictionary<string, PartsReport>(StringComparer.Ordinal);
        foreach (var ask in parts.Asks().Concat(runAsks ?? []).Distinct(StringComparer.Ordinal))
        {
            if (await plans.OfAsync(ask, ct).ConfigureAwait(false) is not { } plan) continue;
            read[ask] = new PartsReport(ask, plan.Revision, [.. plan.Parts.Select(part => part.Id)], parts.Landed(ask))
            {
                Repositories = plan.Parts.Where(part => part.Repository is { Length: > 0 }).ToDictionary(part => part.Id, part => part.Repository!, StringComparer.Ordinal),
                Quests = plan.Parts.Where(part => part.Quest is { Length: > 0 }).ToDictionary(part => part.Id, part => part.Quest!, StringComparer.Ordinal),
                Waits = plan.Parts.Where(part => part.Waits.Count > 0).ToDictionary(part => part.Id, part => part.Waits, StringComparer.Ordinal),
            };
        }

        return read;
    }

    /// <summary>
    /// The asks the runs bound here and not ended name: a part's run reads what it waits on (<see cref="PartReceipts"/>) and a joint check
    /// its parts' receipts (<see cref="JointChecks"/>) from its ask's report, whether or not the train holds a request for it yet.
    /// </summary>
    public static IEnumerable<string> RunAsks(string home) =>
        new GraphRuns(new NodeRecordFiles(home, () => false)).Read().Where(run => run.Ended is null).Select(run => run.Binding.Ask).OfType<string>();
}

/// <summary>
/// The <c>parts</c> node read from the integration's record: reached once every required part of the standing plan has landed on the ask's
/// branch, its batch green. A part's quest being Done is the worker's completion and counts for nothing here. Pure: the caller hands in the
/// report, and nothing is asked or written.
/// </summary>
public static class PartsReading
{
    /// <summary>Where the integration's record is named in a reach's evidence.</summary>
    public const string Record = "integration";

    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.NotBound(reading) is { } refused) return refused;
        if (reading.Parts is not { } report) return NodeObserving.Seen(reading, null, "the ask's plan and what landed of it were not read");
        if (report.Required.Count == 0) return NodeObserving.Seen(reading, null, $"ask #{report.Ask}'s plan names no part to land yet");
        if (!report.Reached)
        {
            var left = report.Waiting;
            return NodeObserving.Seen(reading, null, $"waits for {left.Count} of {report.Required.Count} parts to land on the ask's branch ({string.Join(", ", left)})");
        }

        return NodeObserving.Seen(reading, NodeStates.Reached, $"every part of ask #{report.Ask}'s plan (revision {report.Revision}) landed on its branch") with
        {
            Source = NodeSource.Integration,
            Acceptance = reading.Binding with
            {
                Evidence = new NodePort(NodeObserving.EvidenceSchema(reading.Node.Kind), $"{Record}#{report.Ask}@{report.Revision}", null, reading.Binding.Id),
            },
        };
    }
}
