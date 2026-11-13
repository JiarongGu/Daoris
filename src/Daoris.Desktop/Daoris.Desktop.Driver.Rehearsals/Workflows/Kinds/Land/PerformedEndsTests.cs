using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a press waiting on a run's <c>land</c> reads, for every end of the performer's try and not for a landing alone: a tree moved past the
/// tip its land started at, a closed try and a carry-on each keep an end the press says, and a moved tree that comes back to that tip lands
/// at the next pass with no press.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PerformedEndsTests : GraphRunFixture
{
    private LandingDoor Door() => new(() => new LandPerformer(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true });

    private IEnumerable<string?> Codes() => _lines.Where(line => line.Event == "landing.auto").Select(CodeOf);

    private static TreeLanding? EndOf(string tip) => LandOutcomes.Of(RunUnits.IdOf("q1"), tip);

    private const string Merge = """{"method":"merge"}""";

    /// <summary>The tree's HEAD moved on to a commit of its own while its session branch stays at the tip the land is started at.</summary>
    private static async Task<string> MovedAsync(string tree)
    {
        var named = (await GitAsync(tree, "rev-parse", "--abbrev-ref", "HEAD")).Trim();
        await GitAsync(tree, "checkout", "--detach");
        await CommitAsync(tree, "later.txt", "later\n", "a later commit");
        return named;
    }

    [Fact]
    public async Task A_tree_moved_past_its_start_is_answered_with_the_move()
    {
        var (root, tree, branch) = await BoundRunAsync(Merge);
        var start = await HeadAsync(tree);
        await MovedAsync(tree);
        var door = Door();

        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Equal([LandingCode.Moved], Codes());
        var end = EndOf(start);
        Assert.NotNull(end);
        Assert.Equal((false, LandingCode.Moved), (end.Landed, end.Refusal));
        Assert.Contains("moved since it was judged", end.Message);
    }

    /// <summary>Back at the tip its land started at, with the status of its refusal, the next pass tries it again and lands it.</summary>
    [Fact]
    public async Task A_tree_moved_and_back_at_its_start_lands_at_the_next_pass()
    {
        var (root, tree, branch) = await BoundRunAsync(Merge);
        var start = await HeadAsync(tree);
        var named = await MovedAsync(tree);
        var door = Door();
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        await GitAsync(tree, "checkout", named);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Equal([LandingCode.Moved, LandingCode.Landed], Codes());
        Assert.Equal(start, (await GitAsync(root, "merge-base", "main", start)).Trim());
        Assert.True(EndOf(start)?.Landed);
    }

    /// <summary>A try closed before any landing (its quest no longer done) keeps that end, worded, for the press.</summary>
    [Fact]
    public async Task A_closed_try_is_answered_with_why_nothing_lands()
    {
        var (root, tree, branch) = await BoundRunAsync(Merge);
        var start = await HeadAsync(tree);
        _world.Quests["q1"] = _world.Quests["q1"] with { Status = "Taken" };
        var door = Door();

        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        var end = EndOf(start);
        Assert.Equal((false, LandingCode.Undone), (end?.Landed, end?.Refusal));
        Assert.Contains("no longer done", end!.Message);
    }

    /// <summary>Another quest's session went on in the run's tree: the superseded end is kept for the press and said once.</summary>
    [Fact]
    public async Task A_superseded_part_is_said_once_and_not_tried_again_at_every_pass()
    {
        var (root, tree, branch) = await BoundRunAsync(Merge);
        var start = await HeadAsync(tree);
        var made = _world.Records.Single(record => record.Id == "s1").Created;
        _world.Records.Add(new SessionRecord("s9", "engine", "completed") { Quest = "q9", Tree = tree, Created = made.AddMinutes(1) });
        var door = Door();

        for (var pass = 0; pass < 3; pass++)
        {
            await PassAsync(root, branch, tree, door: door);
            await door.IdleAsync();
        }

        Assert.Equal([LandingCode.Superseded], Codes());
        var end = EndOf(start);
        Assert.Equal((false, LandingCode.Superseded), (end?.Landed, end?.Refusal));
        Assert.Contains("`s9`", end!.Message);
        Assert.Single(_lines, line => line.Event == "landing.auto");
    }

    /// <summary>The person's Accept on a dirty tree carries the session on, and that end is kept as a carry-on for the press to say so.</summary>
    [Fact]
    public async Task A_dirty_tree_s_carry_on_is_answered_as_one()
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Reviewed), DateTimeOffset.UtcNow);
        var branch = Assert.Single(new SessionBranches(_home).All());
        var start = await HeadAsync(tree);
        var door = Door();
        await PassAsync(root, branch, tree, door: door);
        await File.WriteAllTextAsync(Path.Combine(tree, "wip.txt"), "unfinished\n");
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);

        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Single(_world.Said);
        var end = EndOf(start);
        Assert.Equal((false, LandingCode.CarriedOn), (end?.Landed, end?.Refusal));
    }

    /// <summary>A graph holding the person's review before a merge.</summary>
    private const string Reviewed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},{"id":"r>l","kind":"after","from":"review","to":"landing"}]}
        """;
}
