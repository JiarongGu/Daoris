namespace Daoris.Driver;

/// <summary>
/// The receipts of an ask's parts, each read from the journal of its part's run (<see cref="StepActFiles"/>), the run of the quest the plan
/// gave it in its own repository; and the waits a part's run holds before it starts: the plan's <c>after</c> edges naming another part's
/// receipt (<see cref="PartWait"/>), each settled once that part's newest receipt of the fact is reached. No new graph: the plan's edge and
/// the parts' journals are all it reads. Reads only.
/// </summary>
public static class PartReceipts
{
    /// <summary>The part of <paramref name="report"/>'s plan whose quest is the run's (its id, or a quest of its chain); null for a run that is no part's.</summary>
    public static string? PartOf(PartsReport report, string run, IEnumerable<string> quests)
    {
        var own = quests.Append(run).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return report.Quests.Where(each => own.Contains(each.Value)).Select(each => each.Key).FirstOrDefault();
    }

    /// <summary>The run of <paramref name="part"/>, the quest the plan gave it, or null where it has none yet.</summary>
    public static string? RunOf(PartsReport report, string part) => report.Quests.GetValueOrDefault(part);

    /// <summary>The newest receipt of <paramref name="fact"/> journaled on <paramref name="part"/>'s run, or null.</summary>
    public static StepActRow? Newest(string home, PartsReport report, string part, string fact) =>
        RunOf(report, part) is { } run ? StepActFiles.Journal(home, RunUnits.IdOf(run)).LastOrDefault(row => row.Fact == fact) : null;

    /// <summary>What the run's part waits on before it starts: each wait its plan names whose part's newest receipt of the fact has not reached.</summary>
    public static IReadOnlyList<PartWait> Waiting(string home, string run, RunScope scope)
    {
        if (scope.Parts is not { } report || PartOf(report, run, scope.Quests.Select(quest => quest.Id)) is not { } part
            || !report.Waits.TryGetValue(part, out var waits))
        {
            return [];
        }

        return [.. waits.Where(wait => Newest(home, report, wait.On, wait.Fact) is not { State: StepReceipts.Reached })];
    }

    /// <summary>The waits as words: each part and the fact its receipt is to give.</summary>
    public static string Words(IEnumerable<PartWait> waits) => string.Join(", ", waits.Select(wait => $"`{wait.On}`'s `{wait.Fact}`"));
}
