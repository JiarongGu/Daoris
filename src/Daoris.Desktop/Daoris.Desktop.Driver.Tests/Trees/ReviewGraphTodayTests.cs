using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-ii's comparison (D202 part one, the git-app design P1.7): on each fixture, real git in scratch repositories, Review is read
/// once through today's chain (<see cref="SessionReviews.ChainReadAsync"/>, the oracle) and once through the graph the route reads
/// through (<see cref="SessionReviews.TimedAsync"/>), and the two outcomes are held equal: an answer field by field, its patches byte for
/// byte, or a refusal (none answered) or a fault by its type and words. The commands each ran are held equal too: the Π reads (every
/// diff, <c>status</c> and <c>merge-tree</c>) in the same order, and every other start alike but for the graph's wasted speculative
/// starts and the <c>--show-toplevel</c>s it shares. Nothing moves the tree between the two reads.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewGraphTodayTests : LandedFixture
{
    private const string Session = "aurora";

    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Began)> TreeAsync(string? from = null)
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", Session, from: from);
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        return (root, trees, tree, began);
    }

    /// <summary>Both reads of one fixture, held equal; the graph's starts returned for a test that says more of them.</summary>
    private static async Task<GitCount> SameAsync(SessionTrees trees, string? tree, string? began, string? checkout = null)
    {
        Task<string?> CheckoutOf(string _, CancellationToken __) => Task.FromResult(checkout);

        string chain;
        GitLaunched[] chainStarts;
        using (var count = GitLaunch.Count())
        {
            chain = await OutcomeAsync(() => SessionReviews.ChainReadAsync(Session, tree, began, trees, CheckoutOf, CancellationToken.None));
            chainStarts = [.. count.Starts];
        }

        GitCount graph;
        string answered;
        using (graph = GitLaunch.Count())
        {
            answered = await OutcomeAsync(async () => (await SessionReviews.TimedAsync(Session, tree, began, trees, CheckoutOf, null, CancellationToken.None)).Review);
        }

        Assert.Equal(chain, answered);
        SameCommands(chainStarts, [.. graph.Starts]);
        return graph;
    }

    /// <summary>An answer as its JSON, every field and patch in it; none as a refusal; a fault by its type and words.</summary>
    private static async Task<string> OutcomeAsync(Func<Task<SessionReview?>> read)
    {
        try
        {
            return await read() is { } review ? JsonSerializer.Serialize(review) : "refused: nothing reviewable";
        }
        catch (DriverException error)
        {
            return $"{error.GetType().Name}: {error.Message}";
        }
    }

    private static readonly string[] Programs = ["diff", "status", "merge-tree"];

    private static void SameCommands(IReadOnlyList<GitLaunched> chain, IReadOnlyList<GitLaunched> graph)
    {
        static string Spelled(GitLaunched start) => string.Join(' ', start.Arguments);
        static bool Program(GitLaunched start) => GitLaunch.Command(start.Arguments) is [var verb, ..] && Programs.Contains(verb);

        // Π: the same reads in the same order (P1.1: one chain in today's order). The offer's and the landed review's own reads are its.
        Assert.Equal(chain.Where(Program).Select(Spelled), graph.Where(Program).Select(Spelled));

        // The rest: the chain's starts all ran in the graph but the shared --show-toplevel; the graph's extras are speculative ones.
        var left = graph.Select(Spelled).ToList();
        foreach (var start in chain)
        {
            if (left.Remove(Spelled(start))) continue;
            Assert.True(start.Arguments is ["rev-parse", "--show-toplevel"], $"today's chain ran `git {Spelled(start)}`, which the graph did not");
        }

        foreach (var extra in left)
        {
            Assert.True(
                extra.StartsWith("rev-parse ", StringComparison.Ordinal) || extra.StartsWith("merge-base ", StringComparison.Ordinal)
                || extra.StartsWith("rev-list ", StringComparison.Ordinal) || extra.StartsWith("symbolic-ref ", StringComparison.Ordinal),
                $"the graph ran `git {extra}`, which today's chain did not, and which no speculative node of the graph runs");
        }
    }

    // 1-2. The tree gone: no folder, an empty one; with no landing the refusal, with one the landed review.
    [Fact]
    public async Task A_tree_with_no_folder_and_no_landing_is_refused_alike_with_no_start()
    {
        var trees = new SessionTrees(Home);

        var count = await SameAsync(trees, Path.Combine(Scratch, "nowhere"), "abc1234");

        Assert.Empty(count.Starts);
    }

    [Fact]
    public async Task An_empty_folder_reads_as_gone_alike()
    {
        var trees = new SessionTrees(Home);
        var empty = Directory.CreateDirectory(Path.Combine(Scratch, "empty")).FullName;

        await SameAsync(trees, empty, "abc1234");
    }

    // 3. A tree outside this home: no W, S or U, no range start, the committed review and H, no offer.
    [Fact]
    public async Task A_tree_outside_this_home_reads_alike()
    {
        var root = await RepositoryAsync("outside");
        var began = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await CommitAsync(root, "theirs.txt", "the person's\n", "the person's work");
        await File.WriteAllTextAsync(Path.Combine(root, "loose.txt"), "never read\n");

        var count = await SameAsync(new SessionTrees(Home), root, began);

        Assert.DoesNotContain(count.Starts, start => start.Node is "W" or "S" or "U" or "P" or "K");
    }

    // 4. W's T not top: a folder under the trees that is no repository of its own (git walks up).
    [Fact]
    public async Task A_folder_that_is_no_top_reads_alike()
    {
        var trees = new SessionTrees(Home);
        var folder = Path.Combine(Home, "trees", "games", "engine", Session);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "left.txt"), "a leftover\n");

        await SameAsync(trees, folder, "abc1234");
    }

    // 5. A blank base: dirty work only with H there; and a clean tree, the refusal (10).
    [Fact]
    public async Task A_blank_base_with_dirty_work_reads_it_alike()
    {
        var (_, trees, tree, _) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), "dirty\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "own.txt"), "changed\n");

        await SameAsync(trees, tree.Path, null);
    }

    [Fact]
    public async Task A_blank_base_on_a_clean_tree_is_refused_alike()
    {
        var (_, trees, tree, _) = await TreeAsync();

        await SameAsync(trees, tree.Path, "  ");
    }

    // 6. The range's own branches.
    [Fact]
    public async Task The_ordinary_review_with_untracked_files_and_the_offer_reads_alike()
    {
        var (_, trees, tree, began) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "a.txt"), "a\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "b.txt"), "b\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "big.txt"), new string('x', WorkingTree.UntrackedPatchLimit + 1));

        var count = await SameAsync(trees, tree.Path, began);

        // The guess's G2 and G3 beside G1, and E beside K: wasted where G1 named none and K held the record.
        Assert.Contains(count.Starts, start => start.Budget == GitBudget.Offer);
    }

    [Fact]
    public async Task Untracked_files_past_the_budget_read_alike()
    {
        var (_, trees, tree, began) = await TreeAsync();
        // Tracked patches spending most of the budget, then untracked files past it, listed with no read.
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "own.txt"), string.Concat(Enumerable.Repeat("a long tracked line of the session's work\n", 9_000)));
        for (var at = 0; at < 4; at++)
        {
            await File.WriteAllTextAsync(Path.Combine(tree.Path, $"u{at}.txt"), string.Concat(Enumerable.Repeat($"untracked {at}\n", 5_000)));
        }

        await SameAsync(trees, tree.Path, began);
    }

    [Fact]
    public async Task With_origins_head_set_and_dangling_the_guess_reads_alike()
    {
        var (root, trees, tree, began) = await TreeAsync();
        await GitAsync(root, "remote", "set-head", "origin", "main");
        await SameAsync(trees, tree.Path, began);

        await GitAsync(root, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/gone");
        await SameAsync(trees, tree.Path, began);
    }

    [Fact]
    public async Task With_master_only_or_no_line_the_guess_reads_alike()
    {
        var (root, trees, tree, began) = await TreeAsync();
        await GitAsync(root, "branch", "-m", "main", "master");
        await SameAsync(trees, tree.Path, began);

        await GitAsync(root, "branch", "-m", "master", "trunk");
        await SameAsync(trees, tree.Path, began);
    }

    [Fact]
    public async Task A_line_set_in_the_choices_reads_alike()
    {
        var (_, trees, tree, began) = await TreeAsync();
        DriverConfig.Load(DaorisHome.Entry(Home, HomeEntry.DriverConfig)).WithLine("engine", "main")
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

        var count = await SameAsync(trees, tree.Path, began);

        Assert.DoesNotContain(count.Starts, start => start.Node is "G1" or "G2" or "G3");
    }

    [Fact]
    public async Task A_record_that_is_no_id_and_an_unknown_one_read_alike()
    {
        var (_, trees, tree, _) = await TreeAsync();

        await SameAsync(trees, tree.Path, "HEAD~1");
        // 7. D not answering: git refuses a range from a commit it does not have; dirty work only, else the refusal.
        await SameAsync(trees, tree.Path, "0123456789abcdef0123456789abcdef01234567");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), "dirty\n");
        await SameAsync(trees, tree.Path, "0123456789abcdef0123456789abcdef01234567");
    }

    [Fact]
    public async Task Two_comparable_candidates_read_alike()
    {
        var (root, trees, tree, began) = await TreeAsync();
        await CommitAsync(root, "line.txt", "theirs\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");

        await SameAsync(trees, tree.Path, began);
    }

    /// <summary>REVIEWBASE3's chain step (ReviewBaseTests.cs:169-187), M answering; and its conflicting variant, M refusing.</summary>
    [Theory]
    [InlineData("parent.txt")]
    [InlineData("line.txt")]
    public async Task A_chain_step_that_merged_the_line_reads_alike(string parentFile)
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", parentFile, "the parent's\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", Session, from: "topic");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await CommitAsync(root, "line.txt", "theirs\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "-X", "ours", "main");

        var count = await SameAsync(trees, tree.Path, began);

        Assert.Contains(count.Starts, start => start.Node == "M");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_displaced_record_with_a_grown_base_reads_alike(bool grownHeld)
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "parent.txt", "first take\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", Session, from: "topic");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await GitAsync(root, "branch", "topic-again", "main");
        await CommitOnAsync(root, "topic-again", "parent.txt", "second take\n", "the parent's work, again");
        var again = (await GitAsync(root, "rev-parse", "topic-again")).Trim();
        await GitAsync(tree.Path, "rebase", "--quiet", "--onto", again, began);
        var grown = grownHeld ? again : (await GitAsync(root, "rev-parse", "main")).Trim()[..7] + "0";
        trees.Grown.Record(new GrownBranch("engine", Session, tree.Branch, "main", grown, "topic-again", DateTimeOffset.UtcNow));

        await SameAsync(trees, tree.Path, began);
    }

    // 8-9. D answering: with a landing standing (its review without changes, no offer); a detached HEAD named none.
    [Fact]
    public async Task D_answering_with_a_landing_standing_reads_alike()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var began = (await GitAsync(root, "rev-parse", "main")).Trim();
        var tree = await trees.OpenAsync(root, "engine", Session);
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        Assert.True((await trees.LandAsync(tree.Path, new LandingSubject(Session, "q1", "Work q1"))).Landed);
        // Only into the tree's own repository: a folder that is none would let git walk up into whatever encloses it.
        Assert.True(await WorkingTree.IsTopLevelAsync(tree.Path, CancellationToken.None));
        await CommitAsync(tree.Path, "after.txt", "after the landing\n", "work after its landing");

        var count = await SameAsync(trees, tree.Path, began, checkout: root);

        Assert.Contains(count.Starts, start => start.Budget == GitBudget.Landed);
        Assert.DoesNotContain(count.Starts, start => start.Budget == GitBudget.Offer);
    }

    [Fact]
    public async Task A_detached_head_reads_alike()
    {
        var (_, trees, tree, began) = await TreeAsync();
        await GitAsync(tree.Path, "checkout", "--quiet", "--detach");

        await SameAsync(trees, tree.Path, began);
    }

    // 11. The landed and gone reviews' own branches: standing, not holding its From, not ours, and gone.
    [Fact]
    public async Task The_landed_reviews_branches_read_alike()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await LandAsync(trees, root, "own.txt", "mine\n", new LandingSubject(Session, "q1", "Work q1"));
        var entry = trees.Recorded.Landing(Session)!;
        var gone = Path.Combine(Scratch, "removed");

        // Standing, its tree gone: the landed branch's changes.
        await SameAsync(trees, gone, entry.From, checkout: root);
        // No checkout here.
        await SameAsync(trees, gone, entry.From, checkout: null);

        // Not holding its recorded From: the branch's fork from the line is read instead.
        trees.Recorded.Record(entry with { From = "0123456789abcdef0123456789abcdef01234567" });
        await SameAsync(trees, gone, entry.From, checkout: root);

        // Not ours: the branch moved to a history without the landing's tip.
        await GitAsync(root, "branch", "-f", entry.Branch, "main~0");
        trees.Recorded.Record(entry);
        await SameAsync(trees, gone, entry.From, checkout: root);

        // Gone: the branch deleted, its commit still held; and recorded gone.
        await GitAsync(root, "branch", "-D", entry.Branch);
        await SameAsync(trees, gone, entry.From, checkout: root);
        trees.Recorded.Gone("engine", [entry.Branch]);
        await SameAsync(trees, gone, entry.From, checkout: root);
    }
}
