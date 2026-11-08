using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An npm workspace over real git and a real <c>npm ci</c>: the install hoists the member's packages to the root, so the member's
/// <c>node_modules/</c> stays absent, and that absence is the record's, held like a folder is. Fixtures live under the repository's
/// gitignored <c>_fixtures/</c>, never OS temp.
/// </summary>
/// <remarks>Alone: the install seam is a process-wide hook.</remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class DependencyAbsenceTests : IDisposable
{
    private const string Member = "pkgs/a/node_modules/";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-absent", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose()
    {
        DependencyCommands.Installing.Value = null;
        GitSeed.Discard(_scratch);
    }

    private async Task MakeAsync()
    {
        Directory.CreateDirectory(Path.Combine(Root, "pkgs", "a"));
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "node_modules/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "pkgs", "a", "package.json"), """{ "name": "a", "version": "1.0.0" }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "package.json"), """{ "name": "engine", "version": "1.0.0", "workspaces": ["pkgs/a"] }""");
        Assert.Equal(0, (await DependencyCommands.RunAsync(Home, Root, "npm install --package-lock-only --no-audit --no-fund", Root, InstallEnvironment.Of(new FileRule(FileKind.Dependency, ["node_modules/"])), DependencyCommands.InstallBound, CancellationToken.None)).Code);
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "an npm workspace");
    }

    private async Task<string> TreeAsync(string name)
    {
        var tree = Path.Combine(_scratch, name);
        await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    private Task<TreeFilesOutcome> StageAsync(string tree) => new TreeFilesStage(Home, Root, tree, "engine").RunAsync(CancellationToken.None);

    private static IReadOnlyList<string?> Codes(TreeFilesOutcome outcome) => [.. outcome.Said.SelectMany(each => each.Parts).Select(part => part.Code)];

    [Fact]
    public async Task A_member_whose_install_left_it_absent_is_recorded_absent_held_and_placed_with_no_install()
    {
        await MakeAsync();
        var installs = new List<string>();
        DependencyCommands.Installing.Value = installs.Add;
        var one = await TreeAsync("t1");

        var installed = await StageAsync(one);

        var said = string.Join(" | ", installed.Said.Select(each => each.Note));
        Assert.True(Directory.Exists(Path.Combine(one, "node_modules")), said);
        Assert.False(Directory.Exists(Path.Combine(one, "pkgs", "a", "node_modules")), said);
        var record = TreeFilesRecord.Read(Home, one)!.Of(Member)!;
        Assert.True(record is not null, said + " || " + string.Join(", ", TreeFilesRecord.Read(Home, one)!.Placements.Select(each => each.Path)));
        Assert.True(record.Manifest!.Absent);
        Assert.Null(record.Manifest.Differs(one, "pkgs/a/node_modules", []));

        installs.Clear();
        var two = await TreeAsync("t2");
        var placed = await StageAsync(two);

        Assert.Empty(installs);
        Assert.Contains(StartNoteCodes.StartedDepsPlaced.Code, Codes(placed));
        Assert.False(Directory.Exists(Path.Combine(two, "pkgs", "a", "node_modules")));
        Assert.True(TreeFilesRecord.Read(Home, two)!.Of(Member)!.Manifest!.Absent);

        var deps = await StoreDependencyRules.ReadAsync(Home, two, "engine", (await WorkingTree.HeadAsync(two))!, CancellationToken.None);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal) { ["node_modules/"] = "root", [Member] = "member" };
        var members = new[] { "node_modules/", Member };
        var grouped = DependencyGroups.Formed(two, TreeFilesRecord.Read(Home, two)!, members, keys, deps.Each);
        var group = Assert.Single(grouped.Groups);
        Assert.True(grouped.Of(Member)!.Manifest!.Absent);
        Assert.Null(DependencyGroups.Fails(two, grouped, group, deps.Each, keys, others: false));

        Directory.CreateDirectory(Path.Combine(two, "pkgs", "a", "node_modules"));
        Assert.Equal("its folder appeared", record.Manifest.Differs(two, "pkgs/a/node_modules", []));
        Assert.Equal("`pkgs/a/node_modules/`: its folder appeared", DependencyGroups.Fails(two, grouped, group, deps.Each, keys, others: false));
    }
}
