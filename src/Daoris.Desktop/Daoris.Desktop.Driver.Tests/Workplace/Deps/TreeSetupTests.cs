using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A dependency rule's <c>setup</c> (the tree-pool design §35.6) over real git and real processes, and the two traces of §34.5: this
/// repository's shape (a root whose setup writes git's own files and an output, beside a second dependency), and a group's member whose
/// rule became another kind. Fixtures live under the repository's gitignored <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSetupTests : IDisposable
{
    private const string InstallA = """
        const fs = require('fs');
        fs.mkdirSync('a/pkg', { recursive: true });
        fs.writeFileSync('a/pkg/a.js', fs.readFileSync('a.lock', 'utf8'));
        if (fs.readFileSync('a.lock', 'utf8').trim() === 'beside') fs.mkdirSync('docs-out', { recursive: true }), fs.writeFileSync('docs-out/index.txt', 'index');
        if (fs.readFileSync('a.lock', 'utf8').trim() === 'beside') fs.writeFileSync(require('child_process').execSync('git rev-parse --path-format=absolute --git-common-dir').toString().trim() + '/hooks/post-merge', '#!/bin/sh\n');
        """;

    private const string InstallB = """
        const fs = require('fs');
        fs.mkdirSync('b', { recursive: true });
        fs.writeFileSync('b/b.js', fs.readFileSync('b.lock', 'utf8'));
        if (fs.readFileSync('b.lock', 'utf8').trim() === 'touch-a') fs.appendFileSync('a/pkg/a.js', 'patched by b\n');
        """;

    private const string Setup = """
        const fs = require('fs'), cp = require('child_process');
        fs.appendFileSync('../setup-count.txt', 'ran ' + fs.readFileSync('setup.stamp', 'utf8').trim() + '\n');
        fs.mkdirSync('docs-out', { recursive: true });
        fs.writeFileSync('docs-out/index.txt', 'index');
        fs.writeFileSync(cp.execSync('git rev-parse --path-format=absolute --git-common-dir').toString().trim() + '/hooks/post-merge', '#!/bin/sh\n');
        """;

    private const string Tail = """
        { "kind": "dependency", "paths": ["b/"], "install": "node b.cjs", "key": ["b.lock"] },
        { "kind": "output", "paths": ["docs-out/"] } ] }
        """;

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-setup", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");
    private string Count => Path.Combine(_scratch, "setup-count.txt");

    public void Dispose()
    {
        DependencyCommands.Installing.Value = null;
        GitSeed.Discard(_scratch);
    }

    private static string RulesWith(string per) =>
        $$"""{ "rules": [ { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"], "setup": ["node setup.cjs"], "per": "{{per}}" },""" + Tail;

    private async Task MakeAsync(string rules, string a = "plain", string b = "plain")
    {
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await GitAsync(Root, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "a/\nb/\ndocs-out/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "a.cjs"), InstallA);
        await File.WriteAllTextAsync(Path.Combine(Root, "b.cjs"), InstallB);
        await File.WriteAllTextAsync(Path.Combine(Root, "setup.cjs"), Setup);
        await File.WriteAllTextAsync(Path.Combine(Root, "setup.stamp"), "one\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), rules);
        await File.WriteAllTextAsync(Path.Combine(Root, "a.lock"), a + "\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "b.lock"), b + "\n");
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "first");
    }

    private async Task<string> TreeAsync(string name)
    {
        var tree = Path.Combine(_scratch, name);
        await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    private Task<TreeFilesOutcome> StageAsync(string tree) => new TreeFilesStage(Home, Root, tree, "engine").RunAsync(CancellationToken.None);

    private static int Of(TreeFilesOutcome outcome, NoteCode code) => outcome.Said.SelectMany(each => each.Parts).Count(part => part.Code == code.Code);

    private int Ran() => File.Exists(Count) ? File.ReadAllLines(Count).Length : 0;

    [Fact]
    public async Task A_root_whose_setup_writes_outside_its_folder_installs_once_and_seals_and_the_next_tree_places_both_with_the_setup_run_once()
    {
        await MakeAsync(RulesWith("repository"));
        var first = await StageAsync(await TreeAsync("t1"));

        Assert.Equal(2, Of(first, StartNoteCodes.StartedDepsInstalled));
        Assert.Equal(2, DependencyStore.Entries(Home).Count);
        Assert.Equal(1, Ran());

        var second = await StageAsync(await TreeAsync("t2"));

        Assert.Equal(2, Of(second, StartNoteCodes.StartedDepsPlaced));
        Assert.Equal(0, Of(second, StartNoteCodes.StartedDepsInstalled));
        Assert.Equal(1, Ran());
    }

    [Fact]
    public async Task A_setup_per_repository_runs_again_once_a_file_it_names_changes_and_a_setup_per_tree_runs_in_each_tree()
    {
        await MakeAsync(RulesWith("repository"));
        await StageAsync(await TreeAsync("t1"));
        await File.AppendAllTextAsync(Path.Combine(Root, "setup.cjs"), "// changed\n");
        await GitAsync(Root, "commit", "-q", "-am", "setup changed");

        await StageAsync(await TreeAsync("t2"));
        await StageAsync(await TreeAsync("t3"));

        Assert.Equal(2, Ran());

        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), RulesWith("tree"));
        await GitAsync(Root, "commit", "-q", "-am", "per tree");
        await StageAsync(await TreeAsync("t4"));
        await StageAsync(await TreeAsync("t5"));

        Assert.Equal(4, Ran());
    }

    [Fact]
    public async Task A_setup_that_fails_is_said_and_runs_again_at_the_next_tree()
    {
        await MakeAsync(RulesWith("repository"));
        await File.WriteAllTextAsync(Path.Combine(Root, "setup.cjs"), "process.exit(3);\n");
        await GitAsync(Root, "commit", "-q", "-am", "failing");

        var failed = await StageAsync(await TreeAsync("t1"));

        Assert.Equal(1, Of(failed, StartNoteCodes.StartedSetupFailed));
        await File.WriteAllTextAsync(Path.Combine(Root, "setup.cjs"), Setup);
        await GitAsync(Root, "commit", "-q", "-am", "mended");
        await StageAsync(await TreeAsync("t2"));
        Assert.Equal(1, Ran());
    }

    [Fact]
    public async Task An_install_that_wrote_an_output_beside_it_is_recorded_installed_unsealed_and_kept_in_its_tree()
    {
        await MakeAsync(RulesWith("repository"), a: "beside");
        var tree = await TreeAsync("t1");

        var made = await StageAsync(tree);

        Assert.Equal(1, Of(made, StartNoteCodes.StartedDepsCommon));
        Assert.Equal("installed", TreeFilesRecord.Read(Home, tree)!.Of("a/")!.How);
        Assert.Equal(["docs-out/"], TreeFilesRecord.Read(Home, tree)!.Of("a/")!.Others);
        TreeFilesStep.Hand(Home, tree);
        var worked = await StageAsync(tree);
        Assert.True(Of(worked, StartNoteCodes.StartedDepsKept) > 0);
    }

    [Fact]
    public async Task A_group_member_whose_path_another_rule_now_keeps_is_not_removed_with_the_group()
    {
        await MakeAsync("""{ "rules": [ { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"] }, """ + Tail, b: "touch-a");
        var tree = await TreeAsync("t1");
        await StageAsync(tree);
        Assert.Single(TreeFilesRecord.Read(Home, tree)!.Groups);

        await File.WriteAllTextAsync(Path.Combine(tree, "daoris.files.json"),
            """{ "rules": [ { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"] }, { "kind": "intermediate", "paths": ["b/"] } ] }""");
        await GitAsync(tree, "commit", "-q", "-am", "b is a build state now");
        TreeFilesRecord.Mark(Home, tree);
        await StageAsync(tree);

        Assert.True(Directory.Exists(Path.Combine(tree, "b")), "a folder another rule keeps is that rule's, not a removed dependency");
        Assert.Empty(TreeFilesRecord.Read(Home, tree)!.Groups);
    }
}
