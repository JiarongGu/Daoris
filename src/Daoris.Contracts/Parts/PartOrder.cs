using System.Globalization;

namespace Daoris.Contracts.Parts;

/// <summary>
/// The order a root runs its parts in: phase by phase in the phase enum's order, and within a phase each part after the parts
/// it names, ties broken by name, so the same marks always give the same order.
/// </summary>
public static class PartOrder
{
    /// <exception cref="PartOrderException">
    /// A name declared twice; phases of two enums; an <see cref="PartDeclaration.After"/> that names no part, or a part in a later
    /// phase; or a cycle, named by its members.
    /// </exception>
    public static IReadOnlyList<PartDeclaration> Of(IEnumerable<PartDeclaration> parts)
    {
        var all = parts.ToList();
        var twice = all.GroupBy(part => part.Name, StringComparer.Ordinal).FirstOrDefault(named => named.Count() > 1);
        if (twice is not null)
        {
            throw new PartOrderException(
                $"the part {twice.Key} is declared twice, by {string.Join(" and ", twice.Select(part => part.Type.Name))}");
        }

        var enums = all.Select(part => part.Phase.GetType()).Distinct().ToList();
        if (enums.Count > 1)
        {
            throw new PartOrderException(
                $"the parts' phases are of {string.Join(" and ", enums.Select(type => type.Name))}: a seam's parts share one phase enum");
        }

        var byName = all.ToDictionary(part => part.Name, StringComparer.Ordinal);
        foreach (var part in all)
        {
            foreach (var after in part.After)
            {
                if (!byName.TryGetValue(after, out var before))
                {
                    throw new PartOrderException($"the part {part.Name} comes after {after}, which is no part");
                }

                if (Rank(before) > Rank(part))
                {
                    throw new PartOrderException(
                        $"the part {part.Name} runs in {part.Phase} and comes after {after}, which runs later, in {before.Phase}");
                }
            }
        }

        var ordered = new List<PartDeclaration>(all.Count);
        foreach (var phase in all.GroupBy(Rank).OrderBy(group => group.Key)) ordered.AddRange(InPhase([.. phase]));
        return ordered;
    }

    private static long Rank(PartDeclaration part) => Convert.ToInt64(part.Phase, CultureInfo.InvariantCulture);

    private static List<PartDeclaration> InPhase(List<PartDeclaration> phase)
    {
        var byName = phase.ToDictionary(part => part.Name, StringComparer.Ordinal);

        // Only a part of this phase orders it: one of an earlier phase has run before any of these.
        var waiting = phase.ToDictionary(
            part => part.Name, part => part.After.Where(byName.ContainsKey).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var ready = new SortedSet<string>(waiting.Where(each => each.Value.Count == 0).Select(each => each.Key), StringComparer.Ordinal);
        var done = new List<PartDeclaration>(phase.Count);
        while (ready.Count > 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            waiting.Remove(next);
            done.Add(byName[next]);
            foreach (var (name, afters) in waiting)
            {
                if (afters.Remove(next) && afters.Count == 0) ready.Add(name);
            }
        }

        if (waiting.Count > 0)
        {
            throw new PartOrderException($"the parts {Cycle(waiting)} come after each other, so none of them can run first");
        }

        return done;
    }

    /// <summary>One cycle among the parts left waiting, from the first by name: every one of them still waits on another of them.</summary>
    private static string Cycle(Dictionary<string, HashSet<string>> waiting)
    {
        var path = new List<string>();
        var at = waiting.Keys.Min(StringComparer.Ordinal)!;
        while (!path.Contains(at))
        {
            path.Add(at);
            at = waiting[at].Min(StringComparer.Ordinal)!;
        }

        return string.Join(" → ", [.. path[path.IndexOf(at)..], at]);
    }
}
