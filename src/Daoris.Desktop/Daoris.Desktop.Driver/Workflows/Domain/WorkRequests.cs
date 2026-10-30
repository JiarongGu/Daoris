namespace Daoris.Driver;

/// <summary>
/// What a run's published quests and asked helpers say beside the node that holds on them (INTEGRATE1o part o2; the integration design §17.3): the one
/// reading the run's view and its <c>work.requests</c> share. Pure.
/// </summary>
public static class WorkRequests
{
    /// <summary>
    /// The code a <c>work.requests</c> node carries on the run's view while it holds on a declined quest (§17.3): the node stays active, never failed,
    /// and waits on the person, whose pass over it lets the flow go on. It is read from the quests' records at each look, not stored on the node.
    /// </summary>
    public const string DeclinedCode = "declined";

    /// <summary>Whether any request the work's sessions made as a quest was declined.</summary>
    public static bool Declined(IEnumerable<WorkflowRunRequest> requests) =>
        requests.Any(request => request is { Kind: WorkflowRunRequestKinds.Quest, State: "declined" });

    /// <summary>
    /// The requests as a run's view draws them (§17.3): each quest a session of the work <paramref name="published"/> (its repository, its state, the
    /// close's note, and whether a quest of the <paramref name="work"/> waits on it, <see cref="QuestView.Awaits"/>, until it closes), then each of
    /// the <paramref name="helpers"/> a session of the work asked, as a request to an agent.
    /// </summary>
    /// <param name="published">The quests the work's sessions published (<see cref="WorkRequestsNode.Published"/>).</param>
    /// <param name="work">The run's own work quests here: the one a session waits with is the one naming its question.</param>
    /// <param name="helpers">The helpers' records, each naming the session of the work it serves.</param>
    public static IReadOnlyList<WorkflowRunRequest> Draw(IEnumerable<QuestView> published, IEnumerable<QuestView> work, IEnumerable<TracedSession> helpers)
    {
        var waiting = work.Select(quest => quest.Awaits).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var drawn = published.Select(quest => new WorkflowRunRequest(WorkflowRunRequestKinds.Quest, quest.Id, quest.To, quest.Status.ToLowerInvariant())
        {
            Session = quest.PublishedBy,
            Awaits = quest.Status is not ("Done" or "Declined") && waiting.Contains(quest.Id),
            Words = quest.Note is { Length: > 0 } note ? note : null,
            At = quest.Updated,
        }).ToList();
        drawn.AddRange(helpers.Select(helper => new WorkflowRunRequest(WorkflowRunRequestKinds.Agent, helper.Id, helper.Adapter ?? "", HelperState(helper.State))
        {
            Session = helper.Serves,
            Words = helper.Note is { Length: > 0 } note ? note : null,
            At = helper.Updated ?? helper.Created,
        }));
        return drawn;
    }

    /// <summary>A helper's session state as a request's: working until it ends, then how it ended.</summary>
    private static string HelperState(string state) => state switch
    {
        "completed" or "stood-down" or "declined" => "done",
        "failed" => "failed",
        "stopped" => "stopped",
        _ => "working",
    };
}
