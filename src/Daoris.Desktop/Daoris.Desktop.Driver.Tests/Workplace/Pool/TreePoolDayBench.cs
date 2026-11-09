using System.Diagnostics;
using Daoris.Driver;
using Microsoft.Data.Sqlite;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A day of a repository's main replayed as successive lines, in one of three arms: through one kept slot with the rules as they stand
/// (<c>base</c>), the same with the person's one rule that sets every <c>bin/</c> aside at a take (<c>aside</c>, the control), or a fresh
/// tree per line (<c>fresh</c>). Each line's steps are counted apart (the take or the open, the files stage, the line's build, a fresh
/// tree's removal), every refusal is said with its reason, the found layer is read as a look reads it, and once in the day a running
/// process holds one of the slot's files over a take and its build. The scratch is measured and removed at the end.
/// </summary>
internal sealed class TreePoolDayBench(string scratch, BenchShape shape, string arm, int stride, bool builds, BenchRuns run)
{
    private const string Workspace = "w";

    private const string Repository = "engine";

    private readonly List<(string Phase, long Ms)> _phases = [];

    private readonly Stopwatch _clock = new();

    private PoolSite _site = null!;

    private TreePool _pool = null!;

    private BenchBuilds _builds = null!;

    private IReadOnlyList<string> Commits => shape.Commits;

    internal async Task RunAsync()
    {
        var home = Path.Combine(scratch, "home");
        Directory.CreateDirectory(home);
        try
        {
            foreach (var index in Samples()) await GitAsync(shape.Root, "branch", "-f", BranchAt(index), Commits[index]);
            _site = new PoolSite(home, shape.Root, Workspace, Repository);
            _pool = new TreePool(home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell)
            {
                Seams = new PoolSeams
                {
                    Open = (claim, _) => Task.FromResult<string?>($"record-{claim.Line}"),
                    Reached = (phase, _) =>
                    {
                        _phases.Add((phase, _clock.ElapsedMilliseconds));
                        return Task.CompletedTask;
                    },
                },
            };
            _builds = new BenchBuilds(home, shape.Web, builds, run);
            run.Say($"== {(shape.Web ? "the web shape" : "this repository")}, arm {arm}: {Commits.Count} commits, every {stride}th a line, {(builds ? "a build per line" : "builds off")}");
            await FoundAsync(Commits[0]);
            if (arm == "fresh")
            {
                await FreshAsync();
                return;
            }

            if (arm == "aside")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersonFiles.PathOf(home))!);
                File.WriteAllText(PersonFiles.PathOf(home), """{ "form": 2, "machine": { "rules": [ { "kind": "output", "paths": ["bin/"], "take": "aside" } ] } }""");
                run.Say("  the control: the person's one machine rule sets every bin/ aside at a take");
            }

            var holder = await BenchHolder.BuildAsync(Path.Combine(scratch, "holder"), run);
            await ReplayAsync(await MakeAsync(), holder);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var held = TreeWrites.Of(scratch);
            run.Say($"the scratch held {held.Files} files, {BenchStep.Mb(held.Bytes)} MB at the end, links not followed; removed");
            GitSeed.Discard(scratch);
        }
    }

    /// <summary>The found layer read as a look reads it: at registration, and again where the line moved past a file the analysis reads.</summary>
    private async Task FoundAsync(string commit)
    {
        var watch = Stopwatch.StartNew();
        var why = await FoundReads.LookAsync(_site.Home, Repository, _site.Root, commit, DateTimeOffset.UtcNow, FoundReads.AskAsync, default);
        if (why is null) return;
        var rules = FoundFiles.Read(_site.Home, Repository).Reading?.Rules ?? [];
        run.Say($"  the found layer read at {commit[..10]} ({why}) in {watch.Elapsed.TotalSeconds:F1} s: "
            + string.Join("; ", rules.Select(rule => $"{rule.Kind} {string.Join(' ', rule.Paths)}{(rule.Take is { } take ? $" take {take}" : "")}")));
    }

    private async Task<string> MakeAsync()
    {
        var watch = Stopwatch.StartNew();
        var made = await _pool.MakeAsync(new PoolMake(_site, 1, "quest-1", Line(0), "", From: BranchAt(0)));
        if (!made.Ok) throw new InvalidOperationException(made.Refused ?? made.Stuck);
        var folder = made.Slot!.Path;
        var tree = TreeWrites.Of(folder);
        var open = new BenchStep(tree.Files, tree.Bytes, 0, watch.Elapsed.TotalSeconds);
        var line = new BenchLine("line 0 (the slot made, built cold)", open, await StageAsync(folder, "line 0"), (await BuildAsync(folder, "line 0")).Step);
        run.Say($"  {line}");
        if (await LetGoAsync(folder, Line(0)) is { } stuck) throw new InvalidOperationException(stuck);
        return folder;
    }

    private async Task ReplayAsync(string folder, string holder)
    {
        var steps = Enumerable.Range(1, Commits.Count - 1).Where(each => each % stride == 0 || each == Commits.Count - 1).ToList();
        var contended = steps[steps.Count / 2];
        run.Say($"replay: {steps.Count} takes; a process holds {shape.Held} over the take of line {contended} and its build");
        var (lines, refused, judged) = (new List<BenchLine>(), new List<string>(), new List<long>());
        var previous = Commits[0];
        foreach (var step in steps)
        {
            var commit = Commits[step];
            var differing = (await GitAsync(_site.Root, "diff", "--name-only", previous, commit)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            run.Say($"  line {step} at {commit[..10]}: {differing} tracked files differ from the last line taken");
            await FoundAsync(commit);
            var watch = Stopwatch.StartNew();
            await _pool.JudgeAsync(_site, 1);
            judged.Add(watch.ElapsedMilliseconds);
            var status = await FillJudgement.StatusAsync(folder, true, default);
            var tops = await SlotIgnored.ReadAsync(_site, folder, status, await WorkingTree.HeadAsync(folder, default) ?? "", default);
            run.Say($"    the slot's ignored tops: the line's own [{string.Join(", ", tops.Own)}]; {status.Count(each => each.Value == "!!")} ignored entries listed");
            var held = step == contended && File.Exists(Path.Combine(folder, shape.Held)) ? await BenchHolder.HoldAsync(holder, Path.Combine(folder, shape.Held)) : null;
            try
            {
                var (taken, take) = await TakeAsync(folder, step, commit);
                if (!taken.Ok && held is not null)
                {
                    run.Say($"  contention: the take refused with {shape.Held} held by process {held.Id}: {taken.Refused ?? taken.Stuck}; the process stopped and the take asked again");
                    held.Dispose();
                    held = null;
                    var (again, retake) = await TakeAsync(folder, step, commit);
                    (taken, take) = (again, take + retake);
                }
                else if (held is not null)
                {
                    run.Say($"  contention: the take went ahead with {shape.Held} held by process {held.Id}");
                }

                if (!taken.Ok)
                {
                    refused.Add($"{step}: {taken.Refused ?? taken.Stuck}");
                    continue;
                }

                previous = commit;
                var stage = await StageAsync(folder, $"line {step}");
                var built = await BuildAsync(folder, $"line {step}");
                var build = built.Step;
                if (held is not null)
                {
                    run.Say($"  contention: the build with the file held {(built.Failed.Count == 0 ? "succeeded" : "failed: " + string.Join("; ", built.Failed))}; the process stopped");
                    held.Dispose();
                    held = null;
                    if (built.Failed.Count > 0) build += (await BuildAsync(folder, $"line {step} again, the file let go")).Step;
                }

                lines.Add(new BenchLine(Line(step), take, stage, build));
                run.Say($"  {lines[^1]}");
                if (step != steps[^1] && await LetGoAsync(folder, Line(step)) is { } why) refused.Add(why);
            }
            finally
            {
                held?.Dispose();
            }
        }

        var slot = _pool.Slot(Workspace, Repository, 1)!;
        run.Say($"replay: {lines.Count} lines taken, {refused.Count} refused{(refused.Count == 0 ? "" : ": " + string.Join("; ", refused))}");
        run.Say($"  judgement: mean {judged.Average():F0} ms, max {judged.Max()} ms; the slot holds {BenchStep.Mb(slot.Bytes)} MB of ignored bytes outside the dependencies");
        run.Say($"  {BenchLine.Means(lines)}");
    }

    private async Task<(PoolDone Taken, BenchStep Take)> TakeAsync(string folder, int step, string commit)
    {
        var before = TreeWrites.Of(folder);
        _phases.Clear();
        _clock.Restart();
        var taken = await _pool.TakeAsync(new PoolTake(_site, 1, "quest-2", Line(step), commit, false, "main", null, ""));
        var take = BenchStep.Of(before.Until(TreeWrites.Of(folder)), _clock.Elapsed.TotalSeconds);
        run.Say($"    take: {(taken.Ok ? "taken" : $"refused: {taken.Refused ?? taken.Stuck}")}; {take}; [{string.Join(' ', _phases.Select(each => $"{each.Phase}@{each.Ms}"))}]");
        return (taken, take);
    }

    /// <summary>A line's work committed, its slot let go and its line finished, which sets its own ignored paths aside and detaches the slot.</summary>
    private async Task<string?> LetGoAsync(string folder, string line)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "work.txt"), line + "\n");
        await GitAsync(folder, "add", "work.txt");
        await GitAsync(folder, "commit", "--quiet", "-m", line);
        var ended = await _pool.EndAsync(new PoolEnd(_site, 1, "quest-1", "session-1"));
        if (!ended.Ok) return $"end: {ended.Refused ?? ended.Stuck}";
        var finished = await _pool.FinishAsync(new PoolFinish(_site, "quest-1", line, "main"));
        return finished.Ok ? null : $"finish: {finished.Refused ?? finished.Stuck}";
    }

    /// <summary>The files stage as a start runs it after its record opens: what it wrote in the tree and in Daoris's store, and what it said.</summary>
    private async Task<BenchStep> StageAsync(string folder, string what)
    {
        var store = DependencyStore.RootOf(_site.Home);
        Directory.CreateDirectory(store);
        var (before, stored) = (TreeWrites.Of(folder), TreeWrites.Of(store));
        var watch = Stopwatch.StartNew();
        var done = await new TreeFilesStage(_site.Home, _site.Root, folder, Repository).RunAsync(default);
        var seconds = watch.Elapsed.TotalSeconds;
        var (tree, filled) = (before.Until(TreeWrites.Of(folder)), stored.Until(TreeWrites.Of(store)));
        var placed = TreeFilesRecord.Read(_site.Home, folder)?.Placements.Select(each => $"{each.Path} {each.How}") ?? [];
        run.Say($"    stage, {what}: in the tree {tree.Written} files {BenchStep.Mb(tree.Bytes)} MB, in the store {filled.Written} files {BenchStep.Mb(filled.Bytes)} MB; "
            + $"dependencies [{string.Join(", ", placed)}]; said: {string.Join(" | ", done.Said.Select(each => each.Note))}{(done.Stopped ? " (stopped)" : "")}");
        return new BenchStep(tree.Written + filled.Written, tree.Bytes + filled.Bytes, tree.Deleted, seconds);
    }

    private async Task<BenchBuilt> BuildAsync(string folder, string what)
    {
        var built = await _builds.BuildAsync(folder);
        run.Say($"    build, {what}: {built.Parts}; {built.Files} files {BenchStep.Mb(built.Bytes)} MB, {built.Deleted} gone; bases left unreleased {built.Unreleased}"
            + (built.Failed.Count > 0 ? $"; failed: {string.Join("; ", built.Failed)}" : ""));
        return built;
    }

    /// <summary>A fresh tree per line: today's open at the line's commit, its files stage, its cold build and its removal, each counted.</summary>
    private async Task FreshAsync()
    {
        var opening = new TreeOpening(new TreeContext(_site.Home, WorkplacePlugins.Current.Wire(_site.Home)));
        var lines = new List<BenchLine>();
        foreach (var index in Samples())
        {
            var folder = Path.Combine(scratch, "fresh");
            var watch = Stopwatch.StartNew();
            await opening.OpenAsync(_site.Root, Repository, Workspace, default, BranchAt(index), null, null, folder, $"f-{index:x7}");
            var tracked = TreeWrites.Of(folder);
            var open = new BenchStep(tracked.Files, tracked.Bytes, 0, watch.Elapsed.TotalSeconds);
            var stage = await StageAsync(folder, $"fresh at {index}");
            var build = (await BuildAsync(folder, $"fresh at {index}")).Step;
            var all = TreeWrites.Of(folder);
            watch.Restart();
            if (TreeLinks.Unlinked(folder) is { } stays) throw new InvalidOperationException($"the fresh tree at {index} stays: {stays}");
            var (_, code) = await run.TimedAsync(scratch, "cmd", "/c", "rd", "/s", "/q", folder);
            await GitAsync(_site.Root, "worktree", "prune");
            lines.Add(new BenchLine($"fresh at {index}", open, stage, build, new BenchStep(0, 0, all.Files, watch.Elapsed.TotalSeconds)));
            run.Say($"  {lines[^1]}{(code == 0 ? "" : $" (the removal exited {code})")}");
        }

        run.Say($"  {BenchLine.Means(lines)}");
    }

    /// <summary>The first, the middle and the last of the day's commits, where a fresh tree is opened.</summary>
    private IEnumerable<int> Samples() => new[] { 0, (Commits.Count - 1) / 2, Commits.Count - 1 }.Distinct();

    private static string BranchAt(int index) => $"bench-{index}";

    private static string Line(int index) => $"s-{index:x8}";
}
