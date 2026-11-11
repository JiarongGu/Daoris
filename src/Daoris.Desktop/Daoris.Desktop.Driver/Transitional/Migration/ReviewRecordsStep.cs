namespace Daoris.Driver;

/// <summary>
/// The runs in flight when a review began binding its record. A branch's review, and a branchless one withheld before, are given their record
/// by the pass itself as their attempt stands (<see cref="ReviewRecords.KeepActive"/>), so this step converts only the words that name none
/// (<see cref="ReviewLegacy"/>): a person's Accept is kept as legacy where the run's pinned graph has nothing after its review, that being all it
/// ever allowed, and otherwise dropped, said on the run, so the review waits for a word on its record; a supervisor's is dropped stale either
/// way. No pinned graph is edited.
/// </summary>
public sealed class ReviewRecordsStep : IHomeMigrationStep
{
    public string Name => "review-records";

    public void Run(HomeMigrationRun run)
    {
        if (!Directory.Exists(WorkflowRunBindings.FolderOf(run.Home))) return;
        var records = new NodeRecordFiles(run.Home, () => true);
        var now = DateTimeOffset.UtcNow;
        foreach (var bound in new GraphRuns(records).Read().Where(each => each.Ended is null && each.Line is null))
        {
            if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } record) continue;
            var reviews = graph.Nodes.Where(node => node.Kind == NodeKinds.Review).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            var legacy = record.Pending.Where(word => reviews.Contains(word.Node) && ReviewLegacy.Unscoped(word, WorkPerformer.StatusOf(record, word.Node))).ToList();
            if (legacy.Count == 0) continue;

            var pending = new List<NodeWord>();
            var said = new List<NodeHistory>();
            foreach (var word in record.Pending)
            {
                if (!legacy.Contains(word)) pending.Add(word);
                else if (word.By is null && NothingAfter(graph, word.Node))
                {
                    pending.Add(word with { Words = ReviewLegacy.Receipt(word.At) });
                    run.Count(Name, "legacy-kept");
                }
                else
                {
                    var why = word.By is null ? ReviewLegacy.Dropped : ReviewLegacy.Stale;
                    said.Add(new NodeHistory(now, why, WorkPerformer.StatusOf(record, word.Node)));
                    run.Count(Name, word.By is null ? "legacy-dropped" : "legacy-stale");
                }
            }

            var path = NodeRecordFiles.PathOf(run.Home, bound.Unit);
            run.Keep(path);
            records.Write(record with { Pending = pending, History = [.. record.History, .. said] });
            run.Said(path);
        }
    }

    /// <summary>Whether <paramref name="review"/> is the graph's end with no step after its Accept: a way back on its changes is no step after it.</summary>
    private static bool NothingAfter(NodeGraph graph, string review) =>
        graph.End == review && !graph.Wires.Any(wire => wire.From?.Node == review && wire.Kind != WireKinds.Back);
}
