using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What each reader of a repository's Daoris configuration costs, asked as the driver asks it: from a checkout's files, and at a commit
/// through git. It times this repository and a home of many repositories made under <c>_fixtures/repoconfig-bench/</c>, each holding this
/// repository's four declaration files, and writes a table to the test's output and to <c>bench.log</c> beside them. It does nothing
/// unless <c>REPOCONFIG_BENCH</c> is set, to the count of repositories (20 where it is not a number); the name carries no <c>DAORIS_</c>
/// prefix, which the host's environment strips. It runs alone, since its figures are times.
/// </summary>
/// <remarks>
/// Each reader is asked once cold with its git starts counted, then warm: the median of several asks of this repository, and one ask of
/// each repository of the home summed, which is what one pass over every repository pays. No reader keeps anything between asks, so the
/// warm figure is what every ask pays. The stamps (a file's write time and length, the asking tree's context, the refs' version) are what
/// a store keyed by version would pay instead of a read, so they are timed the same way.
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class RepoConfigBenchProcessTests(ITestOutputHelper output)
{
    private const int Rounds = 9;

    private const string Line = "main";

    private static readonly string[] Declarations = ["daoris.json", "daoris.files.json", "daoris.gates.json", "daoris.lanes.json"];

    [Fact]
    public async Task Each_reader_of_a_repositorys_configuration_is_timed_on_this_repository_and_a_home_of_many()
    {
        if (Environment.GetEnvironmentVariable("REPOCONFIG_BENCH") is not { Length: > 0 } asked) return;
        var count = int.TryParse(asked, out var each) && each > 0 ? each : 20;
        var scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "repoconfig-bench", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var home = Path.Combine(scratch, "home");
        Directory.CreateDirectory(home);
        var runs = new BenchRuns(Path.Combine(scratch, "bench.log"), output.WriteLine);

        var own = await TargetAsync("daoris", WorkspaceRoot.Folder);
        var made = Stopwatch.StartNew();
        var many = new List<Target>();
        for (var i = 0; i < count; i++) many.Add(await MakeAsync(Path.Combine(scratch, $"repo{i:00}"), own));
        runs.Say($"repoconfig bench: this repository at {own.Commit[..12]}, a home of {count} made in {made.Elapsed.TotalSeconds:0.0} s");
        runs.Say("declarations: " + string.Join(", ", Declarations.Select(file => $"{file} {own.Text.GetValueOrDefault(file)?.Length ?? 0} B")));
        runs.Say($"| Reader | This: cold ms | This: warm ms | This: git starts | {count}: pass ms | {count}: git starts |");
        runs.Say("|---|---|---|---|---|---|");

        foreach (var (name, read) in Readers(home))
        {
            var (cold, starts) = await ColdAsync(read, own);
            var warm = new List<double>();
            for (var round = 0; round < Rounds; round++) warm.Add(await TimedAsync(read, own));
            warm.Sort();

            var pass = 0.0;
            var passStarts = 0;
            foreach (var target in many)
            {
                passStarts += (await ColdAsync(read, target)).Starts;
                pass += await TimedAsync(read, target);
            }

            runs.Say($"| {name} | {cold:0.00} | {warm[Rounds / 2]:0.00} | {starts} | {pass:0.0} | {passStarts} |");
        }
    }

    /// <summary>One repository as the readers are handed it: its root, the commit read at, and its declarations' blobs and texts.</summary>
    private sealed record Target(string Repository, string Root, string Commit, IReadOnlyDictionary<string, string> Blobs, IReadOnlyDictionary<string, string> Text);

    private static IReadOnlyList<(string Name, Func<Target, Task> Read)> Readers(string home) =>
    [
        ("stamp: the four files' write time and length", Sync(target =>
        {
            foreach (var file in Declarations) _ = new FileInfo(Path.Combine(target.Root, file)) is { Exists: true } info ? info.LastWriteTimeUtc.Ticks ^ info.Length : 0;
        })),
        ("stamp: the asking tree's context (`GitStamp.ContextOf`)", Sync(target => GitStamp.ContextOf(target.Root))),
        ("stamp: the whole stamp and its fence (`GitStamp`)", async target => _ = await GitStamp.OfAsync(target.Root, CancellationToken.None)),
        ("stamp: the refs' version (`CountedRefs`)", Sync(target =>
        {
            if (RefFiles.CommonDirOf(target.Root) is { } common) CountedRefs.Read(common);
        })),
        ("disk: the four files read and parsed", Sync(target =>
        {
            foreach (var file in Declarations.Select(file => Path.Combine(target.Root, file)).Where(File.Exists))
            {
                using var _ = JsonDocument.Parse(File.ReadAllBytes(file));
            }
        })),
        ("git: the four blobs read by id", async target =>
        {
            foreach (var id in target.Blobs.Values) _ = await GitObjectReads.TextAsync(target.Root, id, 1 << 20, CancellationToken.None);
        }),
        ("`LineRegistration.Compose` (manifest and lanes, parsed)", Sync(target =>
            LineRegistration.Compose(target.Repository, target.Text["daoris.json"], target.Text.GetValueOrDefault("daoris.lanes.json"), target.Root, true))),
        ("`LaneRooms.Read` (lanes, parsed)", Sync(target => LaneRooms.Read(target.Text.GetValueOrDefault("daoris.lanes.json")))),
        ("`RepositoryFilesReader.Read` (rule file, parsed)", Sync(target => RepositoryFilesReader.Read(target.Text.GetValueOrDefault("daoris.files.json")))),
        ("`HeavyGates.Read` (checkout)", Sync(target => HeavyGates.Read(target.Root))),
        ("`HeavyGates.Marks` (checkout)", Sync(target => HeavyGates.Marks(target.Root))),
        ("`TreeLocalInputs.Declared` (checkout)", Sync(target => TreeLocalInputs.Declared(target.Root))),
        ("`OpinionPackets.RulesIn` (checkout)", Sync(target => OpinionPackets.RulesIn(target.Root))),
        ("`RepositoryIndexes.Find` (checkout)", Sync(target => RepositoryIndexes.Find(target.Root))),
        ("`PersonFiles.Read` (home)", Sync(_ => PersonFiles.Read(home))),
        ("`FoundFiles.Read` (home)", Sync(target => FoundFiles.Read(home, target.Repository))),
        ("`LineDeclarationReader.ReadAsync` (line: manifest, lanes)", async target => _ = await LineDeclarationReader.ReadAsync(target.Root, Line)),
        ("`LandedGates.ReadAsync` and `TextAsync` (line: gates)", async target =>
        {
            if ((await LandedGates.ReadAsync(target.Root, Line)).Tip is { } tip) _ = await LandedGates.TextAsync(target.Root, tip.Gates);
        }),
        ("`RepositoryFilesReader.AtAsync` (commit: rule file)", async target => _ = await RepositoryFilesReader.AtAsync(target.Root, target.Commit, CancellationToken.None)),
        ("`FileAnalysis.ReadAsync` (commit: the found layer)", async target =>
            _ = await FileAnalysis.ReadAsync(target.Root, target.Commit, FileSignatures.Shipped, [], CancellationToken.None)),
        ("`StoreDependencyRules.ReadAsync` (a start's files stage)", async target =>
            _ = await StoreDependencyRules.ReadAsync(home, target.Root, target.Repository, target.Commit, CancellationToken.None)),
        ("`FilesLooks.ReadAsync` (the Files page)", async target =>
            _ = await FilesLooks.ReadAsync(home, target.Root, target.Repository, target.Commit, CancellationToken.None)),
    ];

    private static Func<Target, Task> Sync(Action<Target> read) => target =>
    {
        read(target);
        return Task.CompletedTask;
    };

    private static async Task<(double Ms, int Starts)> ColdAsync(Func<Target, Task> read, Target target)
    {
        using var tally = GitStartTally.Open(target.Root);
        var ms = await TimedAsync(read, target);
        return (ms, tally.Starts.Count);
    }

    private static async Task<double> TimedAsync(Func<Target, Task> read, Target target)
    {
        var watch = Stopwatch.StartNew();
        await read(target);
        return watch.Elapsed.TotalMilliseconds;
    }

    /// <summary>One repository of the home: this repository's declarations and its root manifest, committed on its line.</summary>
    private static async Task<Target> MakeAsync(string root, Target from)
    {
        Directory.CreateDirectory(root);
        await GitFixture.GitAsync(root, "init", "-q", "-b", Line);
        foreach (var (file, text) in from.Text) await File.WriteAllTextAsync(Path.Combine(root, file), text);
        await File.WriteAllTextAsync(Path.Combine(root, "package.json"), """{ "name": "bench", "private": true, "scripts": { "test": "node --test" } }""");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# bench\n");
        await GitFixture.GitAsync(root, "add", "-A");
        await GitFixture.GitAsync(root, "-c", "user.name=bench", "-c", "user.email=bench","commit", "-q", "-m", "declarations");
        return await TargetAsync(Path.GetFileName(root), root);
    }

    private static async Task<Target> TargetAsync(string repository, string root)
    {
        var commit = (await GitFixture.GitAsync(root, "rev-parse", "HEAD")).Trim();
        var blobs = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Declarations.Where(file => File.Exists(Path.Combine(root, file))))
        {
            blobs[file] = (await GitFixture.GitAsync(root, "rev-parse", $"{commit}:{file}")).Trim();
            text[file] = await File.ReadAllTextAsync(Path.Combine(root, file));
        }

        return new Target(repository, root, commit, blobs, text);
    }
}
