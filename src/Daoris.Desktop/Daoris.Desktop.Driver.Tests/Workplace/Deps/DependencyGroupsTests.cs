using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The files stage's groups, companions, other writes, git's common folder and a terminal's visit, over real git and real processes:
/// a repository whose own rules name two dependencies, `a/` then `b/`, each installed by a tracked script whose behaviour its tracked
/// lockfile chooses, and an output `gen/` another rule names. Fixtures live under the repository's gitignored `_fixtures/`.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DependencyGroupsTests : IDisposable
{
    private const string InstallA = """
        const fs = require('fs'), cp = require('child_process');
        const how = fs.readFileSync('a.lock', 'utf8').trim();
        fs.mkdirSync('a/pkg', { recursive: true });
        fs.writeFileSync('a/pkg/a.js', how + '\n');
        if (how === 'gen') fs.mkdirSync('gen', { recursive: true }), fs.writeFileSync('gen/made.txt', 'made');
        if (how === 'side') fs.mkdirSync('side', { recursive: true }), fs.writeFileSync('side/x.txt', 'beside');
        if (how === 'common') fs.writeFileSync(cp.execSync('git rev-parse --path-format=absolute --git-common-dir').toString().trim() + '/hooks/post-merge', '#!/bin/sh\n');
        if (how === 'slow') setTimeout(() => {}, 120000);
        """;

    private const string InstallB = """
        const fs = require('fs');
        const how = fs.readFileSync('b.lock', 'utf8').trim();
        fs.mkdirSync('b', { recursive: true });
        fs.writeFileSync('b/b.js', how + '\n');
        if (how === 'touch-a') fs.appendFileSync('a/pkg/a.js', 'patched by b\n');
        """;

    private const string BothRules = """
        { "rules": [
          { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"] },
          { "kind": "dependency", "paths": ["b/"], "install": "node b.cjs", "key": ["b.lock"] },
          { "kind": "output", "paths": ["gen/"] } ] }
        """;

    private const string OnlyA = """
        { "rules": [ { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"] } ] }
        """;

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-groups", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose()
    {
        DependencyCommands.Installing.Value = null;
        GitSeed.Discard(_scratch);
    }

    private async Task MakeAsync(string a, string b)
    {
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await GitAsync(Root, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "a/\nb/\ngen/\nside/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "a.cjs"), InstallA);
        await File.WriteAllTextAsync(Path.Combine(Root, "b.cjs"), InstallB);
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), BothRules);
        await File.WriteAllTextAsync(Path.Combine(Root, "README.md"), "# engine\n");
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

    private static Noted Said(TreeFilesOutcome outcome, NoteCode code, string path) =>
        Assert.Single(outcome.Said, each => each.Parts.Any(part => part.Code == code.Code && part.Values.Any(value => value.Key == "path" && Equals(value.Value, path))));

    private static string? Why(Noted line) => line.Parts[0].Values.FirstOrDefault(value => value.Key == "why").Value as string;

    private static string Text(params string[] parts) => string.Concat(parts.Select(part => part + "\n"));

    [Fact]
    public async Task An_install_that_changes_an_earlier_placement_groups_the_two_and_a_member_the_line_removed_stays_absent()
    {
        await MakeAsync("plain", "touch-a");
        var tree = await TreeAsync("t1");

        var made = await StageAsync(tree);

        Assert.Equal(DepsWhy.Group, Why(Said(made, StartNoteCodes.StartedDepsUnstored, "b")));
        var group = Assert.Single(TreeFilesRecord.Read(Home, tree)!.Groups);
        Assert.Equal(["a/", "b/"], group.Members);
        Assert.Single(DependencyStore.Entries(Home));

        TreeFilesStep.Hand(Home, tree);
        TreeSetAside.DeleteNeverFollowing(Path.Combine(tree, "a"));
        var resumed = await StageAsync(tree);

        Assert.Equal(DepsWhy.Grouped, Why(Said(resumed, StartNoteCodes.StartedDepsLeft, "a")));
        Assert.False(Directory.Exists(Path.Combine(tree, "a")), "a group's member is never placed by its own key");
    }

    [Fact]
    public async Task A_removed_rule_s_effect_on_an_earlier_placement_is_removed_and_its_member_installed_again()
    {
        await MakeAsync("plain", "touch-a");
        var tree = await TreeAsync("t1");
        await StageAsync(tree);
        Assert.Equal(Text("plain", "patched by b"), await File.ReadAllTextAsync(Path.Combine(tree, "a", "pkg", "a.js")));

        await File.WriteAllTextAsync(Path.Combine(tree, "daoris.files.json"), OnlyA);
        await GitAsync(tree, "commit", "-q", "-am", "b's rule removed");
        TreeFilesRecord.Mark(Home, tree);
        var again = await StageAsync(tree);

        Assert.Equal(Text("plain"), await File.ReadAllTextAsync(Path.Combine(tree, "a", "pkg", "a.js")));
        Assert.False(Directory.Exists(Path.Combine(tree, "b")));
        Assert.DoesNotContain(again.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsPlaced.Code));
        Assert.Empty(TreeFilesRecord.Read(Home, tree)!.Groups);
    }

    [Fact]
    public async Task An_install_s_output_another_rule_names_removed_is_installed_again_before_the_hand()
    {
        await MakeAsync("gen", "plain");
        var tree = await TreeAsync("t1");
        var made = await StageAsync(tree);
        Assert.Equal(DepsWhy.Beside, Why(Said(made, StartNoteCodes.StartedDepsUnstored, "a")));
        Assert.Equal(["gen/"], TreeFilesRecord.Read(Home, tree)!.Of("a/")!.Others);

        TreeSetAside.DeleteNeverFollowing(Path.Combine(tree, "gen"));
        TreeFilesRecord.Mark(Home, tree);
        var again = await StageAsync(tree);

        Assert.True(File.Exists(Path.Combine(tree, "gen", "made.txt")), "its producer ran again before the hand");
        Said(again, StartNoteCodes.StartedDepsUnstored, "a");
        Assert.DoesNotContain(again.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsKept.Code && part.Values.Any(value => Equals(value.Value, "a"))));
    }

    [Fact]
    public async Task A_changed_companion_installs_the_dependency_again_in_an_unworked_tree()
    {
        await MakeAsync("side", "plain");
        var tree = await TreeAsync("t1");
        await StageAsync(tree);
        Assert.Equal("side/", Assert.Single(TreeFilesRecord.Read(Home, tree)!.Of("a/")!.Companions).Path);

        await File.WriteAllTextAsync(Path.Combine(tree, "side", "x.txt"), "changed");
        TreeFilesRecord.Mark(Home, tree);
        var again = await StageAsync(tree);

        Said(again, StartNoteCodes.StartedDepsUnstored, "a");
        Assert.Equal("beside", await File.ReadAllTextAsync(Path.Combine(tree, "side", "x.txt")));
    }

    [Fact]
    public async Task A_worked_tree_s_hit_beside_a_dependency_the_line_changed_is_left_absent_and_said()
    {
        await MakeAsync("plain", "plain");
        var tree = await TreeAsync("t1");
        var made = await StageAsync(tree);
        Assert.True(DependencyStore.Entries(Home).Count == 2, string.Join(" | ", made.Said.Select(each => each.Note)));
        TreeFilesStep.Hand(Home, tree);

        await File.WriteAllTextAsync(Path.Combine(tree, "a", "pkg", "a.js"), "the line's own edit\n");
        TreeSetAside.DeleteNeverFollowing(Path.Combine(tree, "b"));
        var resumed = await StageAsync(tree);

        Assert.Equal(DepsWhy.Changed, Why(Said(resumed, StartNoteCodes.StartedDepsLeft, "a")));
        var left = Said(resumed, StartNoteCodes.StartedDepsLeft, "b");
        Assert.Equal(DepsWhy.BeforeOwn, Why(left));
        Assert.Contains("`a`", left.Note);
        Assert.False(Directory.Exists(Path.Combine(tree, "b")));
        Assert.Equal("the line's own edit\n", await File.ReadAllTextAsync(Path.Combine(tree, "a", "pkg", "a.js")));
    }

    [Fact]
    public async Task An_install_that_writes_git_s_common_folder_is_said_and_takes_the_repository_s_lock_next_time()
    {
        await MakeAsync("common", "plain");
        var tree = await TreeAsync("t1");
        Assert.Null(InstallAdmission.WroteCommon(Home, "engine", "a/"));

        var made = await StageAsync(tree);

        Assert.Contains("hooks/post-merge", Said(made, StartNoteCodes.StartedDepsCommon, "a").Note);
        Assert.True(InstallAdmission.WroteCommon(Home, "engine", "a/"));
        Assert.False(InstallAdmission.WroteCommon(Home, "engine", "b/"));
    }

    [Fact]
    public async Task A_terminal_s_visit_during_an_install_ends_it_and_leaves_the_person_s_edits_and_new_files_as_they_were()
    {
        await MakeAsync("slow", "plain");
        var tree = await TreeAsync("t1");
        Task? visit = null;
        DependencyCommands.Installing.Value = folder =>
        {
            if (folder != "a") return;
            visit = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                await File.WriteAllTextAsync(Path.Combine(tree, "README.md"), "# the person's edit\n");
                await File.WriteAllTextAsync(Path.Combine(tree, "notes.txt"), "new and unignored\n");
                Directory.CreateDirectory(Path.Combine(tree, "side"));
                await File.WriteAllTextAsync(Path.Combine(tree, "side", "mine.txt"), "new and ignored\n");
                Assert.True(TreeVisit.Take(Home, tree));
            });
        };
        var clock = Stopwatch.StartNew();

        var stopped = await StageAsync(tree);
        await visit!;

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"the install was ended, not waited out ({clock.Elapsed})");
        Assert.True(stopped.Stopped);
        Assert.False(TreeFilesRecord.Read(Home, tree)!.Unworked);
        Assert.Null(TreeFilesRecord.Read(Home, tree)!.Of("a/"));
        Assert.Equal("# the person's edit\n", await File.ReadAllTextAsync(Path.Combine(tree, "README.md")));
        Assert.True(File.Exists(Path.Combine(tree, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(tree, "side", "mine.txt")));
        Assert.False(Directory.Exists(Path.Combine(tree, "b")), "nothing after the visit is installed");
    }
}
