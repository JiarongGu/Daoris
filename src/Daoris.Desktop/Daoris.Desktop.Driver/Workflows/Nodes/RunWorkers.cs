namespace Daoris.Driver;

/// <summary>The sessions that worked a run, as the pass last governed it.</summary>
internal static class RunWorkers
{
    /// <summary>Those serving the run's work quests and those that worked its branch; null where the pass has not governed the run.</summary>
    public static IReadOnlyList<string>? Of(IReadOnlyDictionary<string, GovernedUnit>? governed, string run)
    {
        var named = run.Trim().TrimStart('#');
        return governed?.Values.FirstOrDefault(each => each.Run == named)?.Scope is { } scope
            ? [.. scope.Self.Executors.Concat(scope.Quest?.Executors ?? []).Concat(scope.Branch?.Executors ?? []).Distinct(StringComparer.Ordinal)]
            : null;
    }
}
