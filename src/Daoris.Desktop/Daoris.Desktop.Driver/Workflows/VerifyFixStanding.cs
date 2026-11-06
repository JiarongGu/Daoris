namespace Daoris.Driver;

/// <summary>
/// The person's standing say-so on a failed verification (the pipeline design §4.5, <c>failed: ask</c>): where the check a run is bound to says
/// <c>ask</c>, Daoris asks for the fix itself at a whole look, as the person's <i>Ask for a fix…</i> would, with no press. Only a version the
/// person saved can say it: an agent's lowering of <c>failed</c> is kept as a proposal (<see cref="InvolvementRoutes"/>). A check that says
/// <c>wait</c>, or nothing, waits for the press; <c>stop</c> asks nothing.
/// </summary>
/// <remarks>
/// The failure stays held for the person, whose <i>Run again</i> and <i>Not now</i> still stand. The look is what keeps it to one ask: the
/// workspace's asks are read once for every failure it asks about, and an ask already made, or a failure already on an open ask's channel, is
/// found and not made again, so a restart between a send and its answer makes none twice.
/// </remarks>
public static class VerifyFixStanding
{
    /// <summary>The <c>failed</c> that stands for the person's press.</summary>
    public const string Ask = "ask";

    /// <summary>A failure the look asks about: the run's unit, its check and the attempt that failed.</summary>
    public sealed record Owed(string Unit, string Node, int Attempt);

    /// <summary>The failures held for the person whose check says <c>failed: ask</c>, in run order.</summary>
    public static IReadOnlyList<Owed> Of(NodeRecordFiles records)
    {
        var owed = new List<Owed>();
        foreach (var run in VerifyRuns.All(records.Home))
        {
            var unit = RunUnits.IdOf(run.Run);
            if (records.Read(unit) is not { } record) continue;
            foreach (var attempt in record.Attempts.Where(each => each.Plugin == NodeKinds.Check && WorkPerformer.KeptFailure(record, each)))
            {
                if (VerifyFixAsks.NodeOf(records.Home, run, attempt.Node) is { } check && NodeOptions.Text(check, "failed") == Ask)
                {
                    owed.Add(new Owed(unit, attempt.Node, attempt.Attempt));
                }
            }
        }

        return owed;
    }

    /// <summary>
    /// One whole look's asks: each failure <see cref="Of"/> names asked for as <see cref="VerifyFixAsks.AskAsync"/> asks, the workspace's asks read
    /// once. Returns what it published or posted, as the look's report says it; a failure already asked says nothing.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AskAsync(ServiceClient service, NodeRecordFiles records, CancellationToken ct)
    {
        var owed = Of(records);
        if (owed.Count == 0) return [];
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        var said = new List<string>();
        foreach (var each in owed)
        {
            var pressed = await VerifyFixAsks.AskAsync(service, records, each.Unit, each.Node, each.Attempt, ct, asks).ConfigureAwait(false);
            if (!pressed.Found) said.Add($"work  {each.Unit} {each.Node}: {pressed.Message}.");
        }

        return said;
    }
}
