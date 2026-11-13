using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// The pure judge a plan passes before it is kept: its shape is refused, and what is only wrong with it is found, never refused: a cycle, an
/// edge to a part no plan holds, a claim on a record or a generated path (stripped, and the integrator named as its writer), and two
/// parallel parts claiming one path (ordered, and said). A plan with flags is saved with them, and the supervisor reads them.
/// </summary>
public static partial class TeamPlanJudge
{
    /// <summary>The longest a part's id may be.</summary>
    public const int LongestId = 60;

    /// <summary>Judges one plan against the parts and edges the ask's other plans already hold.</summary>
    /// <param name="parts">The plan's parts, in the order written.</param>
    /// <param name="edges">The plan's edges.</param>
    /// <param name="others">The parts of the ask's other plans, at their latest revisions.</param>
    /// <param name="otherEdges">The edges of those plans.</param>
    /// <param name="policy">The paths no worker changes, by repository.</param>
    public static TeamJudged Judge(
        IReadOnlyList<TeamPart> parts, IReadOnlyList<TeamEdge> edges, IReadOnlyList<TeamPart> others, IReadOnlyList<TeamEdge> otherEdges,
        Func<string, TeamPathPolicy> policy)
    {
        if (Refuse(parts, edges, others) is { } refusal) return new(refusal, parts, edges, []);

        var flags = new List<TeamFlag>();
        var stripped = parts.Select(part => Strip(part, policy(part.Repository), flags)).ToList();
        var known = stripped.Select(part => part.Id).Concat(others.Select(part => part.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges.Where(edge => !known.Contains(edge.On) || !known.Contains(edge.Part)))
        {
            var missing = known.Contains(edge.On) ? edge.Part : edge.On;
            flags.Add(new(TeamFlags.MissingReference, edge.Part, $"Edge `{edge.Part}` {edge.Kind} `{edge.On}` names `{missing}`, which no plan of this ask holds."));
        }

        var graph = edges.Concat(otherEdges).Where(edge => known.Contains(edge.On) && known.Contains(edge.Part)).ToList();
        if (Cycle(graph) is { } cycle)
        {
            flags.Add(new(TeamFlags.Cycle, cycle[0], $"The edges run in a circle: {string.Join(" -> ", cycle)}. Nothing can start first."));
        }

        var added = new List<TeamEdge>();
        for (var later = 1; later < stripped.Count; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                var (first, second) = (stripped[earlier], stripped[later]);
                if (!string.Equals(first.Repository, second.Repository, StringComparison.OrdinalIgnoreCase)
                    || Related(graph.Concat(added).ToList(), first.Id, second.Id)
                    || Shared(first, second) is not { } path)
                {
                    continue;
                }

                var order = new TeamEdge(second.Id, first.Id, TeamEdge.Orders, null, $"Both claim `{path}`; either order works, one at a time.");
                added.Add(order);
                flags.Add(new(TeamFlags.ClaimOverlap, second.Id, $"`{first.Id}` and `{second.Id}` both claim `{path}`: `{second.Id}` lands after `{first.Id}`."));
            }
        }

        return new(null, stripped, [.. edges, .. added], flags);
    }

    /// <summary>Why the plan has no shape to keep, or null.</summary>
    private static string? Refuse(IReadOnlyList<TeamPart> parts, IReadOnlyList<TeamEdge> edges, IReadOnlyList<TeamPart> others)
    {
        if (parts.Count == 0) return "A plan holds at least one part: one part is a valid plan.";
        var seen = others.Select(part => part.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (part.Id is not { Length: > 0 and <= LongestId } || !IdShape().IsMatch(part.Id))
            {
                return $"A part's id is letters, digits and `. _ : -`, at most {LongestId} characters: `{part.Id}` is not one.";
            }

            if (!seen.Add(part.Id)) return $"Two parts of this ask carry the id `{part.Id}`: a part is published under its id, so each is its own.";
            if (string.IsNullOrWhiteSpace(part.Repository) || string.IsNullOrWhiteSpace(part.Title) || string.IsNullOrWhiteSpace(part.Outcome))
            {
                return $"Part `{part.Id}` needs its repository, its title and its outcome (what a person sees working).";
            }

            var (_, unfit) = QuestPaths.Judge([.. part.Claims]);
            if (unfit is not null) return $"Part `{part.Id}`: {unfit}";
        }

        foreach (var edge in edges)
        {
            if (edge.Kind is not (TeamEdge.After or TeamEdge.Orders)) return $"An edge is `{TeamEdge.After}` or `{TeamEdge.Orders}`: `{edge.Kind}` is neither.";
            if (edge.Milestone is { } milestone && !TeamEdge.Milestones.Contains(milestone)) return $"A milestone is oriented, submitted or landed: `{milestone}` is none.";
            if (string.IsNullOrWhiteSpace(edge.Why)) return $"Edge `{edge.Part}` {edge.Kind} `{edge.On}` needs its reason.";
        }

        return null;
    }

    /// <summary>The part with every claim inside a record or a generated output taken out, each said as a flag.</summary>
    private static TeamPart Strip(TeamPart part, TeamPathPolicy policy, List<TeamFlag> flags)
    {
        var generated = policy.Generated.Concat(part.Pack?.Generated ?? []).ToList();
        var kept = new List<string>();
        foreach (var claim in part.Claims)
        {
            if (policy.Records.FirstOrDefault(record => ClaimPaths.Within(claim, record)) is { } record)
            {
                flags.Add(new(TeamFlags.ClaimRecord, part.Id, $"`{part.Id}` claims `{claim}`, a record: the integrator writes `{record}` from the part's intents, so the claim is dropped."));
            }
            else if (generated.FirstOrDefault(output => ClaimPaths.Within(claim, output)) is { } output)
            {
                flags.Add(new(TeamFlags.ClaimGenerated, part.Id, $"`{part.Id}` claims `{claim}`, generated output `{output}`: the integrator regenerates it, so the claim is dropped."));
            }
            else
            {
                kept.Add(claim);
            }
        }

        return part with { Claims = kept };
    }

    private static string? Shared(TeamPart a, TeamPart b) =>
        a.Claims.SelectMany(left => b.Claims.Where(right => ClaimPaths.Overlap(left, right)).Select(right => left.Length <= right.Length ? left : right))
            .FirstOrDefault();

    /// <summary>Whether an order already runs from one part to the other, through any edges, in either direction.</summary>
    private static bool Related(IReadOnlyList<TeamEdge> edges, string a, string b) => Reaches(edges, a, b) || Reaches(edges, b, a);

    private static bool Reaches(IReadOnlyList<TeamEdge> edges, string from, string to)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { from };
        var queue = new Queue<string>([from]);
        while (queue.TryDequeue(out var at))
        {
            foreach (var edge in edges.Where(edge => edge.Part == at))
            {
                if (edge.On == to) return true;
                if (seen.Add(edge.On)) queue.Enqueue(edge.On);
            }
        }

        return false;
    }

    /// <summary>The ids of one circle in the edges, first id repeated at the end; null where there is none.</summary>
    private static List<string>? Cycle(IReadOnlyList<TeamEdge> edges)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in edges.Select(edge => edge.Part).Distinct(StringComparer.Ordinal))
        {
            if (Walk(edges, start, [], done) is { } found) return found;
        }

        return null;
    }

    private static List<string>? Walk(IReadOnlyList<TeamEdge> edges, string at, List<string> path, HashSet<string> done)
    {
        if (path.Contains(at, StringComparer.Ordinal)) return [.. path.SkipWhile(id => id != at), at];
        if (done.Contains(at)) return null;
        path.Add(at);
        foreach (var next in edges.Where(edge => edge.Part == at).Select(edge => edge.On).Distinct(StringComparer.Ordinal))
        {
            if (Walk(edges, next, path, done) is { } found) return found;
        }

        path.RemoveAt(path.Count - 1);
        done.Add(at);
        return null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdShape();
}
