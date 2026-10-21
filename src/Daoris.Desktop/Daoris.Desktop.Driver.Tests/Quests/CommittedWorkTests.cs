using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's half of RETRYWORK1: the commits a failed session left beyond its start are read for an open quest it failed before
/// taking, and for nothing else, so a look with no such quest asks git nothing.
/// </summary>
public sealed class CommittedWorkTests
{
    private static QuestView Quest(string id, string status = "Open") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    private static PriorSession Last(string session, string state, bool interrupted = false, string? tree = "D:/trees/s", string? before = "4f2a9c1") =>
        new(session, tree, state, Interrupted: interrupted) { BaseCommit = before };

    [Fact]
    public async Task Only_an_open_quests_cut_off_with_its_tree_and_start_is_counted()
    {
        var snapshot = new Snapshot(
            [
                Quest("failed"), Quest("interrupted"), Quest("stopped"), Quest("completed"), Quest("no-tree"), Quest("no-start"),
                Quest("taken", "Taken"), Quest("teammate"), Quest("none"),
            ],
            [], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase)
            {
                ["failed"] = Last("s1", "failed"),
                ["interrupted"] = Last("s2", "stopped", interrupted: true),
                ["stopped"] = Last("s3", "stopped"),
                ["completed"] = Last("s4", "completed"),
                ["no-tree"] = Last("s5", "failed", tree: null),
                ["no-start"] = Last("s6", "failed", before: null),
                ["taken"] = Last("s7", "failed"),
                ["teammate"] = Last("origin/s8", "failed"),
            },
        };
        var asked = new List<(string Tree, string Before)>();

        var looked = await CommittedWork.LookAsync(snapshot, (tree, before, _) =>
        {
            asked.Add((tree, before));
            return Task.FromResult<int?>(asked.Count == 1 ? 2 : null);
        });

        Assert.Equal(2, asked.Count);
        Assert.Equal(2, Assert.Single(looked.Committed, pair => pair.Key == "failed").Value);
        // A count git could not read is left out, and the quest is planned as a first start.
        Assert.DoesNotContain("interrupted", looked.Committed.Keys);
    }

    [Fact]
    public async Task A_look_with_no_such_quest_asks_nothing_and_keeps_its_snapshot()
    {
        var snapshot = new Snapshot([Quest("q1")], [], []);

        var looked = await CommittedWork.LookAsync(snapshot, (_, _, _) => throw new InvalidOperationException("git was asked"));

        Assert.Same(snapshot, looked);
    }
}
