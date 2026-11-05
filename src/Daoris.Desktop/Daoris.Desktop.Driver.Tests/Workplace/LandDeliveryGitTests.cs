using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1b2 (the land node design §3 step 4, §8 row 4): a landing from its delivery's intent. The intent chooses the method, the branch,
/// the line and the pinned tip, never the live rule; each git effect is written ahead and its outcome after (prepared, ref written,
/// recorded); the branch's ref goes through one ref transaction under the checkout's lock; today's checks refuse as before, a refusal
/// withdrawing the preparation; a push, a pull request and an integrate stop once the branch is prepared and recorded. Real git, as
/// <see cref="LandingSquashTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandDeliveryGitTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private const string Named = "feature/ENG-12-tidy";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "land-delivery", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public LandDeliveryGitTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// The live rule merges; the delivery says branch, and its branch is made at the pinned tip under the intent's name, recorded, each phase
    /// done with what it read, and the delivery settled delivered.
    /// </summary>
    [Fact]
    public async Task A_branch_delivery_lands_its_intent_s_branch_at_the_pinned_tip_whatever_the_live_rule_says()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(Named, landed.Branch);
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(tip, trees.Recorded.Of("engine", Named)?.Tip);
        Assert.Equal(await RevAsync(root, "main^{commit}"), await RevAsync(root, "refs/heads/main"));
        Assert.DoesNotContain("the work", await GitFixture.GitAsync(root, "log", "main", "--oneline"));

        var phases = Reopened().Phases(claim.Key);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], phases.Select(each => each.Phase));
        Assert.All(phases, each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.Equal(tip, phases[0].Evidence);
        Assert.Equal(tip, phases[1].Evidence);
        Assert.Contains(Named, phases[1].Intended);
        Assert.Equal(DeliverySettlements.Delivered, Reopened().Delivery(claim.Key)?.Settled);
    }

    /// <summary>A commit made after the tip the delivery pinned: nothing lands, and the preparation is withdrawn with the moved words.</summary>
    [Fact]
    public async Task A_tip_moved_past_the_pinned_one_is_refused_and_its_preparation_withdrawn()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        await CommitAsync(tree.Path, "later.txt", "made after the judgement", "a later commit");

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.False(landed.Landed, landed.Message);
        Assert.Contains("moved after its landing was judged", landed.Message);
        Assert.Null(await RevAsync(root, $"refs/heads/{Named}"));
        var phase = Assert.Single(Reopened().Phases(claim.Key));
        Assert.Equal((DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn), (phase.Phase, phase.Outcome));
        Assert.Contains("moved after its landing was judged", phase.Said);
        Assert.True(Reopened().Delivery(claim.Key)!.Unresolved);
    }

    /// <summary>The intent's branch already stands, made by somebody: refused as today, the branch untouched, nothing written ahead past the preparation.</summary>
    [Fact]
    public async Task A_branch_name_that_stands_and_that_Daoris_did_not_make_is_refused_and_left_as_it_is()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        await GitFixture.GitAsync(root, "branch", Named);
        var theirs = await RevAsync(root, $"refs/heads/{Named}");
        var (store, claim) = Start(tip, DeliveryMethods.Branch);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal(LandingCode.Exists, landed.Refusal);
        Assert.Contains("does not move a branch it did not make", landed.Message);
        Assert.Equal(theirs, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Null(trees.Recorded.Of("engine", Named));
        var phase = Assert.Single(Reopened().Phases(claim.Key));
        Assert.Equal((DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn), (phase.Phase, phase.Outcome));
    }

    /// <summary>
    /// The authorization's options squash: one commit of the tip's tree onto the line, written by the transaction, the record keeping the tip
    /// it squashed; the preparation's evidence is the commit the ref was then written at.
    /// </summary>
    [Fact]
    public async Task A_squash_delivery_lands_one_commit_of_the_pinned_tip_s_tree_and_records_what_it_squashed()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        await CommitAsync(tree.Path, "second.txt", "the second step", "the second step");
        tip = await RevAsync(tree.Path, "HEAD");
        var (store, claim) = Start(tip, DeliveryMethods.Branch, options: """{"method":"branch","squash":true}""");

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("squashed", landed.Message);
        var made = await RevAsync(root, $"refs/heads/{Named}");
        Assert.NotEqual(tip, made);
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, $"{Named}^"));
        Assert.Equal(await RevAsync(root, $"{tip}^{{tree}}"), await RevAsync(root, $"{Named}^{{tree}}"));
        var record = trees.Recorded.Of("engine", Named);
        Assert.Equal((made, tip), (record?.Tip, record?.Squashed));
        var phases = Reopened().Phases(claim.Key);
        Assert.Equal((made, made), (phases[0].Evidence, phases[1].Evidence));
        Assert.Equal(DeliverySettlements.Delivered, Reopened().Delivery(claim.Key)?.Settled);
    }

    /// <summary>
    /// LANDNODE1f: the node's template rendered into the intent opens the squash commit, over the commits' list, and the maker's line stays.
    /// </summary>
    [Fact]
    public async Task A_squash_delivery_s_commit_opens_with_its_intent_s_rendered_message_and_keeps_the_makers_line()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var message = LandNodes.RenderMessage("feat: {title} ({ticket}) [{session}]", "Tidy the readme", "0fda18", "ENG-12", "s1")!;
        var (store, claim) = Start(tip, DeliveryMethods.Branch, options: """{"method":"branch","squash":true}""", message: message);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feat: Tidy the readme (ENG-12) [s1]", (await GitFixture.GitAsync(root, "log", "-1", "--format=%s", Named)).Trim());
        var body = await GitFixture.GitAsync(root, "log", "-1", "--format=%B", Named);
        Assert.Contains("- the work", body);
        Assert.Contains(DaorisMaker.Trailer, body);
    }

    /// <summary>LANDNODE1f: the merge commit says the intent's message, with the maker's line, where the line's own is <c>Merge branch</c>.</summary>
    [Fact]
    public async Task A_merge_delivery_s_merge_commit_says_its_intent_s_rendered_message()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Merge, message: "Land the readme tidy (ENG-12)");

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim), beside: NoSessions);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("Land the readme tidy (ENG-12)", (await GitFixture.GitAsync(root, "log", "-1", "--format=%s", "main")).Trim());
        Assert.Contains(DaorisMaker.Trailer, await GitFixture.GitAsync(root, "log", "-1", "--format=%B", "main"));
        Assert.Equal(2, (await GitFixture.GitAsync(root, "log", "-1", "--format=%P", "main")).Trim().Split(' ').Length);
    }

    /// <summary>The line already holds the work: nothing lands, nothing is written past the preparation, which is withdrawn.</summary>
    [Fact]
    public async Task Work_the_line_already_holds_is_refused_and_its_preparation_withdrawn()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        await GitFixture.GitAsync(root, "merge", "--ff-only", "--quiet", tree.Branch);
        var (store, claim) = Start(tip, DeliveryMethods.Branch);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal(LandingCode.Nothing, landed.Refusal);
        Assert.Contains("nothing to land", landed.Message);
        Assert.Null(await RevAsync(root, $"refs/heads/{Named}"));
        var phase = Assert.Single(Reopened().Phases(claim.Key));
        Assert.Equal((DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn), (phase.Phase, phase.Outcome));
    }

    /// <summary>Another writer holds the checkout alone: the ref is not written, nothing is written ahead past the preparation, which is withdrawn.</summary>
    [Fact]
    public async Task A_checkout_another_writer_holds_writes_no_ref_and_withdraws_the_preparation()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);

        TreeLanding landed;
        using (TreeLock.TryWriting(_home, root, "engine", TreeLock.Acting))
        {
            landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));
        }

        Assert.False(landed.Landed, landed.Message);
        Assert.Contains("a git act was changing `engine`'s checkout", landed.Message);
        Assert.Null(await RevAsync(root, $"refs/heads/{Named}"));
        var phase = Assert.Single(Reopened().Phases(claim.Key));
        Assert.Equal((DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn), (phase.Phase, phase.Outcome));
    }

    /// <summary>A later delivery of the unit moves the branch its first made on, from the recorded tip, by the transaction's update.</summary>
    [Fact]
    public async Task A_later_delivery_moves_the_branch_its_first_made_on_from_the_recorded_tip()
    {
        var (root, trees, tree, first) = await TreeAsync();
        var (store, claim) = Start(first, DeliveryMethods.Branch);
        Assert.True((await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim))).Landed);
        await CommitAsync(tree.Path, "more.txt", "more work", "more work");
        var second = await RevAsync(tree.Path, "HEAD");
        var (_, later) = Start(second!, DeliveryMethods.Branch);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, later));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(first, landed.AdvancedFrom);
        Assert.Equal(second, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(second, trees.Recorded.Of("engine", Named)?.Tip);
        var written = Reopened().Phases(later.Key)[1];
        Assert.Equal((DeliveryPhases.RefWritten, DeliveryOutcomes.Done), (written.Phase, written.Outcome));
        Assert.Contains($"moved from {first} to {second}", written.Intended);
        Assert.Equal(DeliverySettlements.Delivered, Reopened().Delivery(later.Key)?.Settled);
    }

    /// <summary>A merge delivery merges, into the intent's line, through the promotion, and records it so.</summary>
    [Fact]
    public async Task A_merge_delivery_merges_into_its_line()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Merge);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim), beside: NoSessions);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("merged", landed.Message);
        Assert.Equal(0, (await GitFixture.RunAsync(root, "merge-base", "--is-ancestor", tip, "refs/heads/main")).ExitCode);
        Assert.Empty((await GitFixture.GitAsync(root, "for-each-ref", "refs/heads/feature/")).Trim());
        var phases = Reopened().Phases(claim.Key);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], phases.Select(each => each.Phase));
        Assert.All(phases, each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.Equal(await RevAsync(root, "refs/heads/main"), phases[1].Evidence);
        Assert.Equal(DeliverySettlements.Delivered, Reopened().Delivery(claim.Key)?.Settled);
    }

    /// <summary>
    /// An integrate prepares and records the branch and stops there, no plugin told, delivered, its line's admission the next node's.
    /// </summary>
    [Theory]
    [InlineData(DeliveryMethods.Integrate)]
    public async Task A_push_a_pull_request_and_an_integrate_stop_once_the_branch_is_prepared_and_recorded(string method)
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, method);

        var landed = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        Assert.Null(landed.Plugin);
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(tip, trees.Recorded.Of("engine", Named)?.Tip);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], Reopened().Phases(claim.Key).Select(each => each.Phase));
        Assert.Equal(method == DeliveryMethods.Integrate ? DeliverySettlements.Delivered : null, Reopened().Delivery(claim.Key)?.Settled);
    }

    /// <summary>
    /// A stop between an effect and its outcome, as a crash would leave it: reopened, the store shows that phase written ahead with no
    /// outcome, git and the record hold what the effect made, and a landing again is refused until a restart settles it by phase.
    /// </summary>
    [Theory]
    [InlineData(DeliveryPhases.RefWritten)]
    [InlineData(DeliveryPhases.Recorded)]
    public async Task An_effect_cut_before_its_outcome_stands_written_ahead_without_one_after_a_reopen(string cut)
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        var stopping = new TreeDelivery(store, claim) { Seam = phase => phase == cut ? Task.FromException(new Crash()) : Task.CompletedTask };

        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: stopping));

        var phases = Reopened().Phases(claim.Key);
        Assert.Equal(cut, phases[^1].Phase);
        Assert.True(phases[^1].Open);
        Assert.Contains(Named, phases[^1].Intended);
        Assert.All(phases.SkipLast(1), each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(cut == DeliveryPhases.Recorded ? tip : null, new SessionTrees(_home).Recorded.Of("engine", Named)?.Tip);

        var again = await new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(Reopened(), claim));

        Assert.False(again.Landed, again.Message);
        Assert.Contains("a restart settles it by phase", again.Message);
        Assert.Equal(phases.Count, Reopened().Phases(claim.Key).Count);
        Assert.True(Reopened().Delivery(claim.Key)!.Unresolved);
    }

    /// <summary>
    /// LANDNODE1h (the land node design §3): the standing automatic authority is judged again just before the first effect. Revoked, or judged
    /// by nothing, it refuses with no phase written ahead and no branch made; standing, it lands as the person's Accept does.
    /// </summary>
    [Fact]
    public async Task A_standing_automatic_authority_is_judged_again_before_the_first_effect()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch, automatic: true);

        var revoked = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning,
            delivery: new TreeDelivery(store, claim) { Standing = _ => "its repository's rule no longer accepts automatically" });
        var unjudged = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim));

        Assert.False(revoked.Landed, revoked.Message);
        Assert.Contains("no longer accepts automatically", revoked.Message);
        Assert.False(unjudged.Landed, unjudged.Message);
        Assert.Empty(Reopened().Phases(claim.Key));
        Assert.Null(await RevAsync(root, $"refs/heads/{Named}"));

        var standing = await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim) { Standing = _ => null });

        Assert.True(standing.Landed, standing.Message);
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
    }

    // ——— The fixture.

    private static readonly LandingSubject Subject = new("s1", "0fda18", "Tidy the readme");

    private static readonly Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>> NoSessions = _ => Task.FromResult<IReadOnlyList<TreeHolder>>([]);

    private sealed class Crash : Exception;

    private DeliveryStore Reopened() => new(_home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);

    private (DeliveryStore Store, DeliveryClaim Claim) Start(
        string tip, string method, string options = """{"method":"branch"}""", bool automatic = false, string message = "Tidy the readme (ENG-12)")
    {
        var store = Reopened();
        var intent = new DeliveryIntent(
            "unit-1", "engine", tip, method, "main", "main", method == DeliveryMethods.Merge ? null : Named, method == DeliveryMethods.Merge ? null : "ENG-12",
            message, method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? "forge" : null, "epoch-7");
        var start = store.Start(intent, automatic
            ? DeliveryAuthorization.Automatic("repository:engine", "r3", "graph-digest", options, "[]")
            : DeliveryAuthorization.Person("accept-3", "r3", "graph-digest", options, "[]"));
        Assert.True(start.Started, start.Refused ?? start.Held);
        return (store, start.Claim!);
    }

    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Tip)> TreeAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work", "the work");
        return (root, trees, tree, await RevAsync(tree.Path, "HEAD"));
    }

    private static async Task<string?> RevAsync(string cwd, string what)
    {
        var run = await GitFixture.RunAsync(cwd, "rev-parse", "--verify", "--quiet", what);
        return run.ExitCode == 0 ? run.Stdout.Trim() : null;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }
}
