using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The trees a door reads that no part has judged are walked a few at once, never one after another: the Sessions list's groups
/// wait on these walks at a start, before the machine's facts have begun, and each walk's git queues behind the first look's.
/// </summary>
public sealed class HelpTreeWalkTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-11T07:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task The_trees_no_part_judged_are_walked_a_few_at_once()
    {
        var trees = Enumerable.Range(1, 6).Select(each => Path.Combine("trees", "default", "engine", $"s-{each}")).ToList();
        var look = new SessionLook([.. trees.Select((tree, at) => Ended($"e{at}", "engine", tree))], [], []);
        using var facts = new MachineFacts();
        var walking = 0;
        var most = 0;

        async Task<TreeWork?> Walk(string tree, CancellationToken ct)
        {
            var now = Interlocked.Increment(ref walking);
            InterlockedMax(ref most, now);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref walking);
            return new TreeWork(1, 0);
        }

        var read = await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(trees.Select(SessionGroups.Normal).Order(StringComparer.OrdinalIgnoreCase), read.Trees.Keys.Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(read.Trees.Values, work => Assert.Equal(1, work.Commits));
        Assert.InRange(Volatile.Read(ref most), 2, HelpTreeFacts.AtOnce);
    }

    [Fact]
    public async Task A_walk_that_finds_nothing_leaves_its_tree_out()
    {
        var kept = Path.Combine("trees", "default", "engine", "s-1");
        var gone = Path.Combine("trees", "default", "engine", "s-2");
        var look = new SessionLook([Ended("e1", "engine", kept), Ended("e2", "engine", gone)], [], []);
        using var facts = new MachineFacts();

        var read = await HelpFacts.WithTreesAsync(
            facts, look, _ => true, (tree, _) => Task.FromResult(tree == gone ? null : new TreeWork(2, 0))).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal([SessionGroups.Normal(kept)], read.Trees.Keys);
    }

    private static SessionRecord Ended(string id, string repository, string tree) =>
        new(id, repository, "completed") { Tree = tree, Created = At.AddHours(-1), Updated = At.AddMinutes(-30) };

    private static void InterlockedMax(ref int most, int now)
    {
        var seen = Volatile.Read(ref most);
        while (now > seen)
        {
            var was = Interlocked.CompareExchange(ref most, now, seen);
            if (was == seen) return;
            seen = was;
        }
    }
}
