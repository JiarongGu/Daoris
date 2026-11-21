using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The files stage's read of its keys over real git and real processes, in the shape of a large web repository: two dependencies, `deps/`
/// and `web/deps/`, whose rules name the same toolchain reads as the npm signature gives every `package.json` folder. Each read is a
/// tracked script that counts its runs in a file beside the trees. Fixtures live under the repository's gitignored `_fixtures/`.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ToolchainReadsTests : IDisposable
{
    private const string Install = """
        const fs = require('fs');
        fs.mkdirSync('deps/pkg', { recursive: true });
        fs.writeFileSync('deps/pkg/index.js', 'module.exports = 1;\n');
        """;

    private const string Read = """
        const fs = require('fs'), path = require('path');
        fs.appendFileSync(path.join(__dirname, '..', 'reads.txt'), process.argv[2] + '\n');
        console.log('tool ' + process.argv[2]);
        """;

    private const string Rules = """
        { "rules": [
          { "kind": "dependency", "paths": ["deps/"], "install": "node install.cjs", "key": ["deps.lock"], "toolchain": ["node read.cjs one", "node read.cjs two"] },
          { "kind": "dependency", "paths": ["web/deps/"], "install": "node ../install.cjs", "key": ["web/deps.lock"], "toolchain": ["node read.cjs one", "node read.cjs two"] } ] }
        """;

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "toolchain-reads", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");
    private string Reads => Path.Combine(_scratch, "reads.txt");

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task<string> TreeAsync(string name)
    {
        Directory.CreateDirectory(Path.Combine(Root, "web"));
        if (!Directory.Exists(Path.Combine(Root, ".git")))
        {
            await GitAsync(Root, "init", "-q", "-b", "main");
            await GitAsync(Root, "config", "user.email", "test@example.com");
            await GitAsync(Root, "config", "user.name", "Test");
            await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "deps/\n");
            // CommonJS by its name: the fixture sits under this repository, whose package says its scripts are modules.
            await File.WriteAllTextAsync(Path.Combine(Root, "install.cjs"), Install);
            await File.WriteAllTextAsync(Path.Combine(Root, "read.cjs"), Read);
            await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), Rules);
            await File.WriteAllTextAsync(Path.Combine(Root, "deps.lock"), "root\n");
            await File.WriteAllTextAsync(Path.Combine(Root, "web", "deps.lock"), "web\n");
            await GitAsync(Root, "add", ".");
            await GitAsync(Root, "commit", "-q", "-m", "first");
        }

        var tree = Path.Combine(_scratch, name);
        await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    private IReadOnlyList<string> ReadsRun() => File.Exists(Reads) ? File.ReadAllLines(Reads) : [];

    [Fact]
    public async Task Each_toolchain_read_runs_once_per_stage_for_every_dependency_that_names_it_and_the_commits_files_are_listed_once()
    {
        var one = await TreeAsync("t1");

        TreeFilesOutcome installed;
        int listings;
        using (var tally = GitStartTally.Open(_scratch))
        {
            installed = await new TreeFilesStage(Home, Root, one, "engine").RunAsync(CancellationToken.None);
            listings = tally.Starts.Count(start => start.StartsWith("ls-tree ", StringComparison.Ordinal) && start.Contains("--full-tree", StringComparison.Ordinal));
        }

        Assert.Equal(1, listings);

        Assert.Equal(new[] { "one", "two" }, ReadsRun());
        var codes = installed.Said.SelectMany(each => each.Parts).Select(part => part.Code).ToList();
        Assert.Equal(2, codes.Count(code => code == StartNoteCodes.StartedDepsInstalled.Code));
        Assert.Equal(2, DependencyStore.Entries(Home).Count);

        // Each key read the output it shares: the next tree's stage reads each once again and places both from the store.
        var two = await TreeAsync("t2");
        var placed = await new TreeFilesStage(Home, Root, two, "engine").RunAsync(CancellationToken.None);

        Assert.Equal(new[] { "one", "two", "one", "two" }, ReadsRun());
        Assert.Equal(2, placed.Said.SelectMany(each => each.Parts).Count(part => part.Code == StartNoteCodes.StartedDepsPlaced.Code));
    }
}
