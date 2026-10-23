using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-ii's order-sensitive fixture (the git-app design P1.7, Codex R4-1): the tree's files carry a textconv that logs each call
/// and writes the count of its calls into its output, a clean filter that logs each call, and a merge driver that logs each call and
/// writes its count into the merge. The log and the counter are reset before today's chain reads and before the graph does; the two
/// answers equal byte for byte and the two logs line for line, so the graph ran the same programs, as often, in the same order.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewGraphProgramsTests : LandedFixture
{
    private const string Session = "aurora";

    private string Programs => Path.Combine(Scratch, "programs");

    private string Log => Path.Combine(Programs, "log");

    private string Counter => Path.Combine(Programs, "count");

    /// <summary>The three programs, each a shell script git runs, logging to the fixture and counting its calls there.</summary>
    private async Task ConfigureAsync(string root)
    {
        Directory.CreateDirectory(Programs);
        var dir = Programs.Replace('\\', '/');
        const string count = "n=$(cat \"$D/count\" 2>/dev/null || echo 0); n=$((n+1)); echo $n > \"$D/count\"";
        await File.WriteAllTextAsync(Path.Combine(Programs, "textconv.sh"), $"D='{dir}'\n{count}\necho \"textconv $n\" >> \"$D/log\"\necho \"conversion $n\"\ncat \"$1\"\n");
        await File.WriteAllTextAsync(Path.Combine(Programs, "clean.sh"), $"D='{dir}'\n{count}\necho \"clean $1\" >> \"$D/log\"\ncat\n");
        await File.WriteAllTextAsync(
            Path.Combine(Programs, "merge.sh"), $"D='{dir}'\n{count}\necho \"merge $4\" >> \"$D/log\"\necho \"merged by the driver, call $n\" > \"$2\"\n");
        await GitAsync(root, "config", "diff.logged.textconv", $"sh '{dir}/textconv.sh'");
        await GitAsync(root, "config", "filter.logged.clean", $"sh '{dir}/clean.sh' %f");
        await GitAsync(root, "config", "merge.logged.driver", $"sh '{dir}/merge.sh' %O %A %B %P");
        await GitAsync(root, "config", "merge.logged.name", "a logged merge");
    }

    private void Reset()
    {
        File.Delete(Log);
        File.Delete(Counter);
    }

    private string Logged() => File.Exists(Log) ? File.ReadAllText(Log) : "";

    /// <summary>Today's chain, then the graph, each from a reset log: the answers and the logs equal.</summary>
    private async Task SameAsync(SessionTrees trees, string tree, string began)
    {
        Reset();
        var chain = JsonSerializer.Serialize(
            await SessionReviews.ChainReadAsync(Session, tree, began, trees, (_, _) => Task.FromResult<string?>(null), CancellationToken.None));
        var chainLog = Logged();

        Reset();
        var graph = JsonSerializer.Serialize(
            (await SessionReviews.TimedAsync(Session, tree, began, trees, (_, _) => Task.FromResult<string?>(null), null, CancellationToken.None)).Review);
        var graphLog = Logged();

        Assert.Contains("textconv", chainLog, StringComparison.Ordinal);
        Assert.Equal(chain, graph);
        Assert.Equal(chainLog.Split('\n'), graphLog.Split('\n'));
    }

    /// <summary>A chain's step whose parent and the line both changed one file: the merge driver resolves it, in the step and in M.</summary>
    private async Task<(SessionTrees Trees, TreeOpened Tree, string Began, string Root)> StepAsync()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await ConfigureAsync(root);
        await CommitAsync(root, ".gitattributes", "*.txt diff=logged filter=logged merge=logged\n", "programs for text");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "shared.txt", "the parent's take\n", "the parent's work");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", Session, from: "topic");
        var began = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        return (trees, tree, began, root);
    }

    [Fact]
    public async Task A_chain_step_with_textconv_filters_and_a_merge_driver_runs_the_same_programs_in_the_same_order()
    {
        var (trees, tree, began, root) = await StepAsync();
        await CommitAsync(root, "shared.txt", "the line's take\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");
        // Tracked, uncommitted and untracked text: W, S and each U run the clean filter and textconv.
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "own.txt"), "mine, changed\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "new-a.txt"), "untracked a\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "new-b.txt"), "untracked b\n");

        await SameAsync(trees, tree.Path, began);
        Assert.Contains("merge ", Logged(), StringComparison.Ordinal);
    }

    /// <summary>The record displaced (round 4's speculative committed range): no conversion runs that the chain did not run.</summary>
    [Fact]
    public async Task A_displaced_record_runs_no_conversion_the_chain_did_not()
    {
        var (trees, tree, began, root) = await StepAsync();
        await GitAsync(root, "branch", "topic-again", "main");
        await CommitOnAsync(root, "topic-again", "parent.txt", "second take\n", "the parent's work, again");
        var again = (await GitAsync(root, "rev-parse", "topic-again")).Trim();
        await GitAsync(tree.Path, "rebase", "--quiet", "--onto", again, began);
        trees.Grown.Record(new GrownBranch("engine", Session, tree.Branch, "main", again, "topic-again", DateTimeOffset.UtcNow));
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), "untracked\n");

        await SameAsync(trees, tree.Path, began);
    }
}
