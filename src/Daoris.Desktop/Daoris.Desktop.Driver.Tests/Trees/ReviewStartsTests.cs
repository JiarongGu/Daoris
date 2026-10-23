using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-i's proof on today's chain (D202 point 2, the git-app design P1.4, P1.8 row 1): Review's read of a session tree, counted
/// through the one seam, starts <c>12 + I + M + Uᵣ</c> git before the offer, and the line's guesses and both <c>--show-toplevel</c>s
/// besides, in today's order, each with today's arguments (<see cref="GitLaunchTests.Today"/>). Real git in scratch repositories,
/// read through <see cref="SessionReviews"/>, the route's own code.
/// </summary>
/// <remarks>
/// The fixtures' origin is added after its repository, so it has no <c>refs/remotes/origin/HEAD</c> and git's guess at the line asks
/// origin's <c>HEAD</c> and then <c>main</c>: two guess starts (G1, G2), or one where origin's <c>HEAD</c> is set.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewStartsTests : LandedFixture
{
    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Began)> TreeAsync()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        return (root, trees, tree, began);
    }

    private static async Task<(SessionReview? Review, GitCount Starts)> ReadAsync(SessionTrees trees, TreeOpened tree, string began) =>
        await SessionReviews.TimedAsync("aurora", tree.Path, began, trees, (_, _) => Task.FromResult<string?>(null), null, CancellationToken.None);

    /// <summary>The review's own starts, untracked tail included, by node in the order the seam saw them.</summary>
    private static string[] Nodes(GitCount count) =>
        [.. count.Starts.Where(start => start.Budget is GitBudget.Review or GitBudget.Untracked).Select(start => start.Node)];

    [Fact]
    public async Task The_ordinary_review_starts_twelve_and_the_lines_two_guesses_before_the_offer()
    {
        var (_, trees, tree, began) = await TreeAsync();

        var (review, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["own.txt"], review!.Diff!.Files.Select(file => file.Path));
        // 12: T W S, K P B1 B2 N1 N2, T D, H; and the line's guess, G1 (origin's HEAD, unset) then G2 (main).
        Assert.Equal(["T", "W", "S", "K", "P", "G1", "G2", "B1", "B2", "N1", "N2", "T", "D", "H"], Nodes(count));
        Assert.Equal(12 + 2, count.In(GitBudget.Review));
        // The work is beyond the line and nothing landed it, so the offer reads after, in a budget of its own.
        Assert.NotNull(review.Offer);
        Assert.True(count.In(GitBudget.Offer) >= 6, $"the offer started {count.In(GitBudget.Offer)}");
        Assert.Equal(0, count.In(GitBudget.Untracked));
        TodaysArguments(count);
    }

    [Fact]
    public async Task With_origins_head_set_the_line_is_guessed_in_one_start()
    {
        var (root, trees, tree, began) = await TreeAsync();
        await GitAsync(root, "remote", "set-head", "origin", "main");

        var (_, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["T", "W", "S", "K", "P", "G1", "B1", "B2", "N1", "N2", "T", "D", "H"], Nodes(count));
    }

    [Fact]
    public async Task Each_untracked_file_read_is_one_start_of_the_tail_after_status()
    {
        var (_, trees, tree, began) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "a.txt"), "a\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "b.txt"), "b\n");

        var (review, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["a.txt", "b.txt"], review!.Uncommitted!.Files.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Equal(["T", "W", "S", "U", "U", "K", "P", "G1", "G2", "B1", "B2", "N1", "N2", "T", "D", "H"], Nodes(count));
        Assert.Equal(2, count.In(GitBudget.Untracked));
        TodaysArguments(count);
    }

    /// <summary>A tree that merged the line: the line's base and the record's are two comparable candidates, so I is asked.</summary>
    [Fact]
    public async Task Two_comparable_candidates_add_the_independence_read()
    {
        var (root, trees, tree, began) = await TreeAsync();
        await CommitAsync(root, "line.txt", "theirs\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");

        var (_, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["T", "W", "S", "K", "P", "G1", "G2", "B1", "B2", "I", "N1", "N2", "T", "D", "H"], Nodes(count));
        TodaysArguments(count);
    }

    /// <summary>REVIEWBASE3's chain step: the record's base and the line's incomparable, so I and M are asked.</summary>
    [Fact]
    public async Task A_chain_step_that_merged_the_line_adds_the_independence_read_and_the_merge()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "parent.txt", "the parent's\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: "topic");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await CommitAsync(root, "line.txt", "theirs\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");

        var (review, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["own.txt"], review!.Diff!.Files.Select(file => file.Path));
        Assert.Equal(["T", "W", "S", "K", "P", "G1", "G2", "B1", "B2", "I", "M", "N1", "N2", "T", "D", "H"], Nodes(count));
        TodaysArguments(count);
    }

    /// <summary>The record's base displaced by a replay, the branch's grown base in its place: E and the second K are asked.</summary>
    [Fact]
    public async Task A_displaced_record_asks_the_branch_and_its_grown_base()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "parent.txt", "first take\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: "topic");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await GitAsync(root, "branch", "topic-again", "main");
        await CommitOnAsync(root, "topic-again", "parent.txt", "second take\n", "the parent's work, again");
        var again = (await GitAsync(root, "rev-parse", "topic-again")).Trim();
        await GitAsync(tree.Path, "rebase", "--quiet", "--onto", again, began);
        trees.Grown.Record(new GrownBranch("engine", "aurora", tree.Branch, "main", again, "topic-again", DateTimeOffset.UtcNow));

        var (_, count) = await ReadAsync(trees, tree, began);

        Assert.Equal(["T", "W", "S", "K", "E", "K", "P", "G1", "G2", "B1", "B2", "I", "N1", "N2", "T", "D", "H"], Nodes(count));
        TodaysArguments(count);
    }

    /// <summary>
    /// The argument census on the real chain: each of the review's starts reached git with exactly the arguments the census of today's
    /// call sites lists for its node, ids and the untracked file's path aside.
    /// </summary>
    private static void TodaysArguments(GitCount count)
    {
        var today = GitLaunchTests.Today.Select(row => ((string)row[2], Normal((string[])row[0]))).ToList();
        foreach (var start in count.Starts.Where(start => start.Budget is GitBudget.Review or GitBudget.Untracked))
        {
            var spelled = Normal(start.Arguments);
            Assert.True(
                today.Any(row => row.Item1 == start.Node && row.Item2.SequenceEqual(spelled)),
                $"{start.Node} ran `git {string.Join(' ', start.Arguments)}`, which today's census does not list");
        }
    }

    private static string[] Normal(IReadOnlyList<string> arguments) =>
    [
        .. arguments.Select((word, at) => at == arguments.Count - 1 && arguments.Contains("--no-index")
            ? "<path>"
            : Regex.Replace(word, "(?<![0-9a-zA-Z])[0-9a-f]{8,40}(?![0-9a-zA-Z])", "<id>")),
    ];
}
