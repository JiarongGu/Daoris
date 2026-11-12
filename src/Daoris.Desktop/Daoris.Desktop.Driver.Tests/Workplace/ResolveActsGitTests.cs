using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: a file in conflict resolved per hunk, ours, theirs or both,
/// from the index's stages by <c>merge-file</c>; the bytes at the press equal to those shown; the replaced bytes kept as files beside the
/// record and the stages by the act's own ref, both surviving a restart; <i>Mark resolved</i> as an index step; Undo of each where the file
/// and the index are still what the act left.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ResolveActsGitTests : TreeSyncFixture
{
    private static readonly string Original = string.Join('\n', Enumerable.Range(1, 30).Select(each => $"line {each}")) + "\n";

    private static readonly IReadOnlyList<TreeHolder> Nobody2 = [];

    private ActJournal Journal(string run = "run-a", Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private ResolveActs Acts(string run = "run-a") => new(Journal(run));

    private static StageContext Context(string root, bool allowed = true) => new("engine", root, () => allowed, _ => Task.FromResult(Nobody2));

    private static string Edited(string text2, string text20, string? text10 = null)
    {
        var lines = Original.TrimEnd('\n').Split('\n');
        lines[1] = text2;
        lines[19] = text20;
        if (text10 is not null) lines[9] = text10;
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>A checkout whose merge of `topic` left `lines.txt` in conflict in two hunks (lines 2 and 20); `other.txt` merged clean.</summary>
    private async Task<string> ConflictAsync(string ours = "main two", string theirs = "topic two")
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "lines.txt", Original, "lines");
        await GitAsync(root, "switch", "-c", "topic");
        await CommitAsync(root, "lines.txt", Edited(theirs, "topic twenty"), "topic");
        await GitAsync(root, "switch", "main");
        await CommitAsync(root, "lines.txt", Edited(ours, "main twenty"), "main");
        var merge = await TryGitAsync(root, "merge", "topic");
        Assert.NotEqual(0, merge);
        return root;
    }

    private static async Task<int> TryGitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string File2(string root, string name = "lines.txt") => Path.Combine(root, name);

    /// <summary>The press for a file as the page would send it from its read: a pick per hunk.</summary>
    private static async Task<ResolvePress> PressAsync(string root, string verb, string file, params string[] takes)
    {
        var read = (await GitConflicts.ReadAsync(root, file)).Value!;
        Assert.Null(read.Why);
        var picks = read.Hunks.Select((hunk, at) => new ResolvePick(hunk.Id, takes[at])).ToList();
        return new ResolvePress(verb, file, read.Head, read.Base, read.Ours, read.Theirs, read.Worktree, picks);
    }

    private async Task<string> UnmergedAsync(string root) => (await GitAsync(root, "ls-files", "-u")).Trim();

    [Fact]
    public async Task The_read_names_each_conflict_with_what_each_side_holds()
    {
        var root = await ConflictAsync();

        var read = (await GitConflicts.ReadAsync(root, "lines.txt")).Value!;

        Assert.Null(read.Why);
        Assert.Equal(2, read.Hunks.Count);
        Assert.Equal(["main two"], read.Hunks[0].Ours);
        Assert.Equal(["topic two"], read.Hunks[0].Theirs);
        Assert.Equal(["main twenty"], read.Hunks[1].Ours);
        Assert.Equal(["topic twenty"], read.Hunks[1].Theirs);
        Assert.Equal(["line 1"], read.Hunks[0].Before.TakeLast(1));
        Assert.NotEqual(read.Hunks[0].Id, read.Hunks[1].Id);
    }

    [Theory]
    [InlineData("ours", "theirs", "main two", "topic twenty")]
    [InlineData("theirs", "ours", "topic two", "main twenty")]
    public async Task Each_hunk_takes_ours_or_theirs_apart(string first, string second, string line2, string line20)
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", first, second);

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(Edited(line2, line20), await File.ReadAllTextAsync(File2(root)));
        Assert.NotEqual("", await UnmergedAsync(root));
    }

    [Fact]
    public async Task Both_keeps_ours_then_theirs_and_the_text_around_stays_as_it_was()
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "both", "ours");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        var lines = Original.TrimEnd('\n').Split('\n').ToList();
        lines[1] = "main two\ntopic two";
        lines[19] = "main twenty";
        Assert.Equal(string.Join('\n', lines) + "\n", await File.ReadAllTextAsync(File2(root)));
    }

    [Fact]
    public async Task Non_ASCII_text_and_CRLF_lines_come_through_as_they_were()
    {
        var root = await RepositoryAsync("engine");
        var original = "alpha\r\nbeta\r\ngamma\r\n";
        await GitAsync(root, "config", "core.autocrlf", "false");
        await CommitAsync(root, "lines.txt", original, "lines");
        await GitAsync(root, "switch", "-c", "topic");
        await CommitAsync(root, "lines.txt", "alpha\r\nnaïve 道\r\ngamma\r\n", "topic");
        await GitAsync(root, "switch", "main");
        await CommitAsync(root, "lines.txt", "alpha\r\nplain\r\ngamma\r\n", "main");
        Assert.NotEqual(0, await TryGitAsync(root, "merge", "topic"));
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "theirs");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal("alpha\r\nnaïve 道\r\ngamma\r\n", System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(File2(root))));
    }

    [Fact]
    public async Task The_replaced_bytes_and_the_stages_are_kept_and_survive_a_restart()
    {
        var root = await ConflictAsync();
        var marked = await File.ReadAllBytesAsync(File2(root));
        var stages = (await GitAsync(root, "ls-files", "-u")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split(' ', '\t')[1]).ToList();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        var later = Journal("run-b");
        var record = later.Act(run.Act!)!;
        Assert.Equal((ResolveVerbs.Resolve, ActPhases.Closed, ActOutcomes.Done), (record.Kind, record.Phase, record.Outcome));
        var step = later.Steps(run.Act!).Single();
        Assert.Equal(ActStepKinds.Bytes, step.Kind);
        Assert.Equal(ActOutcomes.Done, step.Outcome);
        var kept = System.Text.Json.Nodes.JsonNode.Parse(step.Arguments)!;
        Assert.Equal(marked, await File.ReadAllBytesAsync(kept["replaced"]!.GetValue<string>()));
        Assert.Equal(await File.ReadAllBytesAsync(File2(root)), await File.ReadAllBytesAsync(kept["written"]!.GetValue<string>()));
        var held = (await GitAsync(root, "ls-tree", "-r", ActRefs.Of(run.Act!))).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split(' ', '\t')[2]).ToList();
        Assert.All(stages, stage => Assert.Contains(stage, held));
    }

    [Fact]
    public async Task Bytes_that_changed_since_they_were_shown_are_refused_and_the_file_is_left()
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");
        var typed = (await File.ReadAllTextAsync(File2(root))).Replace("main two", "edited by hand");
        await File.WriteAllTextAsync(File2(root), typed);

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("bytes changed since", run.Said);
        Assert.Equal(typed, await File.ReadAllTextAsync(File2(root)));
        Assert.Empty(Journal().Unclosed());
    }

    [Fact]
    public async Task Stages_that_moved_since_they_were_shown_are_refused()
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");

        var run = await Acts().RunAsync(Context(root), press with { Theirs = new string('a', press.Theirs.Length) }, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("stages of `lines.txt` changed since", run.Said);
    }

    [Fact]
    public async Task A_file_not_in_conflict_is_refused_to_resolve_and_to_mark()
    {
        var root = await ConflictAsync();
        var known = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");
        await File.WriteAllTextAsync(File2(root, "plain.txt"), "plain\n");
        await GitAsync(root, "add", "plain.txt");

        var resolve = await Acts().RunAsync(Context(root), known with { Path = "plain.txt" }, CancellationToken.None);
        var mark = await Acts().RunAsync(Context(root), known with { Verb = ResolveVerbs.Mark, Path = "plain.txt" }, CancellationToken.None);

        Assert.True(resolve.Refused);
        Assert.Contains("not in conflict", resolve.Said);
        Assert.True(mark.Refused);
        Assert.Contains("not in conflict", mark.Said);
    }

    [Fact]
    public async Task Every_hunk_must_be_chosen_once()
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");

        var one = await Acts().RunAsync(Context(root), press with { Picks = [press.Picks[0]] }, CancellationToken.None);
        var odd = await Acts().RunAsync(Context(root), press with { Picks = [press.Picks[0], new ResolvePick(press.Picks[1].Id, "mine")] }, CancellationToken.None);
        var stray = await Acts().RunAsync(Context(root), press with { Picks = [.. press.Picks, new ResolvePick("000000000000", "ours")] }, CancellationToken.None);

        Assert.All(new[] { one, odd, stray }, run => Assert.True(run.Refused));
        Assert.Contains("every conflict", one.Said);
    }

    [Fact]
    public async Task The_switch_off_a_session_tree_and_git_s_own_lock_refuse()
    {
        var root = await ConflictAsync();
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");

        var off = await Acts().RunAsync(Context(root, allowed: false), press, CancellationToken.None);
        var lockFile = Path.Combine(root, ".git", "index.lock");
        await File.WriteAllTextAsync(lockFile, "");
        var locked = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(off.Refused);
        Assert.True(locked.Refused);
        Assert.Contains("index.lock", locked.Said);
        Assert.Contains("<<<<<<<", await File.ReadAllTextAsync(File2(root)));
    }

    [Fact]
    public async Task Mark_resolved_adds_the_file_and_the_stages_are_gone_until_undone()
    {
        var root = await ConflictAsync();
        var acts = Acts();
        var resolved = await acts.RunAsync(Context(root), await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "theirs"), CancellationToken.None);
        Assert.False(resolved.Refused, resolved.Said);
        var stages = await UnmergedAsync(root);

        var marked = await acts.RunAsync(Context(root), await MarkAsync(root, "lines.txt"), CancellationToken.None);

        Assert.False(marked.Refused, marked.Said);
        Assert.Equal(ActOutcomes.Done, marked.Outcome);
        Assert.Equal("", await UnmergedAsync(root));
        Assert.Equal(Edited("main two", "topic twenty"), await GitAsync(root, "show", ":lines.txt"));

        var undone = await acts.UndoAsync(Context(root), marked.Act!, CancellationToken.None);

        Assert.False(undone.Refused, undone.Said);
        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(stages, await UnmergedAsync(root));
        Assert.Equal(Edited("main two", "topic twenty"), await File.ReadAllTextAsync(File2(root)));
    }

    private static async Task<ResolvePress> MarkAsync(string root, string file)
    {
        var read = (await GitConflicts.ReadAsync(root, file)).Value!;
        return new ResolvePress(ResolveVerbs.Mark, file, read.Head, read.Base, read.Ours, read.Theirs, read.Worktree, []);
    }

    [Fact]
    public async Task Mark_resolved_refuses_a_file_that_still_holds_the_conflict_markers()
    {
        var root = await ConflictAsync();

        var run = await Acts().RunAsync(Context(root), await MarkAsync(root, "lines.txt"), CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("conflict markers", run.Said);
        Assert.NotEqual("", await UnmergedAsync(root));
    }

    [Fact]
    public async Task Undo_puts_the_marked_bytes_back_where_the_file_is_still_the_act_s_and_names_a_changed_one()
    {
        var root = await ConflictAsync();
        var marked = await File.ReadAllBytesAsync(File2(root));
        var acts = Acts();
        var resolved = await acts.RunAsync(Context(root), await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours"), CancellationToken.None);
        Assert.False(resolved.Refused, resolved.Said);
        await File.AppendAllTextAsync(File2(root), "typed after\n");

        var changed = await acts.UndoAsync(Context(root), resolved.Act!, CancellationToken.None);

        Assert.True(changed.Refused);
        Assert.Contains("`lines.txt` changed since act", changed.Said);
        await File.WriteAllTextAsync(File2(root), Edited("main two", "main twenty"));

        var undone = await acts.UndoAsync(Context(root), resolved.Act!, CancellationToken.None);

        Assert.False(undone.Refused, undone.Said);
        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(marked, await File.ReadAllBytesAsync(File2(root)));
        Assert.Empty(Journal().Unclosed());
    }

    [Fact]
    public async Task A_cut_before_the_write_is_reconciled_by_another_run_as_not_done_and_the_file_is_untouched()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await ConflictAsync();
        var marked = await File.ReadAllBytesAsync(File2(root));
        var acts = Acts();
        acts.Runs.Seam = (point, _) => point == ActSeams.BeforeLaunch ? throw new InvalidOperationException("cut") : Task.CompletedTask;
        var press = await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours");

        await Assert.ThrowsAsync<InvalidOperationException>(() => acts.RunAsync(Context(root), press, CancellationToken.None));
        var open = Journal().Unclosed().Single();
        var held = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(held);
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone), (Journal("run-b").Act(open.Id)!.Phase, Journal("run-b").Act(open.Id)!.Outcome));
        Assert.Equal(marked, await File.ReadAllBytesAsync(File2(root)));
    }

    [Fact]
    public async Task The_files_beside_a_record_go_when_it_expires()
    {
        var root = await ConflictAsync();
        var run = await Acts().RunAsync(Context(root), await PressAsync(root, ResolveVerbs.Resolve, "lines.txt", "ours", "ours"), CancellationToken.None);
        var kept = System.Text.Json.Nodes.JsonNode.Parse(Journal().Steps(run.Act!).Single().Arguments)!["replaced"]!.GetValue<string>();
        Assert.True(File.Exists(kept));

        ActFiles.Remove(Home, run.Act!);

        Assert.False(File.Exists(kept));
    }
}
