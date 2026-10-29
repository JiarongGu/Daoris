using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1d (D215 point 6, the integration design §4.1's <c>merge</c>, §4.3, §5.2 step 4): a line rule of the line's
/// <c>daoris.gates.json</c>, <c>{ paths, line, run }</c>, settles a landing's replay that conflicts only on lines its pattern matches,
/// by its writer recomputing them from the replayed tree, and only where the person accepted that declaration for integration and it
/// is still the one at the line's tip. Its command is read from the accepted blob alone, so a session never runs a command its own
/// branch declared. Unaccepted, changed, or declared only by the session, the conflict stays the session's, refused with the rebase
/// words; a writer that fails keeps no side's copy and refuses the same way. Real git in a scratch repository, and a node writer, so
/// this is the suite's Process half. The merge itself is <c>LineRulesTests</c>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandLineRuleLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    private const string Run = "node tools/status.mjs";

    /// <summary>The writer: the status line names the top's <c>.txt</c> files, so each side adding one disagrees on it and only the replayed tree holds both.</summary>
    private const string Writer = """
        import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
        const names = readdirSync('.').filter((name) => name.endsWith('.txt')).sort();
        const text = readFileSync('docs/STATUS.md', 'utf8');
        writeFileSync('docs/STATUS.md', text.replace(/^Status: .*$/m, `Status: ${names.join(', ')}`));
        """;

    private static string Declared(string run = Run) =>
        $"{{ \"integration\": {{ \"merge\": [ {{ \"paths\": \"docs/STATUS.md\", \"line\": \"^Status: \", \"run\": \"{run}\" }} ] }} }}\n";

    private static string Status(string line, string tail = "") => $"# Status\n\nStatus: {line}\n\nNotes.\n{tail}";

    /// <summary>The line's status page, its writer and its declaration; git's blob id of that declaration.</summary>
    private static async Task<string> DeclareAsync(string root, string declaration)
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "STATUS.md"), Status("shared.txt"));
        await File.WriteAllTextAsync(Path.Combine(root, "tools", "status.mjs"), Writer);
        await CommitAsync(root, LandedGates.File, declaration, "the status line and its rule");
        return (await GitAsync(root, "rev-parse", $"HEAD:{LandedGates.File}")).Trim();
    }

    /// <summary>The owner's acceptance of <paramref name="gates"/> for the repository's integration, as the window's door writes it.</summary>
    private void AcceptIntegration(string gates)
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        DriverConfig.Load(path).WithIntegrationGates("engine", gates).Save(path);
    }

    /// <summary>The work adds <c>work.txt</c> and a note, naming its file on the status line; the line adds <c>zline.txt</c> and names its own.</summary>
    private async Task<TreeOpened> ConflictOnTheLineAsync(string root, SessionTrees trees)
    {
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, Path.Combine("docs", "STATUS.md"), Status("shared.txt, work.txt", "The work's note.\n"), "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "zline.txt"), "the line\n");
        await CommitAsync(root, Path.Combine("docs", "STATUS.md"), Status("shared.txt, zline.txt"), "the line's own");
        return tree;
    }

    /// <summary>
    /// Accepted and still the line's: the plan says no conflict, and the press lands the work replayed with the work's note, its status
    /// line written again from the replayed tree in a commit on top, and says so.
    /// </summary>
    [Fact]
    public async Task A_conflict_only_on_a_declared_line_is_settled_by_its_accepted_writer_and_lands_replayed()
    {
        var root = await RepositoryAsync("engine");
        AcceptIntegration(await DeclareAsync(root, Declared()));
        var trees = new SessionTrees(Home);
        var tree = await ConflictOnTheLineAsync(root, trees);
        var line = await HeadAsync(root);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains($" The declared line rule settled `docs/STATUS.md`, written again from the replayed tree by `{Run}`.", landed.Message);
        Assert.Equal(Status("shared.txt, work.txt, zline.txt", "The work's note.\n"), await GitAsync(root, "show", $"{Branch}:docs/STATUS.md"));
        Assert.Equal("Write docs/STATUS.md again from the replayed tree", (await GitAsync(root, "log", "-1", "--format=%s", Branch)).Trim());
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~2")).Trim());
    }

    /// <summary>Declared on the line and never accepted for integration: no command runs, and the conflict is the session's.</summary>
    [Fact]
    public async Task A_line_rule_whose_declaration_was_never_accepted_settles_nothing()
    {
        var root = await RepositoryAsync("engine");
        await DeclareAsync(root, Declared());
        var trees = new SessionTrees(Home);
        var tree = await ConflictOnTheLineAsync(root, trees);
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in docs/STATUS.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>Accepted, and the line's declaration changed since: the new one waits for the owner, and nothing it or the old one declares runs.</summary>
    [Fact]
    public async Task A_line_rule_whose_declaration_changed_since_its_acceptance_settles_nothing()
    {
        var root = await RepositoryAsync("engine");
        AcceptIntegration(await DeclareAsync(root, Declared()));
        await CommitAsync(root, LandedGates.File, Declared().Replace("{ \"integration\"", "{ \"note\": 1, \"integration\"", StringComparison.Ordinal),
            "the declaration changed");
        var trees = new SessionTrees(Home);
        var tree = await ConflictOnTheLineAsync(root, trees);
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in docs/STATUS.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>
    /// A session that declares the rule in its own work, whose blob the owner even accepted, runs nothing: the acceptance is compared
    /// with the declaration at the line's tip, which declares none.
    /// </summary>
    [Fact]
    public async Task A_line_rule_the_session_declared_itself_is_not_run()
    {
        var root = await RepositoryAsync("engine");
        await DeclareAsync(root, "{ \"gates\": [] }\n");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, LandedGates.File), Declared());
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, Path.Combine("docs", "STATUS.md"), Status("shared.txt, work.txt"), "the work");
        AcceptIntegration((await GitAsync(tree.Path, "rev-parse", $"HEAD:{LandedGates.File}")).Trim());
        await File.WriteAllTextAsync(Path.Combine(root, "zline.txt"), "the line\n");
        await CommitAsync(root, Path.Combine("docs", "STATUS.md"), Status("shared.txt, zline.txt"), "the line's own");
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in docs/STATUS.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>
    /// Accepted, and its writer fails: no rule keeps one side's copy, so the replay is not kept and the landing is refused as a conflict,
    /// naming the file and the writer's failure.
    /// </summary>
    [Fact]
    public async Task A_line_rule_whose_writer_fails_keeps_no_sides_copy_and_is_refused()
    {
        var root = await RepositoryAsync("engine");
        AcceptIntegration(await DeclareAsync(root, Declared("node tools/missing.mjs")));
        var trees = new SessionTrees(Home);
        var tree = await ConflictOnTheLineAsync(root, trees);
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("docs/STATUS.md", refused.Conflict!.Says);
        Assert.Contains("`node tools/missing.mjs`", refused.Conflict.Says);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.False(await BranchAsync(root, Branch), "nothing is made");
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>What a refused landing must leave as it was, as <c>LandRebaseLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
