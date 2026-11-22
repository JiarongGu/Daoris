using System.Diagnostics;
using System.Text;
using Daoris.Driver;
using Daoris.Ports.Repositories;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session's opening, its tree and files stages, timed on a repository of the installed Daoris's own shape: as many tracked
/// files as this repository, build output in the checkout, a root dependency npm's own <c>npm ci</c> installs from a lockfile whose
/// one marked install script is a package npm leaves out on this system (fsevents' shape) under a <c>prepare</c> that reads the whole
/// tree and writes beside its folder, and a web dependency of 5,000 files. The second opening is the one every session pays:
/// both dependencies placed from the store, inside the budget, each stage said. Scratch under the repository's ignored <c>_fixtures/</c>.
/// </summary>
/// <remarks>Alone: it times real installs and copies, which a class beside it would slow.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Collection(Alone.Name)]
public sealed class OpeningShapeProcessTests(ITestOutputHelper output) : IDisposable
{
    private const int Tracked = 11_500;
    private const int WebFiles = 5_000;
    private const int Built = 1_000;
    private const string Repository = "engine";

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "opening-shape", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_scratch, "home");

    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public async Task A_second_opening_places_each_dependency_from_the_store_inside_its_budget_and_says_each_stage()
    {
        await MakeAsync();
        var first = new LookStages();
        var cold = Stopwatch.StartNew();
        await OpenAsync("s-00000001", first);
        output.WriteLine($"first opening {cold.ElapsedMilliseconds} ms: {first.Take()}");

        var steps = new LookStages();
        var watch = Stopwatch.StartNew();
        var said = await OpenAsync("s-00000002", steps);
        watch.Stop();
        var line = steps.Take();
        output.WriteLine($"second opening {watch.ElapsedMilliseconds} ms: {line}");
        output.WriteLine(string.Join(Environment.NewLine, said.Select(each => each.Note)));

        Assert.Equal(2, Count(said, StartNoteCodes.StartedDepsPlaced));
        Assert.Equal(0, Count(said, StartNoteCodes.StartedDepsUnstored));
        Assert.Contains("files.deps=", line);
        Assert.True(watch.Elapsed < Budget, $"the opening took {watch.ElapsedMilliseconds} ms, over its {Budget.TotalSeconds} s: {line}");
    }

    /// <summary>Four openings of one repository at once, as after a restart, beside twelve loops of the looks' reads of it: each stage said with its queue, an opening's ask waiting less than a look's.</summary>
    [Fact]
    public async Task Four_openings_at_once_beside_the_loops_reads_say_where_each_stage_waits()
    {
        await MakeAsync(tracked: 4_000, webFiles: 1_000);
        await OpenAsync("s-00000001", new LookStages());
        _ = GitAskTally.Shared.Take();
        using var stop = new CancellationTokenSource();
        var fanOut = Task.WhenAll(Enumerable.Range(0, FanOut).Select(_ => Task.Run(async () =>
        {
            using var asking = GitAskers.Is(GitAskers.Look);
            while (!stop.IsCancellationRequested) await MachinePorts.Repository.StatusAsync(Root, false, CancellationToken.None);
        })));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var all = Stopwatch.StartNew();
        var openings = await Task.WhenAll(Enumerable.Range(2, 4).Select(number => Task.Run(async () =>
        {
            var steps = new LookStages();
            var watch = Stopwatch.StartNew();
            using (GitAskers.Is($"open{number}")) await OpenThroughGitAsync($"s-0000000{number}", steps);
            return (Ms: watch.ElapsedMilliseconds, Line: steps.Take());
        })));
        all.Stop();
        stop.Cancel();
        await fanOut;
        var asks = GitAskTally.Shared.Take();
        foreach (var opening in openings) output.WriteLine($"opening {opening.Ms} ms: {opening.Line}");
        double Mean(string caller) => asks.Where(row => row.Caller == caller).Sum(row => row.Waited) / (double)Math.Max(1, asks.Where(row => row.Caller == caller).Sum(row => row.Count));
        output.WriteLine($"four at once {all.ElapsedMilliseconds} ms; a git ask waited on average {Mean(GitAskers.Opening):0} ms as the opening's, {Mean(GitAskers.Look):0} ms as the fan-out's: "
            + string.Join(", ", asks.GroupBy(row => row.Caller).Select(group => $"{group.Key}:{group.Sum(row => row.Count)}/{group.Sum(row => row.Waited)}ms")));
        Assert.True(all.Elapsed < FourBudget, $"four openings at once took {all.ElapsedMilliseconds} ms, over {FourBudget.TotalSeconds} s");
        Assert.Contains(asks, row => row.Caller == GitAskers.Opening);
        Assert.True(Mean(GitAskers.Opening) < Mean(GitAskers.Look), $"an opening's git ask waited {Mean(GitAskers.Opening):0} ms on average, no less than the fan-out's {Mean(GitAskers.Look):0} ms");
    }

    private const int FanOut = 12;

    private static readonly TimeSpan FourBudget = TimeSpan.FromMinutes(5);

    /// <summary><see cref="OpenAsync"/> with the tree made through the machine's git places and every git of it the opening's, as <c>Driver.RunAsync</c> asks it.</summary>
    private async Task<IReadOnlyList<Noted>> OpenThroughGitAsync(string name, LookStages steps)
    {
        using var opening = GitScheduler.AsOpening();
        var tree = Path.Combine(_scratch, "trees", name);
        using (var span = steps.Begin("tree"))
        using (span.Aim())
        {
            var added = await MachinePorts.Repository.AddWorktreeAsync(Root, new GitWorktreeAdd(tree, "main", $"daoris/{name}", TreeOpenings.LockReason(Environment.ProcessId), TreeOpenings.HooksPath(Home)), CancellationToken.None);
            Assert.True(added.Ok, added.Error);
            TreeFilesRecord.Mark(Home, tree);
        }

        var said = new List<Noted>();
        using (steps.Begin("files")) await TreeFilesStep.RunAsync(Home, Root, tree, Repository, said.Add, CancellationToken.None, steps: steps);
        return said;
    }

    private static int Count(IEnumerable<Noted> said, NoteCode code) => said.SelectMany(each => each.Parts).Count(part => part.Code == code.Code);

    /// <summary>A tree grown as an opening grows it, its hooks held and marked unworked, then the files stage, each timed.</summary>
    private async Task<IReadOnlyList<Noted>> OpenAsync(string name, LookStages steps)
    {
        var tree = Path.Combine(_scratch, "trees", name);
        using (steps.Begin("tree"))
        {
            await GitAsync(Root, [.. TreeOpenings.HooksHeld(Home), .. TreeOpenings.CheckoutSpread, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main"]);
            TreeFilesRecord.Mark(Home, tree);
        }

        var said = new List<Noted>();
        using (steps.Begin("files")) await TreeFilesStep.RunAsync(Home, Root, tree, Repository, said.Add, CancellationToken.None, steps: steps);
        return said;
    }

    private async Task MakeAsync(int tracked = Tracked, int webFiles = WebFiles)
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await GitAsync(Root, "config", "core.autocrlf", "false");
        var body = new string('x', 600);
        for (var each = 0; each < tracked; each++) Write($"src/unit{each / 50}/File{each % 50}.cs", $"// {each}\n{body}\n");
        Write(".gitignore", "node_modules/\ndocs/index/\nbin/\nobj/\n");
        Write("daoris.files.json", """
            { "form": 2, "rules": [
              { "kind": "dependency", "paths": ["/web/node_modules/"], "install": "node make-deps.cjs", "key": ["web/package.json", "web/make-deps.cjs"] },
              { "kind": "dependency", "paths": ["/node_modules/"], "key": ["package-lock.json", "package.json"], "setup": ["node tools/setup.cjs"], "per": "repository" },
              { "kind": "output", "paths": ["bin/"] },
              { "kind": "output", "paths": ["/docs/index/"] } ] }
            """);
        Write("package.json", """
            { "name": "shape", "version": "1.0.0", "private": true, "scripts": { "prepare": "node tools/prepare.cjs" },
              "devDependencies": { "local-dep": "file:vendor/local-dep" }, "optionalDependencies": { "watcher": "1.0.0" } }
            """);
        Write("package-lock.json", $$"""
            { "name": "shape", "version": "1.0.0", "lockfileVersion": 3, "requires": true, "packages": {
              "": { "name": "shape", "version": "1.0.0", "devDependencies": { "local-dep": "file:vendor/local-dep" }, "optionalDependencies": { "watcher": "1.0.0" } },
              "node_modules/local-dep": { "resolved": "vendor/local-dep", "link": true },
              "node_modules/watcher": { "version": "1.0.0", "resolved": "https://registry.npmjs.org/watcher/-/watcher-1.0.0.tgz", "integrity": "sha512-AAAA",
                "optional": true, "os": ["!{{NpmPlatform.Os}}"], "hasInstallScript": true },
              "vendor/local-dep": { "version": "1.0.0", "dev": true } } }
            """);
        Write("vendor/local-dep/package.json", """{ "name": "local-dep", "version": "1.0.0", "main": "index.js" }""");
        Write("vendor/local-dep/index.js", "module.exports = 1;\n");
        // The repository's own set-up, as this one's `prepare` reads every source file for its orientation index, written beside the folder.
        Write("tools/prepare.cjs", """
            const fs = require('fs'), path = require('path');
            let count = 0;
            const walk = (dir) => { for (const entry of fs.readdirSync(dir, { withFileTypes: true })) { const at = path.join(dir, entry.name); if (entry.isDirectory()) walk(at); else { fs.readFileSync(at); count++; } } };
            walk('src');
            fs.mkdirSync('docs/index', { recursive: true });
            fs.writeFileSync('docs/index/README.md', count + ' files\n');
            """);
        Write("tools/setup.cjs", "process.exit(0);\n");
        Write("web/package.json", """{ "name": "web", "version": "1.0.0", "private": true }""");
        Write("web/make-deps.cjs", $$"""
            const fs = require('fs');
            const body = 'y'.repeat(1000);
            for (let folder = 0; folder < {{webFiles}} / 100; folder++) fs.mkdirSync('node_modules/pkg' + folder, { recursive: true });
            let next = 0;
            const write = async () => { for (let each = next++; each < {{webFiles}}; each = next++) await fs.promises.writeFile('node_modules/pkg' + Math.floor(each / 100) + '/f' + (each % 100) + '.js', body); };
            Promise.all(Array.from({ length: 32 }, write)).then(() => fs.writeFileSync('node_modules/.package-lock.json', '{}'));
            """);
        await CommitAsync();
        for (var each = 0; each < Built; each++)
        {
            var at = Path.Combine(Root, "src", $"unit{each / 100}", "bin", "Debug", $"out{each % 100}.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            File.WriteAllText(at, body);
        }
    }

    private readonly Dictionary<string, string> _tracked = new(StringComparer.Ordinal);

    private void Write(string path, string text) => _tracked[path] = text.ReplaceLineEndings("\n");

    /// <summary>The tracked files as one commit on <c>main</c>, streamed to git's own importer: the checkout's files are a tree's to write.</summary>
    private async Task CommitAsync()
    {
        var stream = new StringBuilder();
        var marks = new List<(string Path, int Mark)>();
        foreach (var (path, text) in _tracked)
        {
            marks.Add((path, marks.Count + 1));
            stream.Append($"blob\nmark :{marks.Count}\ndata {Encoding.UTF8.GetByteCount(text)}\n{text}\n");
        }

        stream.Append("commit refs/heads/main\ncommitter Test <test@example.com> 1700000000 +0000\ndata 6\nfirst\n");
        foreach (var (path, mark) in marks) stream.Append($"M 100644 :{mark} {path}\n");
        var run = await RunWithAsync(Root, null, stream.ToString(), "fast-import", "--quiet");
        Assert.True(run.ExitCode == 0, run.Stderr);
    }
}
