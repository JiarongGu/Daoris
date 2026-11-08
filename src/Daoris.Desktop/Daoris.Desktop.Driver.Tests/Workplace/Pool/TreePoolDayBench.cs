using System.Diagnostics;
using Daoris.Driver;
using Microsoft.Data.Sqlite;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A day of a repository's main replayed as successive lines through one kept slot, driven by the pool's own acts over a scratch clone:
/// each take's files and bytes written, the judgement's reads timed, the claim's phases timed, every refusal counted with its reason, then
/// the incremental build of the day's last line against a fresh tree's cold build, and a fresh tree's checkout and dependencies for a
/// few of the same commits. The scratch clone and its trees are removed at the end.
/// </summary>
internal sealed class TreePoolDayBench(string scratch, IReadOnlyList<string> commits, int stride, bool builds, BenchRuns run)
{
    private const string Workspace = "w";

    private const string Repository = "engine";

    private readonly List<(string Phase, long Ms)> _phases = [];

    private readonly Stopwatch _clock = new();

    private PoolSite _site = null!;

    private TreePool _pool = null!;

    internal async Task RunAsync()
    {
        var source = WorkspaceRoot.Folder;
        var home = Path.Combine(scratch, "home");
        var root = Path.Combine(scratch, Repository);
        Directory.CreateDirectory(home);
        try
        {
            await GitAsync(scratch, "clone", "--quiet", "--no-checkout", source, root);
            await GitAsync(root, "config", "user.email", "bench@example.test");
            await GitAsync(root, "config", "user.name", "Bench");
            foreach (var index in new[] { 0, 0 + (commits.Count - 1) / 4, 2 * (commits.Count - 1) / 4, 3 * (commits.Count - 1) / 4, commits.Count - 1 })
            {
                await GitAsync(root, "branch", "-f", BranchAt(index), commits[index]);
            }

            _site = new PoolSite(home, root, Workspace, Repository);
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
            run.Say($"== the day: {commits.Count} commits of main, every {stride}th replayed; builds {(builds ? "on" : "off")}");
            var slot = await MakeAsync(source);
            if (builds) await BuildAsync(slot, "the slot's first (cold) build at the day's first commit");
            if (await LetGoAsync(slot, Line(0)) is { } stuck) throw new InvalidOperationException(stuck);
            await ReplayAsync(slot);
            if (builds)
            {
                await BuildAsync(slot, "the slot's build after the day (warm)");
                await BuildAsync(slot, "the slot's build again with nothing changed");
            }

            await FreshAsync(source, root);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GitSeed.Discard(scratch);
        }
    }

    private async Task<string> MakeAsync(string source)
    {
        var watch = Stopwatch.StartNew();
        var made = await _pool.MakeAsync(new PoolMake(_site, 1, "quest-1", Line(0), "", From: BranchAt(0)));
        if (!made.Ok) throw new InvalidOperationException(made.Refused ?? made.Stuck);
        var folder = made.Slot!.Path;
        var tree = TreeWrites.Of(folder);
        run.Say($"make (a fresh checkout at the first commit): {watch.Elapsed.TotalSeconds:F1} s, {tree.Files} files, {Mb(tree.Bytes)} MB");
        await StageAsync(folder, "the slot's first files stage (installs, seals)");
        // This repository's own install writes outside its folders (its prepare script), so it is never sealed and the slot would hold it as
        // the line's own and refuse every take; a person's rule makes the two installs intermediates, kept, to measure what keeping them saves.
        Directory.CreateDirectory(Path.GetDirectoryName(PersonFiles.PathOf(_site.Home))!);
        File.WriteAllText(PersonFiles.PathOf(_site.Home), """{ "machine": { "rules": [ { "kind": "intermediate", "paths": ["node_modules/", "src/Daoris.Web/node_modules/"] } ] } }""");
        run.Say("  (the installs held as intermediates by a person's rule from here, since they are not sealed)");
        return folder;
    }

    /// <summary>
    /// A line's work committed, its slot let go and its line finished: the finish sets the line's own ignored paths (a build's scratch) aside
    /// and detaches the slot, which a take of the next line then finds free. The time of each act is said.
    /// </summary>
    private async Task<string?> LetGoAsync(string folder, string line)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "work.txt"), line + "\n");
        await GitAsync(folder, "add", "work.txt");
        await GitAsync(folder, "commit", "--quiet", "-m", line);
        var watch = Stopwatch.StartNew();
        var ended = await _pool.EndAsync(new PoolEnd(_site, 1, "quest-1", "session-1"));
        var endMs = watch.ElapsedMilliseconds;
        if (!ended.Ok) return $"end: {ended.Refused ?? ended.Stuck}";
        watch.Restart();
        var finished = await _pool.FinishAsync(new PoolFinish(_site, "quest-1", line, "main"));
        run.Say($"    let go {line}: end {endMs} ms, finish {watch.ElapsedMilliseconds} ms{(finished.Ok ? "" : $" refused: {finished.Refused ?? finished.Stuck}")}");
        return finished.Ok ? null : $"finish: {finished.Refused ?? finished.Stuck}";
    }

    /// <summary>The files stage as a start runs it after its record opens: what it wrote, how long it took and what it said.</summary>
    private async Task<(double Seconds, int Files, long Bytes)> StageAsync(string folder, string what)
    {
        var before = TreeWrites.Of(folder);
        var watch = Stopwatch.StartNew();
        var done = await new TreeFilesStage(_site.Home, _site.Root, folder, Repository).RunAsync(default);
        var seconds = watch.Elapsed.TotalSeconds;
        var wrote = before.Until(TreeWrites.Of(folder));
        run.Say($"{what}: {seconds:F1} s, {wrote.Written} files, {Mb(wrote.Bytes)} MB written, {wrote.Deleted} deleted; said: {string.Join(" | ", done.Said.Select(each => each.Note))}{(done.Stopped ? " (stopped)" : "")}");
        return (seconds, wrote.Written, wrote.Bytes);
    }

    private async Task ReplayAsync(string folder)
    {
        var steps = Enumerable.Range(1, commits.Count - 1).Where(each => each % stride == 0 || each == commits.Count - 1).ToList();
        run.Say($"replay: {steps.Count} takes; step, commit, files differing from the previous take, judge ms, take ms, [phases ms], written files, MB, deleted, outcome");
        var (files, bytes, deleted, takes, refused) = (0, 0L, 0, 0, new List<string>());
        var (judgeMs, takeMs, componentMs) = (new List<long>(), new List<long>(), new List<long>());
        var (stageFiles, stageBytes, stageSeconds) = (0, 0L, 0.0);
        var previous = commits[0];
        foreach (var step in steps)
        {
            var commit = commits[step];
            var differing = (await GitAsync(_site.Root, "diff", "--name-only", previous, commit)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            var watch = Stopwatch.StartNew();
            await _pool.JudgeAsync(_site, 1);
            judgeMs.Add(watch.ElapsedMilliseconds);
            var parts = await ComponentsAsync(folder, componentMs);
            var before = TreeWrites.Of(folder);
            _phases.Clear();
            _clock.Restart();
            var taken = await _pool.TakeAsync(new PoolTake(_site, 1, "quest-2", Line(step), commit, false, "main", null, ""));
            var took = _clock.ElapsedMilliseconds;
            var wrote = before.Until(TreeWrites.Of(folder));
            var outcome = taken.Ok ? "taken" : $"refused: {taken.Refused ?? taken.Stuck}";
            run.Say($"  {step}, {commit[..10]}, {differing}, {judgeMs[^1]}, {took}, [{string.Join(' ', _phases.Select(each => $"{each.Phase}@{each.Ms}"))}], {wrote.Written}, {Mb(wrote.Bytes)}, {wrote.Deleted}, {outcome} | judge parts: {parts}");
            if (taken.Ok)
            {
                (files, bytes, deleted) = (files + wrote.Written, bytes + wrote.Bytes, deleted + wrote.Deleted);
                takes++;
                takeMs.Add(took);
                previous = commit;
                var staged = await StageAsync(folder, $"    stage after take {step}");
                (stageFiles, stageBytes, stageSeconds) = (stageFiles + staged.Files, stageBytes + staged.Bytes, stageSeconds + staged.Seconds);
                if (step != steps[^1] && await LetGoAsync(folder, Line(step)) is { } why) refused.Add(why);
            }
            else
            {
                refused.Add($"{step}: {taken.Refused ?? taken.Stuck}");
            }
        }

        run.Say($"the files stages after the takes: {stageFiles} files and {Mb(stageBytes)} MB written, {stageSeconds:F0} s in all");
        var held = _pool.Slot(Workspace, Repository, 1)!;
        run.Say($"replay total: {takes} takes taken, {refused.Count} refused; {files} files and {Mb(bytes)} MB written, {deleted} deleted; per take {files / Math.Max(takes, 1)} files, {Mb(bytes) / Math.Max(takes, 1):F1} MB");
        run.Say($"judgement: mean {judgeMs.Average():F0} ms, max {judgeMs.Max()} ms; its parts' mean total {(componentMs.Count > 0 ? componentMs.Average() : 0):F0} ms; take: mean {(takeMs.Count > 0 ? takeMs.Average() : 0):F0} ms, max {(takeMs.Count > 0 ? takeMs.Max() : 0)} ms");
        run.Say($"the slot holds {Mb(held.Bytes)} MB of ignored bytes outside the dependencies (kept_trees.bytes); refusals: {(refused.Count == 0 ? "none" : string.Join("; ", refused))}");
    }

    private async Task<string> ComponentsAsync(string path, List<long> totals)
    {
        var parts = new List<string>();
        var sum = 0L;
        async Task<T> Time<T>(string name, Func<Task<T>> read)
        {
            var watch = Stopwatch.StartNew();
            var result = await read();
            parts.Add($"{name} {watch.ElapsedMilliseconds}");
            sum += watch.ElapsedMilliseconds;
            return result;
        }

        await Time("worktrees", () => TreeWorktrees.EntriesAsync(_site.Root, default));
        await Time("ls-files", () => WorkingTree.GitAsync(path, ["ls-files", "-v", "-z"], default));
        var (_, links) = await Time("walk", () => SetAsideReads.NestedRepositoriesAsync(path, default));
        await Time("links", () => WorkspaceLinks.UnaccountedAsync(path, links, default));
        var status = await Time("status", () => FillJudgement.StatusAsync(path, true, default));
        var head = await WorkingTree.HeadAsync(path, default) ?? "";
        var tops = await Time("tops", () => SlotIgnored.ReadAsync(_site, path, status, head, default));
        if (tops.Own.Count > 0) parts.Add($"the line's own: {string.Join(' ', tops.Own)}");
        await Time("refs", () => PoolJudging.HeldByRefAsync(path, head, default));
        totals.Add(sum);
        return string.Join(", ", parts);
    }

    private async Task BuildAsync(string folder, string what)
    {
        var before = TreeWrites.Of(folder);
        var desktop = Path.Combine(folder, "src", "Daoris.Desktop");
        var (driver, _) = await run.TimedAsync(folder, "dotnet", "build", Path.Combine(desktop, "Daoris.Desktop.Driver.Tests"), "-nodeReuse:false", "--nologo", "-v", "q");
        var (modules, _) = await run.TimedAsync(folder, "dotnet", "build", Path.Combine(desktop, "Daoris.Desktop.Modules.Tests"), "-nodeReuse:false", "--nologo", "-v", "q");
        var (generate, _) = File.Exists(Path.Combine(folder, "tools", "contracts.mjs")) ? await run.TimedAsync(folder, "node", "tools/contracts.mjs") : (0, 0);
        // npm.cmd run from a folder that holds its own node_modules looks for npm there, so node runs npm's entry file by its path.
        var node = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(each => Path.Combine(each, "node.exe")).First(File.Exists);
        var npm = Path.Combine(Path.GetDirectoryName(node)!, "node_modules", "npm", "bin", "npm-cli.js");
        var (web, _) = await run.TimedAsync(folder, node, npm, "--prefix", "src/Daoris.Web", "run", "build");
        var wrote = before.Until(TreeWrites.Of(folder));
        run.Say($"build, {what}: driver tests {driver:F0} s, modules tests {modules:F0} s, contracts {generate:F0} s, web {web:F0} s; {wrote.Written} files, {Mb(wrote.Bytes)} MB written, {wrote.Deleted} deleted");
    }

    private async Task FreshAsync(string source, string root)
    {
        File.Delete(PersonFiles.PathOf(_site.Home));
        var samples = new[] { 0, 2 * (commits.Count - 1) / 4, commits.Count - 1 }.Distinct().ToList();
        run.Say($"fresh trees (today's open, then its files stage placing dependencies from the store, then its removal) at {samples.Count} of the day's commits");
        var opening = new TreeOpening(new TreeContext(_site.Home, WorkplacePlugins.Current.Wire(_site.Home)));
        foreach (var index in samples)
        {
            var folder = Path.Combine(scratch, "fresh");
            var watch = Stopwatch.StartNew();
            await opening.OpenAsync(root, Repository, Workspace, default, BranchAt(index), null, null, folder, $"f-{index:x7}");
            var open = watch.Elapsed.TotalSeconds;
            var tracked = TreeWrites.Of(folder);
            run.Say($"  fresh at {index}: open {open:F1} s, {tracked.Files} files, {Mb(tracked.Bytes)} MB");
            await StageAsync(folder, $"  fresh at {index}: files stage");
            if (builds && index == commits.Count - 1) await BuildAsync(folder, "a fresh tree's cold build at the day's last commit");
            var all = TreeWrites.Of(folder);
            var (removal, _) = await run.TimedAsync(scratch, "cmd", "/c", "rd", "/s", "/q", folder);
            await GitAsync(root, "worktree", "prune");
            run.Say($"  fresh at {index}: removal {removal:F1} s, {all.Files} files, {Mb(all.Bytes)} MB; in all {all.Files} files and {Mb(all.Bytes)} MB written for the session, as many deleted");
        }
    }

    private static string BranchAt(int index) => $"bench-{index}";

    private static string Line(int index) => $"s-{index:x8}";

    private static double Mb(long bytes) => Math.Round(bytes / 1048576.0, 1);
}
