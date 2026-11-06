namespace Daoris.Driver;

/// <summary>
/// The <c>work.requests</c> node read from the quests' records (INTEGRATE1o part o2; the integration design §17.3): the quests a run's sessions
/// published here, which the work holds on. Pure: the caller hands in the quests, and nothing is asked or written here.
/// </summary>
/// <remarks>
/// <para><b>Reached once each is done.</b> Its reach authority is the quests' records (<see cref="NodeSource.Requests"/>): every quest a session of the
/// run published, done, as the exchange keeps it. A run whose sessions published none has nothing to wait on, so it is reached.</para>
/// <para><b>A decline holds, it does not fail.</b> An open or taken quest leaves the node active and pending; so does a declined one, and the node has no
/// failure authority, so only the person's pass over it lets the flow go on (<see cref="RunNodeWords.Word"/>). Its view says it waits on them
/// (<see cref="WorkRequests.Declined"/>). Quests not read wait, never reached on a guess.</para>
/// </remarks>
[NodeKind(NodeKinds.WorkRequests, NodeGroup.Checks, NodeKinds.Opinion)]
public sealed class WorkRequestsNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => Observe(reading.Unit, reading.Node, reading.Binding, reading.Requests);

    public override bool EndsByTheLine => true;

    /// <summary>
    /// INTEGRATE1o (§17.3, C28): reached by the quests' records, each the run's sessions published here done. A decline holds it active and waiting
    /// on the person, so nothing fails it; the person's pass with words is the run's skip, and no record passes it over.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.WorkRequests, NodeReach.Observed, NodeSource.Requests, null, false, PersonSkips: true, true, true);

    /// <summary>INTEGRATE1o (§17.6 point 2): it reads the quests the run's sessions published here, so its subject is the run itself.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    /// <summary>A graph-only kind, kept by its node's id for the comparison's graph part (<see cref="Involvement.Lowers"/>).</summary>
    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.WorkRequests };

    public override string? Title => "The work's requests";

    public override NodeLook Look => NodeLook.Holds;

    /// <summary>Where the quests' records are named in a reach's evidence: the exchange's own, by quest id.</summary>
    public const string Record = "quests";

    /// <summary>
    /// The quests among <paramref name="every"/> that a session of <paramref name="sessions"/> published, in their order: the ones
    /// <see cref="AskWork"/> joins to the work as <c>published</c>, those of <paramref name="work"/> (the run's own quests) left out.
    /// </summary>
    public static IReadOnlyList<QuestView> Published(IEnumerable<QuestView> every, IEnumerable<string> sessions, IReadOnlySet<string>? work = null)
    {
        var held = sessions.ToList();
        return [.. every.Where(quest => quest.PublishedBy is { Length: > 0 } by && held.Any(session => AskWork.Spells(session, by))
            && work?.Contains(quest.Id) != true).DistinctBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The first quest among <paramref name="published"/> the receiver declined, or null: what holds the node and waits on the person.</summary>
    public static QuestView? Declined(IEnumerable<QuestView> published) => published.FirstOrDefault(quest => quest.Status == "Declined");

    /// <summary>What the quests say of <paramref name="node"/> for <paramref name="unit"/>'s run: reached once each is done, or none.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, IReadOnlyList<QuestView>? published)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        if (node.Kind != NodeKinds.WorkRequests) return Seen(null, "this kind is not the work's requests") with { Refused = NodeRefusal.NotObserved };
        if (binding.Observes != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another subject, node or version") with { Refused = NodeRefusal.NotBound };
        }

        if (published is null) return Seen(null, "the quests the run's sessions published were not read");
        if (Declined(published) is { } declined)
        {
            return Seen(null, $"quest #{declined.Id} to `{declined.To}` was declined: yours to pass over");
        }

        if (published.Where(quest => quest.Status != "Done").ToList() is { Count: > 0 } open)
        {
            return Seen(null, $"waits on {string.Join(", ", open.Select(quest => $"quest #{quest.Id} to `{quest.To}` ({quest.Status.ToLowerInvariant()})"))}");
        }

        var named = published.Count == 0 ? "none" : string.Join(",", published.Select(quest => quest.Id).Order(StringComparer.Ordinal));
        return Seen(NodeStates.Reached, published.Count == 0 ? "its sessions published no quest here" : $"each quest its sessions published is done ({named})") with
        {
            Source = NodeSource.Requests,
            Acceptance = binding with { Evidence = new NodePort(BuiltInNodes.EvidenceSchema(node.Kind), $"{Record}#{named}", null, binding.Id) },
        };
    }
}
