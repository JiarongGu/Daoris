namespace Daoris.Knowledge;

/// <summary>
/// The pure judge of a plan's release order. The edges it makes itself come from the declarations alone: one deployable provides a resource
/// another consumes as <c>mirror</c> or <c>reads</c>. The registry's <c>uses</c> and a <c>calls</c> consumption name a neighbour and give no
/// order. An edge a writer gives (a stage condition, a contract, a code read) stands only on the evidence it cites; without it the edge is an
/// order somebody gave, source <c>policy</c>. A cycle is a question, never a refusal. An edge is stale once its evidence's blob moves.
/// </summary>
public static class DeliveryGraph
{
    /// <summary>Judges the deployables as the lead read them, the edges it wrote and the questions it already asks.</summary>
    /// <param name="waivers">The orders the person waived: kept and marked, they no longer break a circle open or go stale.</param>
    public static DeliveryJudged Judge(
        IReadOnlyList<DeliveryDeployable> deployables, IReadOnlyList<DeliveryEdge> written, IReadOnlyList<DeliveryQuestion> asked,
        IReadOnlyList<DeliveryWaiver>? waivers = null)
    {
        if (Refuse(deployables, written) is { } refusal) return new(refusal, [], [], []);

        var edges = new List<DeliveryEdge>();
        void Add(DeliveryEdge edge)
        {
            if (!edges.Any(each => each.Deployable == edge.Deployable && each.On == edge.On && each.Source == edge.Source)) edges.Add(edge);
        }

        foreach (var edge in Declared(deployables)) Add(edge);
        foreach (var edge in written) Add(Settled(edge));
        for (var at = 0; at < edges.Count; at++)
        {
            var waiver = waivers?.LastOrDefault(each => each.Deployable == edges[at].Deployable && each.On == edges[at].On);
            edges[at] = edges[at] with { Waived = waiver?.Reason };
        }

        var flags = new List<TeamFlag>();
        var questions = new List<DeliveryQuestion>(asked);
        var known = deployables.Select(each => each.Ref).ToHashSet();
        foreach (var edge in edges.Where(edge => !known.Contains(edge.Deployable) || !known.Contains(edge.On)))
        {
            var missing = known.Contains(edge.On) ? edge.Deployable : edge.On;
            flags.Add(new(TeamFlags.MissingReference, null, $"{MissingPrefix} `{edge.Deployable}` after `{edge.On}` names `{missing}`, which no deployable of this plan declares."));
        }

        if (Cycle([.. edges.Where(edge => edge.Waived is null && known.Contains(edge.Deployable) && known.Contains(edge.On))]) is { } cycle)
        {
            var spelt = string.Join(" -> ", cycle.Select(each => each.ToString()));
            flags.Add(new(TeamFlags.Cycle, null, $"{CyclePrefix}: {spelt}. Nothing can be released first."));
            questions.Add(new(CycleKey(cycle), DeliveryQuestion.Cycle, $"{CyclePrefix}: {spelt}. Break it with a compatibility change or a staged rollout.")
            {
                Yes = "The plan is revised with the order you choose.",
                Blocks = "Every release in the cycle.",
                Default = "None: no deployable in the cycle is released until it is settled.",
            });
        }

        return new(null, edges, Distinct(questions), flags);
    }

    private const string CyclePrefix = "The release orders form a cycle", MissingPrefix = "Release order";

    /// <summary>Whether a flag is one this judge raised, so a re-judge replaces it and leaves the part judge's alone.</summary>
    public static bool Raised(TeamFlag flag) =>
        flag.Part is null && (flag.Text.StartsWith(CyclePrefix, StringComparison.Ordinal) || flag.Text.StartsWith(MissingPrefix, StringComparison.Ordinal));

    /// <summary>The key a circle is asked under: the same however it is walked and from wherever it starts.</summary>
    public static string CycleKey(IEnumerable<DeliveryRef> circle) =>
        "cycle:" + string.Join('|', circle.Select(each => each.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>The edges whose evidence now reads as another blob. Evidence <paramref name="seen"/> does not hold is not judged.</summary>
    /// <param name="seen">The blob each <see cref="DeliveryEvidence.Key"/> holds now, for what the reader looked at.</param>
    public static IReadOnlyList<DeliveryEdge> Stale(IReadOnlyList<DeliveryEdge> edges, IReadOnlyDictionary<string, string> seen) =>
        [.. edges.Where(edge => edge.Waived is null && edge.Evidence is { } evidence && seen.TryGetValue(evidence.Key, out var now)
            && !string.Equals(now, evidence.Blob, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The person's queue: what the plan asks, then one question for each stale edge, each key once, none the person answered.</summary>
    public static IReadOnlyList<DeliveryQuestion> Queue(
        IReadOnlyList<DeliveryQuestion> asked, IReadOnlyList<DeliveryEdge> edges, IReadOnlyDictionary<string, string> seen,
        IReadOnlyList<DeliveryAnswer>? answers = null) =>
        [.. Distinct([.. asked, .. Stale(edges, seen).Select(edge => StaleQuestion(edge, seen[edge.Evidence!.Key]))])
            .Where(question => answers?.Any(answer => answer.Key == question.Key) != true)];

    /// <summary>What a plan still asks the person: <see cref="Queue"/> over the plan's own questions, orders and answers.</summary>
    public static IReadOnlyList<DeliveryQuestion> Open(TeamPlan plan, IReadOnlyDictionary<string, string> seen) =>
        Queue(plan.Questions, plan.Releases, seen, plan.Answers);

    // The key holds the blob the evidence moved to, so a second move asks again.
    private static DeliveryQuestion StaleQuestion(DeliveryEdge edge, string now) => new(
        $"stale:{edge.Deployable}>{edge.On}:{edge.Source}@{now}", DeliveryQuestion.Stale,
        $"`{edge.Deployable}` is released after `{edge.On}` because {edge.Why} What that read ({edge.Evidence!.File}) has changed since.")
    {
        Yes = "The order stands, and the lead reads it again.",
        Blocks = $"The release of `{edge.Deployable}`.",
        Default = "The order stays, marked stale.",
    };

    private static List<DeliveryQuestion> Distinct(IEnumerable<DeliveryQuestion> questions) =>
        [.. questions.GroupBy(question => question.Key, StringComparer.Ordinal).Select(group => group.First())];

    private static IEnumerable<DeliveryEdge> Declared(IReadOnlyList<DeliveryDeployable> deployables)
    {
        foreach (var consumer in deployables)
        {
            foreach (var use in consumer.Consumes.Where(use => use.Mode is DeliveryUse.Mirror or DeliveryUse.Reads))
            {
                foreach (var provider in deployables.Where(each => each.Ref != consumer.Ref))
                {
                    foreach (var provided in provider.Provides.Where(each => each.Resource == use.Resource))
                    {
                        var same = provider.Ref.Repository == consumer.Ref.Repository ? ", in one repository" : "";
                        yield return new(consumer.Ref, provider.Ref,
                            $"`{consumer.Ref.Id}` {use.Mode} `{use.Resource}`, which `{provider.Ref.Id}` {provided.Mode}{same}.",
                            DeliveryEdge.Declaration, consumer.Declared);
                    }
                }
            }
        }
    }

    /// <summary>The edge as it stands: one that needs evidence and cites none is an order somebody gave.</summary>
    private static DeliveryEdge Settled(DeliveryEdge edge)
    {
        var source = string.IsNullOrWhiteSpace(edge.Source) ? DeliveryEdge.Policy : edge.Source;
        var cited = edge.Evidence is { File.Length: > 0, Blob.Length: > 0 };
        if (DeliveryEdge.Evidenced.Contains(source) && !cited) source = DeliveryEdge.Policy;
        return source is DeliveryEdge.Policy or DeliveryEdge.Person ? edge with { Source = source, Evidence = null } : edge with { Source = source };
    }

    private static string? Refuse(IReadOnlyList<DeliveryDeployable> deployables, IReadOnlyList<DeliveryEdge> written)
    {
        var seen = new HashSet<DeliveryRef>();
        foreach (var each in deployables)
        {
            if (string.IsNullOrWhiteSpace(each.Ref.Repository) || string.IsNullOrWhiteSpace(each.Ref.Id))
            {
                return "A deployable needs its repository and the id its declaration gives it.";
            }

            if (!seen.Add(each.Ref)) return $"Two deployables carry the id `{each.Ref.Id}` in `{each.Ref.Repository}`: an id is unique in its repository.";
        }

        foreach (var edge in written)
        {
            if (!string.IsNullOrWhiteSpace(edge.Source) && !DeliveryEdge.Written.Contains(edge.Source))
            {
                return $"A release order's source is one of {string.Join(", ", DeliveryEdge.Written)}: `{edge.Source}` is none (declarations are read, not written).";
            }

            if (string.IsNullOrWhiteSpace(edge.Why)) return $"Release order `{edge.Deployable}` after `{edge.On}` needs its reason.";
        }

        return null;
    }

    /// <summary>One circle in the orders, its first deployable repeated at the end; null where there is none.</summary>
    private static List<DeliveryRef>? Cycle(IReadOnlyList<DeliveryEdge> edges)
    {
        var done = new HashSet<DeliveryRef>();
        foreach (var start in edges.Select(edge => edge.Deployable).Distinct())
        {
            if (Walk(edges, start, [], done) is { } found) return found;
        }

        return null;
    }

    private static List<DeliveryRef>? Walk(IReadOnlyList<DeliveryEdge> edges, DeliveryRef at, List<DeliveryRef> path, HashSet<DeliveryRef> done)
    {
        if (path.Contains(at)) return [.. path.SkipWhile(each => each != at), at];
        if (done.Contains(at)) return null;
        path.Add(at);
        foreach (var next in edges.Where(edge => edge.Deployable == at).Select(edge => edge.On).Distinct())
        {
            if (Walk(edges, next, path, done) is { } found) return found;
        }

        path.RemoveAt(path.Count - 1);
        done.Add(at);
        return null;
    }
}
