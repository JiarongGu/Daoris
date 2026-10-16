using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END2 (the fabric events design §B5, §C7, §D2, §D3): the default cycle's facts kept. <c>Held</c> on both entries, the branch
/// unit's id and a landing's <c>Units</c>, the traces kept on the forget path with an open unit never pruned, the cross-process lock
/// on the two files, and the one writer (canary 9). Files only; the judgement over real git is <see cref="HeldFactsGitTests"/>'.
/// </summary>
public sealed partial class HeldFactsTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-held-" + Guid.NewGuid().ToString("N")[..8]);

    public HeldFactsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HeldFact Fact(string tip = "t1", string? line = "main", string repository = "r1", DateTimeOffset? since = null) =>
        new(Then, since ?? Then, EndBy.Merged, tip, line, "main", "m1", repository);

    private static GrownBranch Grown(string branch, string? unit = null, string repository = "engine") =>
        new(repository, "default", branch, "main", "f0", null, Then) { Unit = unit };

    /// <summary>🔴 Additive shapes (§B5): an entry written before END2 reads as not yet judged, never as ended, and with no unit.</summary>
    [Fact]
    public void An_old_entry_reads_as_not_yet_judged()
    {
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.SessionBranches),
            """{ "branches": [ { "repository": "engine", "workspace": "default", "branch": "daoris/s1", "from": "f0", "at": "2026-10-01T00:00:00Z" } ] }""");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Landings),
            """{ "branches": [ { "repository": "engine", "workspace": "default", "branch": "feature/x", "tip": "t1", "session": "s1", "landedAt": "2026-10-01T00:00:00Z" } ] }""");

        var grown = new SessionBranches(_home).Of("engine", "daoris/s1")!;
        var landed = new LandedBranches(_home).Of("engine", "feature/x")!;

        Assert.Null(grown.Held);
        Assert.Null(grown.Unit);
        Assert.Null(grown.GoneAt);
        Assert.Null(landed.Held);
        Assert.Empty(landed.Units);
    }

    [Fact]
    public void Held_and_the_unit_are_kept_on_both_entries_as_written()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/s1", "unit-a"));
        var landed = new LandedBranches(_home);
        landed.Record(new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", null, null, Then) { Units = ["unit-a"] });

        grown.Hold("engine", "daoris/s1", Fact());
        landed.Hold(landed.Of("engine", "feature/x")!, Fact(tip: "t2"));

        Assert.Equal(Fact(), grown.Of("engine", "daoris/s1")!.Held);
        Assert.Equal("unit-a", grown.Of("engine", "daoris/s1")!.Unit);
        Assert.Equal(Fact(tip: "t2"), landed.Of("engine", "feature/x")!.Held);
        Assert.Equal(["unit-a"], landed.Of("engine", "feature/x")!.Units);
    }

    /// <summary>A replay or a follow records the entry again: the unit is written once and kept, and only the end judgement writes Held.</summary>
    [Fact]
    public void Recording_again_keeps_the_unit_and_the_held_fact()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/s1", "unit-a"));
        grown.Hold("engine", "daoris/s1", Fact());

        grown.Record(Grown("daoris/s1") with { From = "f1" });

        var entry = grown.Of("engine", "daoris/s1")!;
        Assert.Equal(("unit-a", "f1"), (entry.Unit, entry.From));
        Assert.Equal(Fact(), entry.Held);
    }

    /// <summary>
    /// 🔴 §B5 and §D3: a branch's removal is not its unit's retirement. An ended entry is kept as a trace; an entry whose unit a standing
    /// landing still carries is kept open; the rest are forgotten as before.
    /// </summary>
    [Fact]
    public void Forgetting_keeps_an_ended_entry_and_an_open_unit_as_traces()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/ended", "unit-ended"));
        grown.Record(Grown("daoris/open", "unit-open"));
        grown.Record(Grown("daoris/plain", "unit-plain"));
        grown.Hold("engine", "daoris/ended", Fact());
        new LandedBranches(_home).Record(new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", null, null, Then) { Units = ["unit-open"] });

        grown.Forget("engine", ["daoris/ended", "daoris/open", "daoris/plain"]);

        Assert.Empty(grown.All());
        var traces = grown.Entries();
        Assert.Equal(["daoris/ended", "daoris/open"], traces.Select(each => each.Branch));
        Assert.All(traces, each => Assert.NotNull(each.GoneAt));
        Assert.Equal(Fact(), traces[0].Held);
        Assert.True(SessionBranches.Open(traces[1], new HashSet<string> { "unit-open" }));
    }

    /// <summary>A new tree on a branch name a trace already has makes a new entry; the trace stays, never judged again.</summary>
    [Fact]
    public void A_trace_stays_when_its_name_is_recorded_again()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/s1", "unit-a"));
        grown.Hold("engine", "daoris/s1", Fact());
        grown.Forget("engine", ["daoris/s1"]);

        grown.Record(Grown("daoris/s1", "unit-b"));

        Assert.Equal(["unit-a", "unit-b"], grown.Entries().Select(each => each.Unit));
        Assert.Equal("unit-b", grown.Of("engine", "daoris/s1")!.Unit);
        Assert.Null(grown.Of("engine", "daoris/s1")!.Held);
    }

    /// <summary>🔴 §D3: the traces' pruning keeps each repository's newest fifty retired traces and never prunes an open unit.</summary>
    [Fact]
    public void Pruning_keeps_the_newest_fifty_retired_traces_and_never_an_open_unit()
    {
        var grown = new SessionBranches(_home);
        new LandedBranches(_home).Record(new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", null, null, Then)
        {
            Units = ["unit-open-0", "unit-open-1"],
        });
        for (var at = 0; at < 2; at++) grown.Record(Grown($"daoris/open-{at}", $"unit-open-{at}"));
        grown.Forget("engine", ["daoris/open-0", "daoris/open-1"]);
        for (var at = 0; at < SessionBranches.TracesKept + 5; at++)
        {
            grown.Record(Grown($"daoris/s{at}", $"unit-{at}"));
            grown.Hold("engine", $"daoris/s{at}", Fact());
            grown.Forget("engine", [$"daoris/s{at}"]);
        }

        grown.Record(Grown("daoris/other", "unit-other", repository: "atlas"));
        grown.Hold("atlas", "daoris/other", Fact());
        grown.Forget("atlas", ["daoris/other"]);

        var traces = grown.Entries();
        Assert.Equal(SessionBranches.TracesKept, traces.Count(each => each.Repository == "engine" && each.Held is not null));
        Assert.DoesNotContain(traces, each => each.Branch is "daoris/s0" or "daoris/s4");
        Assert.Contains(traces, each => each.Branch == "daoris/s5");
        Assert.Equal(2, traces.Count(each => each.Branch.StartsWith("daoris/open-", StringComparison.Ordinal)));
        Assert.Contains(traces, each => each.Repository == "atlas");
    }

    /// <summary>Canary 7, the pure half: a fact stops holding when its tip, its line, its repository or its line's commit changes.</summary>
    [Theory]
    [InlineData("t1", "main", "r1", true, true)]
    [InlineData("t2", "main", "r1", true, false)]
    [InlineData("t1", "other", "r1", true, false)]
    [InlineData("t1", "main", "r2", true, false)]
    [InlineData("t1", "main", "r1", false, false)]
    [InlineData("t1", "main", "r1", null, false)]
    public void A_fact_holds_only_while_its_tip_line_repository_and_line_commit_stand(string tip, string line, string repository, bool? descends, bool holds) =>
        Assert.Equal(holds, Fact().HoldsAt(tip, line, repository, descends));

    /// <summary>§C7: <c>Since</c> is when it was first held at this tip, kept across judgements; a verdict that is not an end clears it.</summary>
    [Fact]
    public void Since_is_kept_at_the_same_tip_and_a_verdict_not_ended_clears_the_fact()
    {
        var now = Then.AddHours(1);
        var ended = new EndVerdict(EndState.Ended, EndBy.Merged, "main", "") { Tip = "t1", Line = "main" };

        var same = EndKeeping.Next(Fact(), ended, "m2", "r1", now)!;
        var moved = EndKeeping.Next(Fact(tip: "t0"), ended, "m2", "r1", now)!;
        var replaced = EndKeeping.Next(Fact(), ended, "m2", "r2", now)!;

        Assert.Equal((Then, now, "m2"), (same.Since, same.At, same.OnAt));
        Assert.Equal(now, moved.Since);
        Assert.Equal(now, replaced.Since);
        Assert.Null(EndKeeping.Next(Fact(), new EndVerdict(EndState.Claim, null, null, "") { Tip = "t1", Line = "main" }, null, "r1", now));
        Assert.Null(EndKeeping.Next(Fact(), new EndVerdict(EndState.Unknown, null, null, "") { Tip = "t1", Line = "main" }, null, "r1", now));
    }

    /// <summary>
    /// 🔴 The cross-process writer (D173's END2 note): an edit waits while another handle holds the record's lock under the home's
    /// <c>locks/records/</c>, then reads what the holder wrote, so neither write is lost. A second process is <see cref="RecordLockProcessTests"/>'.
    /// </summary>
    [Fact]
    public void An_edit_waits_for_the_records_lock_and_loses_nothing()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/first"));
        var lockPath = RecordLock.PathOf(_home, SessionBranches.FileName);
        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Locks), lockPath, StringComparison.Ordinal);

        Task edit;
        using (var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            edit = Task.Run(() => grown.Record(Grown("daoris/second")));
            Assert.False(edit.Wait(TimeSpan.FromMilliseconds(400)));
            // The holder's own read-modify-write, made while the edit waits.
            var text = File.ReadAllText(grown.FilePath);
            File.WriteAllText(grown.FilePath, text.Replace("daoris/first", "daoris/holder", StringComparison.Ordinal));
        }

        Assert.True(edit.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(["daoris/holder", "daoris/second"], grown.All().Select(each => each.Branch));
    }

    /// <summary>A lock held past the wait is a tool error naming the lock, and nothing is written.</summary>
    [Fact]
    public void A_lock_held_past_the_wait_writes_nothing_and_says_so()
    {
        var path = RecordLock.PathOf(_home, LandedBranches.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var held = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var error = Assert.Throws<IOException>(() => RecordLock.Take(path, TimeSpan.FromMilliseconds(150)));

        Assert.Contains("records", error.Message);
    }

    /// <summary>🔴 Canary 9: a process that does not hold the home's lease (a verb beside an instance) writes no Held.</summary>
    [Fact]
    public async Task A_process_without_the_homes_lease_writes_no_held_fact()
    {
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/s1", "unit-a"));
        var before = File.ReadAllText(grown.FilePath);

        var written = await new SessionTrees(_home).Ends.KeepAsync(_home, "engine", "default", "main", CancellationToken.None);

        Assert.Equal(0, written);
        Assert.False(HomeLease.HeldHere(_home));
        Assert.Equal(before, File.ReadAllText(grown.FilePath));
    }

    /// <summary>
    /// 🔴 Canary 9, the structure half: no writer of Held outside the end judgement's keeper (the record classes' own <c>Hold</c>
    /// aside), and no writer of the kept pull request answer outside its existing writer.
    /// </summary>
    [Fact]
    public void Only_the_end_judgement_writes_held_and_only_its_writer_the_pull_request_answer()
    {
        var driver = Path.Combine(SourceRoot(), "Daoris.Desktop.Driver");
        string[] keeper = ["EndKeeping.cs"];
        string[] records = ["LandedBranches.cs", "SessionBranches.cs", "HeldFact.cs"];
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(driver, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            var name = Path.GetFileName(file);
            var code = File.ReadAllText(file);
            if (!keeper.Contains(name) && HoldCall().IsMatch(code)) offenders.Add($"{name}: calls a record's Hold");
            if (!keeper.Contains(name) && !records.Contains(name) && code.Contains("new HeldFact(", StringComparison.Ordinal)) offenders.Add($"{name}: makes a HeldFact");
            var entries = code.Contains("GrownBranch", StringComparison.Ordinal) || code.Contains("LandedBranch", StringComparison.Ordinal);
            if (entries && !records.Contains(name) && !keeper.Contains(name) && HeldSet().IsMatch(code)) offenders.Add($"{name}: sets Held");
            if (entries && name != "LandedBranches.cs" && StateSet().IsMatch(code)) offenders.Add($"{name}: sets PullRequestState");
        }

        Assert.Empty(offenders);
    }

    // A call of the two records' writer: on the trees' context's stores, or on a store made where it is called.
    [GeneratedRegex(@"\b(Grown|Recorded|SessionBranches\([^)]*\)|LandedBranches\([^)]*\))\.Hold\(")]
    private static partial Regex HoldCall();

    // The field set on an entry: in a `with`, or where an entry is made.
    [GeneratedRegex(@"(\bwith|new (GrownBranch|LandedBranch)\([^;]*?\))\s*\{[^}]*\bHeld\s*=[^=>]", RegexOptions.Singleline)]
    private static partial Regex HeldSet();

    [GeneratedRegex(@"(\bwith|new LandedBranch\([^;]*?\))\s*\{[^}]*\bPullRequestState\s*=[^=>]", RegexOptions.Singleline)]
    private static partial Regex StateSet();

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Daoris.Desktop.Driver"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("the desktop's source folder was not found above the test's build");
    }
}
