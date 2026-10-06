using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

/// <summary>
/// GATE1b: with a merge open, the docs gate dates each side from the commit the merge would make, not from HEAD.
/// </summary>
/// <remarks>
/// A merge gated before it is committed still has HEAD at the branch it merges into. Dated from HEAD, a README a day
/// behind the source the merge brought passed every gate and failed `verify` once committed (FIX-LOG, GATE1: the
/// TOOL4e merge). Each case is a real repository, so the dates are the ones git will log, not a fake's.
/// </remarks>
public sealed class DocsGateMergeTests : IDisposable
{
    private readonly Fixture _fx = new("docs-merge");

    public void Dispose() => _fx.Dispose();

    private string Git(params string[] arguments) => GitOn(null, arguments);

    /// <summary>Commit dates the fixture sets, so a verdict never depends on the day the test runs.</summary>
    private string GitOn(int? day, params string[] arguments)
    {
        var at = day is { } d ? $"2026-01-{d:00}T10:00:00Z" : null;
        var ran = Process.Run("git", arguments, _fx.Path, at is null ? null : new Dictionary<string, string>
        {
            ["GIT_AUTHOR_DATE"] = at,
            ["GIT_COMMITTER_DATE"] = at,
        });
        Assert.True(ran.ExitCode == 0, $"git {string.Join(' ', arguments)}: {ran.Error}");
        return ran.Output.Trim();
    }

    private void Commit(int day, string message)
    {
        Git("add", "-A");
        GitOn(day, "commit", "-q", "-m", message);
    }

    /// <summary>
    /// TOOL4e's shape: main holds the README and its source from day 1; the branch `work` changes the source on day
    /// 5; main then changes <paramref name="onMain"/> on <paramref name="mainDay"/>. The merge is left open, as the
    /// merge tool leaves it while it gates; with the source changed on main too, it stops on the conflict.
    /// </summary>
    private void MergeOpen(string onMain = "other.txt", int mainDay = 2)
    {
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Fixture");
        Git("config", "user.email", "fixture@example.invalid");
        Git("config", "core.autocrlf", "false");
        _fx.Write("README.md", "# Fixture\n\nWhat src does.\n");
        _fx.Write("src/a.txt", "one\n");
        Commit(1, "one");

        Git("checkout", "-q", "-b", "work");
        _fx.Write("src/a.txt", "one\ntwo\n");
        Commit(5, "two");

        Git("checkout", "-q", "main");
        _fx.Write(onMain, "main\n");
        Commit(mainDay, "three");

        var conflict = onMain == "src/a.txt";
        var merged = Process.Run("git", ["merge", "--no-ff", "--no-commit", "work"], _fx.Path);
        Assert.True(merged.ExitCode == (conflict ? 1 : 0), merged.Output + merged.Error);
    }

    private GateResult RunDocs() =>
        new DocsGate(new CommandLineGitHistory(_fx.Path)).Run(new GateContext(
            _fx.Path, new GateDeclaration { Docs = new DocsOptions(Tracked: [new TrackedDocument("README.md", ["src"])]) }));

    /// <summary>What git says of the checkout, and what its git folder holds: judging a merge leaves all of it.</summary>
    private string State()
    {
        var gitDir = Git("rev-parse", "--absolute-git-dir");
        var mergeHead = Path.Combine(gitDir, "MERGE_HEAD");
        return string.Join("\n--\n",
            Git("rev-parse", "HEAD"),
            File.Exists(mergeHead) ? File.ReadAllText(mergeHead) : "no merge",
            Git("diff", "--cached", "--name-status"),
            Git("status", "--porcelain=v2", "--branch"),
            string.Join(',', Directory.EnumerateFileSystemEntries(gitDir).Select(Path.GetFileName).Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// The failure GATE1 recorded, seen by the devkit itself: HEAD dates both sides day 1, and the commit the merge
    /// would make dates the source day 5, from the branch's commit, as `git log` will once it is made.
    /// </summary>
    [Fact]
    public void During_a_merge_the_source_it_brings_is_dated_as_the_merge_would_commit_it()
    {
        MergeOpen();
        var before = State();

        var result = RunDocs();

        Assert.False(result.Passed, result.Detail);
        Assert.Contains(
            "README.md last changed 2026-01-01, but src changed 2026-01-05 (4 days later) — the document is supposed to describe it",
            result.Detail);
        Assert.Contains("as the open merge would commit it", result.Detail);
        Assert.Equal(before, State());
    }

    /// <summary>The parent fixes the README in the merge and has not staged it: the commit is the tree as it will be added.</summary>
    [Fact]
    public void A_document_fixed_in_the_merge_passes_before_it_is_staged()
    {
        MergeOpen();
        _fx.Write("README.md", "# Fixture\n\nWhat src does, both lines.\n");
        var before = State();

        var result = RunDocs();

        Assert.True(result.Passed, result.Detail);
        Assert.Contains("as the open merge would commit it", result.Detail);
        Assert.Equal(before, State());
    }

    /// <summary>
    /// A path the merge brings keeps the date of the commit that changed it, not the day of the merge: main's README,
    /// written on day 6, came after the branch's source on day 5, and once committed the gate passes. Dating every
    /// path the merge changes as today would fail this merge, and pass one whose branch left its README behind.
    /// </summary>
    [Fact]
    public void A_source_the_merge_brings_keeps_the_date_of_the_commit_that_changed_it()
    {
        MergeOpen(onMain: "README.md", mainDay: 6);

        var result = RunDocs();

        Assert.True(result.Passed, result.Detail);
    }

    /// <summary>A merge stopped on a conflict has no commit yet; the gate names the paths rather than judge markers.</summary>
    [Fact]
    public void A_merge_with_unmerged_paths_fails_the_gate_naming_them()
    {
        MergeOpen(onMain: "src/a.txt");
        var lines = new List<string>();

        var report = new GateRunner(
            new GateContext(_fx.Path, new GateDeclaration { Docs = new DocsOptions(Tracked: [new TrackedDocument("README.md", ["src"])]) }),
            [new DocsGate(new CommandLineGitHistory(_fx.Path))]).Run(lines.Add, declared: false);

        Assert.False(report.Passed);
        var said = string.Join('\n', lines);
        Assert.Contains("unmerged", said);
        Assert.Contains("src/a.txt", said);
    }

    /// <summary>
    /// Outside a merge the gate is what it was, word for word: it reads HEAD, so a change not yet committed is not
    /// dated at all, and what it says carries nothing about a merge.
    /// </summary>
    [Fact]
    public void Outside_a_merge_the_gate_reads_HEAD_and_says_what_it_always_said()
    {
        MergeOpen();
        Git("merge", "--abort");
        _fx.Write("src/a.txt", "one\nnot committed\n");

        var current = RunDocs();

        Assert.True(current.Passed, current.Detail);
        Assert.Equal("1 document(s) keeping up", current.Detail);

        Commit(9, "four");
        var stale = RunDocs();

        Assert.False(stale.Passed);
        Assert.Equal(
            "README.md last changed 2026-01-01, but src changed 2026-01-09 (8 days later) — the document is supposed to describe it",
            stale.Detail);
    }
}
