namespace Daoris.Driver;

/// <summary>
/// The milestone in a terminal's words (FABRIC-MILESTONE1a, D50): `daoris-driver trees state` and `work state`. In the library, so
/// a test holds the sentences; the page's own words are MILESTONE1b's.
/// </summary>
public static class WorkWords
{
    /// <summary>One unit's milestone: its end, its result, its cleanup and its recovery, one line each.</summary>
    public static IEnumerable<string> Milestone(WorkMilestone unit, bool entries = false)
    {
        yield return $"{unit.Unit}  {unit.Repository}" + (unit.Branch is { } branch ? $"  {branch}" : "");
        yield return unit.End.State switch
        {
            "ended" => $"  end       reached: {unit.End.Said}",
            "handed" => $"  end       not reached: handed on; {unit.End.Said}",
            "unknown" => $"  end       unknown: {unit.End.Said}",
            WorkEndState.Dropped => $"  end       dropped: {unit.End.Said}",
            _ => $"  end       not reached: {unit.End.Said}",
        };
        yield return unit.Result.Count == 0
            ? "  result    none kept yet"
            : $"  result    kept: {string.Join("; ", unit.Result.Select(entry => $"`{entry.Resource}` ({entry.Detail})"))}";
        if (unit.Cleanup.State == "completed")
        {
            yield return unit.Cleanup.Gone.Count == 0
                ? "  cleanup   completed: nothing was left to clean"
                : $"  cleanup   completed: removed {string.Join(", ", unit.Cleanup.Gone.Select(Gone))}";
        }
        else
        {
            yield return "  cleanup   pending:";
            foreach (var wait in unit.Cleanup.Waits) yield return $"              {(wait.Disposition == InventoryDisposition.Blocked ? "blocked " : "waits   ")} {wait.Kind} `{wait.Resource}`: {wait.Detail}";
            if (unit.Cleanup.Gone.Count > 0) yield return $"              removed  {string.Join(", ", unit.Cleanup.Gone.Select(Gone))}";
        }

        if (unit.Recovery.Count == 0)
        {
            yield return "  recovery  none retained";
        }
        else
        {
            foreach (var each in unit.Recovery)
            {
                yield return $"  recovery  {each.Kind} `{each.Resource}`: {each.Detail}";
            }
        }

        if (!entries) yield break;
        foreach (var each in unit.Entries)
        {
            yield return $"    {each.Role,-9} {each.Disposition,-8} {each.Kind} `{each.Resource}`: {each.Detail}" + (each.Proof is { } proof ? $" [{proof}]" : "");
        }
    }

    private static string Gone(InventoryEntry entry) => $"{entry.Kind} `{entry.Resource}`" + (entry.Proof is { } proof ? $" ({proof})" : "");

    /// <summary>The recoveries no unit names (CLEANUP1, <see cref="WorkInventory.Recoveries"/>), each with its rule; nothing where there are none.</summary>
    public static IEnumerable<string> Recovery(IReadOnlyList<InventoryEntry> recoveries)
    {
        if (recoveries.Count == 0) yield break;
        yield return "recovery kept, which no unit names any more (each by its own rule, waiting on nobody):";
        foreach (var each in recoveries) yield return $"  {each.Kind} `{each.Resource}`: {each.Detail}";
    }

    /// <summary>Every unit's milestone, a blank line between; the sentence for none.</summary>
    public static IEnumerable<string> All(IReadOnlyList<WorkMilestone> units, bool entries = false)
    {
        if (units.Count == 0)
        {
            yield return "work: no unit of work is recorded on this machine.";
            yield break;
        }

        var first = true;
        foreach (var unit in units)
        {
            if (!first) yield return "";
            first = false;
            foreach (var line in Milestone(unit, entries)) yield return line;
        }
    }
}
