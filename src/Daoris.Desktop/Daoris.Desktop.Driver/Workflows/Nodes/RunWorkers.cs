namespace Daoris.Driver;

/// <summary>The sessions that worked a run, as the pass last governed it.</summary>
internal static class RunWorkers
{
    /// <summary>Those serving the run's work quests and those that worked its branch, with their helpers; null where the pass has not governed the run.</summary>
    public static IReadOnlyList<string>? Of(IReadOnlyDictionary<string, GovernedUnit>? governed, string run, string? home = null)
    {
        var named = run.Trim().TrimStart('#');
        if (governed?.Values.FirstOrDefault(each => each.Run == named)?.Scope is not { } scope) return null;
        IEnumerable<string> makers = scope.Self.Executors.Concat(scope.Quest?.Executors ?? []).Concat(scope.Branch?.Executors ?? []);
        return home is null ? [.. makers.Distinct(StringComparer.OrdinalIgnoreCase)] : WithHelpers(home, makers);
    }

    /// <summary>The makers and each helper session one started, and theirs in turn, from the home's helper asks; a continuation keeps its session's id.</summary>
    public static IReadOnlyList<string> WithHelpers(string home, IEnumerable<string> makers)
    {
        var all = makers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var files = new HelperFiles(home);
        var asks = files.Asks();
        var taken = new HashSet<string>(all, StringComparer.OrdinalIgnoreCase);
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var ask in asks.Where(ask => taken.Contains(ask.Asker)))
            {
                if (files.ReadState(ask.Id)?.Helper is { Length: > 0 } helper && taken.Add(helper))
                {
                    all.Add(helper);
                    grew = true;
                }
            }
        }

        return all;
    }
}
