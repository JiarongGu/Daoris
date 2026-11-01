using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1c3 (the land node design §3, §4): Current's landing performed through its delivery. Every door of a session's tree under Current
/// writes the delivery once the landing is judged and lands through it, its intent read from the live rule: today's message, the rule's
/// <c>integrate</c> and the tree's line kept; the standing automatic authority judged again; the look's landing at done adopting the due list
/// and the person's kept Accept; and a press and a pass at once landing once. Real git, as <see cref="LandDeliveryGitTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CurrentLandGitTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "current-land", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public CurrentLandGitTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>The press on a merge rule: one delivery, the person's, delivered; the merge commit says today's words, not the intent's.</summary>
    [Fact]
    public async Task A_press_on_a_merge_rule_lands_through_its_delivery_and_its_merge_says_today_s_message()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", LandingRule.Merge));
        var (trees, tree, tip) = await TreeAsync(root);

        var landed = await trees.LandAsync(tree, Subject, inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Merge, tip, "main"), (delivery.Intent.Method, delivery.Intent.Tip, delivery.Intent.Line));
        Assert.Equal(DeliveryAuthorities.Person, delivery.Authorization.Authority);
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
        Assert.StartsWith("Merge branch '", (await GitFixture.GitAsync(root, "log", "-1", "--format=%s", "main")).Trim());
    }

    /// <summary>A branch rule carrying <c>integrate</c>: delivered as an integrate from the tree's line, and its record keeps the rule's integrate.</summary>
    [Fact]
    public async Task A_branch_rule_s_integrate_and_its_line_ride_the_delivery_to_the_landing_record()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}"))
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Owner)));
        var (trees, tree, tip) = await TreeAsync(root);

        var landed = await trees.LandAsync(tree, Subject, inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Integrate, "main", "feature/0fda18"), (delivery.Intent.Method, delivery.Intent.Line, delivery.Intent.Branch));
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule), trees.Recorded.Of("engine", "feature/0fda18")?.Integrate);
        Assert.Equal(tip, trees.Recorded.Of("engine", "feature/0fda18")?.Tip);
    }

    /// <summary>
    /// A merge rule that accepts automatically lands at done with no press, since the workflow decides (D145's note); its delivery is the
    /// standing authority's, judged again before its first effect. The look adopts the due entry.
    /// </summary>
    [Fact]
    public async Task A_due_entry_under_a_merge_rule_that_accepts_automatically_is_landed_by_the_look_under_the_standing_authority()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", LandingRule.Merge with { AutoAccept = true }));
        var world = new World();
        var tree = await DoneSessionAsync(world, root, "s1", "q1");

        var said = await LookAsync(world);

        Assert.DoesNotContain("no longer accepts automatically", said);
        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Merge, DeliveryAuthorities.Automatic), (delivery.Intent.Method, delivery.Authorization.Authority));
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
        Assert.Contains("the work of s1", await GitFixture.GitAsync(root, "log", "main", "--oneline"));
        Assert.True(Directory.Exists(tree));
    }

    /// <summary>The person's Accept kept on a session whose rule waits for a press: its due entry is landed by the look as the person's.</summary>
    [Fact]
    public async Task A_kept_Accept_is_adopted_by_the_look_and_lands_as_the_person_s()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}")));
        var world = new World();
        var tree = await OpenedAsync(root, "s1");
        new CarriedAccepts(_home).Keep("s1", tree, DateTimeOffset.UtcNow);
        await DoneSessionAsync(world, root, "s1", "q1", tree);

        await LookAsync(world);

        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Branch, DeliveryAuthorities.Person), (delivery.Intent.Method, delivery.Authorization.Authority));
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
        Assert.NotNull(new SessionTrees(_home).Recorded.Of("engine", "feature/q1"));
    }

    /// <summary>Nothing due: the look's landing reads no quest, no record and no git, and writes no delivery.</summary>
    [Fact]
    public async Task Nothing_due_means_nothing_read()
    {
        Save(DriverConfig.Empty.WithLanding("engine", LandingRule.Merge with { AutoAccept = true }));
        var lander = new AutoLander(_home, new Unread(), new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _ => { });
        var starts = 0;

        AutoChosen chosen;
        using (ProcessStarts.Within(info =>
        {
            Interlocked.Increment(ref starts);
            return Process.Start(info);
        }))
        {
            chosen = await lander.ChooseAsync(Config());
        }

        Assert.Empty(chosen.Chosen);
        Assert.Empty(chosen.Said);
        Assert.Equal(0, starts);
        Assert.Empty(Store().Unresolved());
    }

    /// <summary>The person's press and the look's pass of one session at once: one landing, one delivery, and the other lands nothing more.</summary>
    [Fact]
    public async Task A_press_and_a_pass_at_once_land_once()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}", AutoAccept: true)));
        var (trees, tree, tip) = await TreeAsync(root);

        var both = await Task.WhenAll(
            trees.LandAsync(tree, Subject, inUse: NoneRunning),
            trees.LandAsync(tree, Subject, inUse: NoneRunning, acceptedBy: AcceptedBy.Auto));

        Assert.Single(both, each => each.Landed);
        Assert.Single(both, each => !each.Landed && each.Refusal == AutoLandingCode.Already);
        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
        Assert.Equal(tip, (await GitFixture.GitAsync(root, "rev-parse", "refs/heads/feature/0fda18")).Trim());
    }

    /// <summary>
    /// A terminal with no loop runs the same fence: a press refused for uncommitted work leaves its delivery unresolved, the press again at
    /// the same tip goes on with it, and one at a new tip withdraws it and lands its own.
    /// </summary>
    [Fact]
    public async Task A_press_with_no_loop_goes_on_with_its_unresolved_delivery_or_withdraws_it_for_a_new_tip()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}")));
        var (trees, tree, tip) = await TreeAsync(root);
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "not committed\n");

        var refused = await trees.LandAsync(tree, Subject, inUse: NoneRunning);

        Assert.False(refused.Landed, refused.Message);
        var first = Assert.Single(Store().OfUnit("s1"));
        Assert.True(first.Unresolved);
        Assert.Equal(DeliveryOutcomes.Withdrawn, Assert.Single(Store().Phases(first.Key)).Outcome);

        await CommitAsync(tree, "loose.txt", "committed now", "the loose file");
        var landed = await trees.LandAsync(tree, Subject, inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var deliveries = Store().OfUnit("s1");
        Assert.Equal(2, deliveries.Count);
        Assert.Equal(DeliverySettlements.Withdrawn, deliveries.Single(each => each.Key == first.Key).Settled);
        var second = deliveries.Single(each => each.Key != first.Key);
        Assert.Equal(DeliverySettlements.Delivered, second.Settled);
        Assert.NotEqual(tip, second.Intent.Tip);
    }

    /// <summary>
    /// A name git would not take is refused at its delivery's start, nothing written: a delivery's key holds no name, so one written for it would
    /// stand in the way of the name that takes, which then lands through its own.
    /// </summary>
    [Fact]
    public async Task A_branch_name_git_would_not_take_is_refused_at_its_delivery_s_start_and_the_one_that_takes_lands_through_its_own()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        Save(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}")));
        var (trees, tree, _) = await TreeAsync(root);

        var refused = await trees.LandAsync(tree, Subject with { Entered = "two..dots" }, inUse: NoneRunning);

        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Refused, refused.Refusal);
        Assert.Contains("`two..dots`, is not a branch name git would take", refused.Message);
        Assert.Empty(Store().OfUnit("s1"));

        var landed = await trees.LandAsync(tree, Subject with { Entered = "feature/taken" }, inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var delivery = Assert.Single(Store().OfUnit("s1"));
        Assert.Equal(("feature/taken", DeliverySettlements.Delivered), (delivery.Intent.Branch, delivery.Settled));
    }

    // ——— The fixture.

    private static readonly LandingSubject Subject = new("s1", "0fda18", "Tidy the readme");

    private DeliveryStore Store() => new(_home);

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private void Save(DriverConfig config) => config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private async Task<(SessionTrees Trees, string Tree, string Tip)> TreeAsync(string root)
    {
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work", "the work");
        return (trees, tree.Path, (await GitFixture.GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
    }

    private async Task<string> OpenedAsync(string root, string session)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        return opened.Path;
    }

    private async Task<string> DoneSessionAsync(World world, string root, string session, string quest, string? opened = null)
    {
        var tree = opened ?? await OpenedAsync(root, session);
        world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done");
        world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = tree, Created = DateTimeOffset.UtcNow });
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session, world.Quests[quest],
            "Done", "completed", tree, "aurora");
        Assert.Equal(AutoConcluded.Due, verdict);
        return tree;
    }

    private async Task<string> LookAsync(World world)
    {
        var lander = new AutoLander(_home, world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _ => { });
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);
        return Assert.Single(await lander.LandAsync(chosen));
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SessionRecord> Records { get; } = [];
        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));
        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);
        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);
        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    /// <summary>A world any read of which fails the test.</summary>
    private sealed class Unread : IAutoLandingWorld
    {
        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => throw new InvalidOperationException("a quest was read");
        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => throw new InvalidOperationException("the records were read");
        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => throw new InvalidOperationException("the sessions in use were read");
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => throw new InvalidOperationException("the quests were read");
        public Task<AskView?> AskAsync(string id, CancellationToken ct) => throw new InvalidOperationException("an ask was read");
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => throw new InvalidOperationException("an opinion was read");
    }
}
