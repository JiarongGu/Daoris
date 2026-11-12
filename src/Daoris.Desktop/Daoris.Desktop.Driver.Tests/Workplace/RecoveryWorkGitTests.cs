using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: the census says, for each recovery ref nothing holds, whether its work already reached the repository's line
/// (every commit by patch, else every file it changed by content), or holds work the line lacks; <c>--unheld</c> drops the first two kinds and
/// keeps the last.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RecoveryWorkGitTests : LandedFixture
{
    private static readonly IReadOnlySet<string> NoneMade = new HashSet<string>();

    private const string Prefix = "refs/daoris/discarded/";

    private static string Name(string kind) => $"{Prefix}daoris/s-{kind}";

    /// <summary>A branch from <c>main</c>'s tip that makes <paramref name="files"/> in one commit each, left as a recovery ref alone.</summary>
    private static async Task KeptAsync(string root, string kind, params (string File, string Content)[] files)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", $"work-{kind}", "main");
        foreach (var (file, content) in files) await CommitAsync(root, file, content, $"{kind}: {file}");
        await GitAsync(root, "update-ref", Name(kind), (await GitAsync(root, "rev-parse", "HEAD")).Trim());
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "branch", "-D", $"work-{kind}");
    }

    /// <summary>A repository whose four recovery refs stand as the census meets them.</summary>
    private async Task<string> FourAsync()
    {
        var root = await RepositoryAsync("engine");
        await KeptAsync(root, "picked", ("picked.txt", "picked\n"));
        await KeptAsync(root, "squashed", ("one.txt", "one\n"), ("two.txt", "two\n"));
        await KeptAsync(root, "passed", ("shared.txt", "the ref's words\n"));
        await KeptAsync(root, "lacking", ("lacking.txt", "x\n"), ("more.txt", "y\n"));

        await GitAsync(root, "cherry-pick", Name("picked"));
        await GitAsync(root, "merge", "--squash", Name("squashed"));
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "squash");
        await CommitAsync(root, "shared.txt", "the line's later words\n", "the line moves past");
        return root;
    }

    [Fact]
    public async Task Each_ref_is_read_as_on_the_line_by_patch_by_content_or_holding_work_the_line_lacks()
    {
        var root = await FourAsync();

        var picked = await RecoveryWorks.ReadAsync(root, "main", Name("picked"), CancellationToken.None);
        var squashed = await RecoveryWorks.ReadAsync(root, "main", Name("squashed"), CancellationToken.None);
        var passed = await RecoveryWorks.ReadAsync(root, "main", Name("passed"), CancellationToken.None);
        var lacking = await RecoveryWorks.ReadAsync(root, "main", Name("lacking"), CancellationToken.None);

        Assert.Equal(RecoveryHolding.ByPatch, picked.Holding);
        Assert.Equal(RecoveryHolding.ByContent, squashed.Holding);
        Assert.Equal(RecoveryHolding.ByContent, passed.Holding);
        Assert.Equal((RecoveryHolding.Lacks, 2, 2), (lacking.Holding, lacking.Commits, lacking.Files));
    }

    [Fact]
    public async Task The_plan_says_what_each_refs_work_is_to_the_line()
    {
        var root = await FourAsync();

        var plan = await RecoveryDropping.PlanAsync(Home, root, "engine", "main", CancellationToken.None);

        Assert.Contains(plan, line => line.Contains("would delete") && line.Contains(Name("picked")) && line.Contains("1 commit") && line.Contains("by patch"));
        Assert.Contains(plan, line => line.Contains("would delete") && line.Contains(Name("squashed")) && line.Contains("2 commits") && line.Contains("holds on `main` what the ref holds"));
        Assert.Contains(plan, line => line.Contains("would delete") && line.Contains(Name("passed")) && line.Contains("moved past"));
        Assert.Contains(plan, line => line.Contains("would keep") && line.Contains(Name("lacking")) && line.Contains("2 commits and 2 files"));
    }

    [Fact]
    public async Task Unheld_drops_the_refs_whose_work_the_line_holds_and_keeps_the_rest_with_why()
    {
        var root = await FourAsync();
        var lacking = (await GitAsync(root, "rev-parse", Name("lacking"))).Trim();

        var drops = await RecoveryDropping.DropUnheldAsync(root, "engine", NoneMade, "main", CancellationToken.None);

        Assert.Equal([Name("passed"), Name("picked"), Name("squashed")], drops.Dropped.Select(each => each.Ref).Order(StringComparer.Ordinal));
        var kept = Assert.Single(drops.Skipped);
        Assert.Equal(Name("lacking"), kept.Ref);
        Assert.Contains("lacks", kept.Why);
        Assert.Equal(lacking, (await GitAsync(root, "rev-parse", Name("lacking"))).Trim());
        Assert.Equal(Name("lacking"), (await GitAsync(root, "for-each-ref", "--format=%(refname)", Prefix)).Trim());
    }

    [Fact]
    public async Task A_ref_named_by_hand_drops_whatever_the_line_holds_of_it()
    {
        var root = await FourAsync();

        var drops = await RecoveryDropping.DropAsync(root, "engine", NoneMade, [new RecoveryDrop(Name("lacking"), null)], CancellationToken.None);

        Assert.Equal([Name("lacking")], drops.Dropped.Select(each => each.Ref));
    }

    [Fact]
    public async Task With_no_line_known_nothing_is_dropped_and_the_plan_says_why()
    {
        var root = await FourAsync();

        var drops = await RecoveryDropping.DropUnheldAsync(root, "engine", NoneMade, null, CancellationToken.None);
        var plan = await RecoveryDropping.PlanAsync(Home, root, "engine", null, CancellationToken.None);

        Assert.Empty(drops.Dropped);
        Assert.Equal(4, drops.Skipped.Count);
        Assert.All(drops.Skipped, each => Assert.Contains("line", each.Why));
        Assert.Contains(plan, line => line.Contains("would keep") && line.Contains(Name("picked")) && line.Contains("line"));
    }

    [Fact]
    public async Task The_line_is_the_one_set_else_the_conventional_branch_never_a_remote_head_on_a_feature_branch()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "main")).Trim();
        await GitAsync(root, "update-ref", "refs/remotes/origin/feature/x", tip);
        await GitAsync(root, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/feature/x");
        await GitAsync(root, "branch", "trunk", "main");

        Assert.Equal("trunk", await RecoveryWorks.LineAsync(root, "trunk", CancellationToken.None));
        Assert.Equal("main", await RecoveryWorks.LineAsync(root, null, CancellationToken.None));
        Assert.Null(await RecoveryWorks.LineAsync(root, "gone", CancellationToken.None));
    }
}
