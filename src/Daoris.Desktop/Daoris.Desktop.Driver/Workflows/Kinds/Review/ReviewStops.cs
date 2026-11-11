namespace Daoris.Driver;

/// <summary>What the review's Stop came to: the steps stopped, whether it stopped only what follows its Accept, and what stays, said.</summary>
/// <param name="Following">The review stands accepted, so only the steps after it are stopped.</param>
/// <param name="Stopped">The steps whose stop is kept, for the next pass to apply.</param>
/// <param name="Happened">The steps after the Accept that already came to their end, which stay as they are.</param>
public sealed record ReviewStopped(string Run, string Review, bool Following, IReadOnlyList<string> Stopped, IReadOnlyList<string> Happened, string Stays)
{
    /// <summary>Why nothing is stopped, a word door's refusal; null where the stop is kept.</summary>
    public string? Refused { get; init; }

    public bool Done => Refused is null;
}

/// <summary>
/// The review's <i>Stop…</i>: before its Accept it stops the run, every live step stopped as the person's with their words, as an abandon
/// stops a run's steps; after it, <i>Stop what follows…</i> stops each step after the review that has not come to its end, a step under way
/// where its door can stop it. Each says what stays by the kind of work the review read, and never promises an undo.
/// </summary>
public static class ReviewStops
{
    /// <summary>Keeps the stop of <paramref name="review"/> on <paramref name="run"/>: the run's, or what follows where the review is accepted.</summary>
    public static async Task<ReviewStopped> StopAsync(
        NodeRecordFiles records, IIntegrationDoor door, string run, string review, string? words, Func<DateTimeOffset> clock, CancellationToken ct = default)
    {
        run = run.Trim().TrimStart('#');
        if (new GraphRuns(records).Find(run) is not { } bound) return Refused(run, review, RunNodeWords.NotRun);
        if (bound.Ended is not null) return Refused(run, review, RunNodeWords.Ended);
        if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } record) return Refused(run, review, RunNodeWords.UnreadVersion);
        if (graph.Nodes.FirstOrDefault(node => node.Id == review) is not { } declared) return Refused(run, review, RunNodeWords.NotNode);
        if (declared.Kind != NodeKinds.Review) return Refused(run, review, RunNodeWords.NotReview);

        var status = WorkPerformer.StatusOf(record, review);
        var following = status.State == NodeStates.Reached;
        var after = following ? After(graph, review) : null;
        var steps = graph.Nodes.Where(node => after is null || after.Contains(node.Id)).ToList();
        var stopped = new List<string>();
        foreach (var node in steps.Where(node => NodeStates.Live.Contains(WorkPerformer.StatusOf(record, node.Id).State)))
        {
            var done = await RunNodeWords.WordAsync(records, door, run, node.Id, NodeWordActs.Stop, words, clock, ct).ConfigureAwait(false);
            if (done.Done) stopped.Add(node.Id);
        }

        var happened = steps.Where(node => following && WorkPerformer.StatusOf(record, node.Id).State == NodeStates.Reached).Select(node => node.Id).ToList();
        var kind = ReviewRecords.Read(records.Home, run, review, (following ? status.Acceptance?.Attempt : null) ?? status.Attempt)?.Kind;
        return new ReviewStopped(run, review, following, stopped, happened, Stays(kind, following));
    }

    /// <summary>The nodes a wire leads to from <paramref name="review"/>, at any distance: what follows its Accept.</summary>
    public static IReadOnlySet<string> After(NodeGraph graph, string review)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var next = new Queue<string>([review]);
        while (next.TryDequeue(out var from))
        {
            foreach (var wire in graph.Wires.Where(wire => wire.From?.Node == from && wire.To.Node is { } to && to != review))
            {
                if (found.Add(wire.To.Node!)) next.Enqueue(wire.To.Node!);
            }
        }

        return found;
    }

    /// <summary>What stays where the review read work of <paramref name="kind"/>: Daoris has no undo of the person's work.</summary>
    public static string Stays(string? kind, bool following) => (kind, following) switch
    {
        (_, true) => "What already happened stays as it is; nothing is undone.",
        (ReviewKinds.Branch, _) => "The branch and its tree stay as they are; Discard tree removes them.",
        (ReviewKinds.Checkout, _) => "The commits stay where they are in the checkout.",
        (ReviewKinds.Folder, _) => "The changes stay in the folder.",
        (ReviewKinds.Copy, _) => "Nothing more comes back from the copy; its tidy sets it aside.",
        (ReviewKinds.Plan, _) => "Nothing more is applied.",
        _ => "The answer stays as it was given.",
    };

    /// <summary>The sentence a door says for <paramref name="stopped"/>.</summary>
    public static string Said(ReviewStopped stopped) => stopped switch
    {
        { Refused: { } why } => $"workflow: `{stopped.Review}` on run {stopped.Run} was not stopped ({why}).",
        { Following: true, Stopped.Count: 0 } => $"workflow: nothing follows `{stopped.Review}` on run {stopped.Run} that has not ended. {stopped.Stays}",
        { Following: true } => $"workflow: what follows `{stopped.Review}` on run {stopped.Run} stops ({string.Join(", ", stopped.Stopped)})"
            + (stopped.Happened.Count > 0 ? $"; {string.Join(", ", stopped.Happened)} already happened" : "") + $". {stopped.Stays}",
        _ => $"workflow: run {stopped.Run} stops at its review ({string.Join(", ", stopped.Stopped)}). {stopped.Stays}",
    };

    private static ReviewStopped Refused(string run, string review, string why) => new(run, review, false, [], [], "") { Refused = why };
}
