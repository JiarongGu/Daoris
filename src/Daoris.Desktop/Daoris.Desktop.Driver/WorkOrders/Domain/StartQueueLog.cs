namespace Daoris.Driver;

/// <summary>
/// The start queue's waits as the machine log says them: every quest the plan queues behind another's start, with the head it waits on and
/// the head's own verdict. A queue that stands (a head planned at each look that begins nothing) reads apart from one that moves, where
/// the sentence the page shows alone named the head. Facts and codes only: no one's words.
/// </summary>
internal static class StartQueueLog
{
    /// <summary>The machine log's event, one line per quest per change of what it waits on.</summary>
    public const string Event = "start.queued";

    private static readonly string[] Codes = [SittingFact.Queued, SittingFact.RepositoryOpening, SittingFact.TreeOpening, SittingFact.TreeThisLook];

    /// <summary>
    /// Each queued quest's id with what it waits on as a key (the code, the head and the head's verdict, which moves when the wait does) and
    /// the fields its line carries; a head that is a start still opening has no verdict in the plan and reads <c>opening</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Key, (string Key, object? Value)[] Fields)> Waits(IReadOnlyList<Consideration> plan)
    {
        var waits = new Dictionary<string, (string, (string, object?)[])>(StringComparer.OrdinalIgnoreCase);
        foreach (var queued in plan)
        {
            if (queued is not { Verdict: StartVerdict.RepositoryBusy, Said: { Code: { } code, Quest: { } head } } || !Codes.Contains(code)) continue;
            var ahead = plan.FirstOrDefault(each => string.Equals(each.Quest.Id, head, StringComparison.OrdinalIgnoreCase));
            var verdict = ahead is null ? "opening" : ahead.Verdict.ToString();
            waits[queued.Quest.Id] = ($"{code}|{head}|{verdict}",
            [
                ("quest", queued.Quest.Id), ("repository", queued.Quest.To), ("code", code), ("head", head), ("headVerdict", verdict),
                ("headGoesOn", ahead?.GoesOn == true), ("headSession", ahead?.Resumes?.Session),
            ]);
        }

        return waits;
    }
}
