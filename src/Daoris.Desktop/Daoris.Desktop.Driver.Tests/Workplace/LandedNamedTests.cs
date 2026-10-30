using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRETIRE1 part 2 over real git: a landing whose row the line took by another commit is listed with its age and its closed quest
/// (the quest's closing is the service's, passed in once), offers the door that withdraws it, and the withdrawal from that door clears
/// the row from the next list.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandedNamedTests : LandedFixture
{
    private static IReadOnlySet<string> Closed(params string[] quests) => quests.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task<(string Root, SessionTrees Trees, string Branch)> DiffersAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await CommitAsync(root, "shared.txt", "the line's own take\n", "the row by another commit");
        return (root, trees, landed.Branch!);
    }

    [Fact]
    public async Task A_landing_the_line_took_another_way_is_listed_with_its_age_and_its_closed_quest_and_withdrawn_from_the_lists_door()
    {
        var (root, trees, branch) = await DiffersAsync();
        var repositories = new[] { ("engine", (string?)"aurora", (string?)root) };

        var plan = await trees.CleanPlanAsync(repositories, new HashSet<string>(), default, Closed("q2"));

        var item = Landed(plan, branch);
        Assert.Equal(LandedKind.Differs, item.Kind);
        Assert.Equal("q2", item.Quest);
        Assert.True(item.QuestClosed);
        var landedAt = Assert.IsType<DateTimeOffset>(item.LandedAt);
        var said = TreesPlanWords.CleanPlan(plan, [], new TreesAsk(TreesLook.Clean), landedAt.AddHours(29)).Out;
        Assert.Contains(said, line => line.Contains(branch) && line.Contains("landed 29 hours ago, and its quest #q2 is closed"));
        var door = Assert.Single(said, line => line.Contains("trees remove"));
        Assert.Contains($"trees remove {branch} --repository engine --why", door);

        // The door: the withdrawal the list names, which the next list no longer holds.
        var done = await trees.WithdrawLandedAsync(root, "engine", branch, "the row landed another way, by hand");
        Assert.True(done.Done, done.Message);
        var after = await trees.CleanPlanAsync(repositories, new HashSet<string>(), default, Closed("q2"));
        Assert.Empty(after.Landed);
    }

    [Fact]
    public async Task A_quest_nobody_closed_is_named_by_its_age_alone_and_the_clean_part_carries_the_same_row()
    {
        var (root, trees, branch) = await DiffersAsync();

        var part = await trees.SweepPartAsync("engine", "aurora", root, new HashSet<string>(), default, Closed("q9"));

        var item = Landed(part, branch);
        Assert.Equal("q2", item.Quest);
        Assert.False(item.QuestClosed);
        var said = LandedWords.Standing(item, item.LandedAt!.Value.AddHours(2));
        Assert.Equal("; landed 2 hours ago", said);

        var closed = await trees.SweepPartAsync("engine", "aurora", root, new HashSet<string>(), default, Closed("q2"));
        Assert.True(Landed(closed, branch).QuestClosed);
    }
}
