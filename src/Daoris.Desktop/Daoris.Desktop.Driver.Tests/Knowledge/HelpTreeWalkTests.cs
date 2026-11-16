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

        var read = await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk).WaitAsync(Poll.Patience);

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
            facts, look, _ => true, (tree, _) => Task.FromResult(tree == gone ? null : new TreeWork(2, 0))).WaitAsync(Poll.Patience);

        Assert.Equal([SessionGroups.Normal(kept)], read.Trees.Keys);
    }

    /// <summary>
    /// SESSIONFLASH1: a reader that does not wait, the groups slice, answers at once with what the parts judged and walks nothing
    /// while their first judgement is to come, the facts not begun or their wait not passed; once it passed, it walks as a door does.
    /// </summary>
    [Fact]
    public async Task A_reader_that_does_not_wait_walks_nothing_before_the_parts_first_judgement()
    {
        var tree = Path.Combine("trees", "default", "engine", "s-1");
        var look = new SessionLook([Ended("e1", "engine", tree)], [], []);
        using var facts = new MachineFacts();
        var walked = 0;

        Task<TreeWork?> Walk(string _, CancellationToken __)
        {
            Interlocked.Increment(ref walked);
            return Task.FromResult<TreeWork?>(new TreeWork(1, 0));
        }

        var unbegun = await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk, waits: false).WaitAsync(Poll.Patience);
        facts.Start();
        var waiting = await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk, first: TimeSpan.FromMilliseconds(300), waits: false)
            .WaitAsync(Poll.Patience);

        Assert.Empty(unbegun.Trees);
        Assert.Empty(waiting.Trees);
        Assert.Equal(0, Volatile.Read(ref walked));

        // A door waits the rest of the wait and walks; past it, the reader that does not wait walks too.
        await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk).WaitAsync(Poll.Patience);
        var past = await HelpFacts.WithTreesAsync(facts, look, _ => true, Walk, waits: false).WaitAsync(Poll.Patience);

        Assert.Equal(1, past.Trees[SessionGroups.Normal(tree)].Commits);
        Assert.Equal(2, Volatile.Read(ref walked));
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
