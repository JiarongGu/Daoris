using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 (D88's note): which session branches the look's own tidy takes without a press, what it says of each, and how
/// often. The decision reads the clean-up's own judgement (<see cref="SweepItem"/>); every kind the press clears goes by itself
/// behind the look's guards (AUTOTIDY2a), and the rest stay a press. Nothing here starts a process: the git half is
/// <see cref="BranchTidyingProcessTests"/>'.
/// </summary>
public sealed class BranchTidyingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-tidying-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    public BranchTidyingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string HomeTree => DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "s-1a2b3c4d");

    private string OutsideTree => Path.Combine(Path.GetTempPath(), "a-checkout-of-the-persons", "engine");

    /// <summary>Where the row's tree is: none, one this home opened, or one a person checked the branch out in.</summary>
    public enum At { None, Home, Outside }

    /// <summary>
    /// The table: each kind the clean-up sorts a session branch into, where its tree is, the ignored paths it shares with the
    /// checkout (a build's output), whether git could not answer a guard, whether it made commits the line then took, and
    /// whether the look takes it without a press.
    /// </summary>
    public static TheoryData<string, At, int, bool, bool, bool> Table => new()
    {
        // Nothing beyond the line: with no tree it goes, whether or not it ever made a commit (a start's failed open left
        // sixteen such, FG5).
        { SweepKind.Empty, At.None, 0, false, false, true },
        { SweepKind.Empty, At.None, 0, false, true, true },
        // With its tree, it goes once its work reached the line and the tree holds nothing at all: the row's own case, a pull
        // request merged.
        { SweepKind.Empty, At.Home, 0, false, true, true },
        // A tree whose branch never moved is a conversation's place, which words to its session go on in (D137): the press's.
        { SweepKind.Empty, At.Home, 0, false, false, false },
        // An ignored path the checkout also holds goes with the tree, moved aside for fourteen days (AUTOTIDY2a).
        { SweepKind.Empty, At.Home, 2, false, true, true },
        // A tree a person checked the branch out in is the person's, whatever it holds.
        { SweepKind.Empty, At.Outside, 0, false, true, false },
        // What git could not read keeps it.
        { SweepKind.Empty, At.Home, 0, true, true, false },
        // AUTOTIDY2a: every other kind the press clears goes too, behind the same guards: landed (ancestry, or content after a
        // squash) and carried by a completed pull request.
        { SweepKind.Landed, At.None, 0, false, false, true },
        { SweepKind.Landed, At.Home, 0, false, true, true },
        { SweepKind.Landed, At.Home, 3, false, true, true },
        { SweepKind.Landed, At.Home, 0, false, false, false },
        { SweepKind.Landed, At.Outside, 0, false, true, false },
        { SweepKind.Landed, At.Home, 0, true, true, false },
        { SweepKind.Carried, At.None, 0, false, true, true },
        { SweepKind.Carried, At.Home, 0, false, true, true },
        { SweepKind.Carried, At.Home, 0, false, false, false },
        // What the press keeps stays exactly as it is: commits no branch of the person's holds, dirty, or in use.
        { SweepKind.Unlanded, At.Home, 0, false, true, false },
        { SweepKind.Unlanded, At.None, 0, true, false, false },
        { SweepKind.Dirty, At.Home, 0, false, true, false },
        { SweepKind.Dirty, At.Home, 0, true, false, false },
        { SweepKind.InUse, At.Home, 0, false, true, false },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Every_kind_the_press_clears_goes_by_itself_behind_the_look_s_guards(
        string kind, At at, int ignoredShared, bool unread, bool worked, bool goes)
    {
        var tree = at switch { At.Home => HomeTree, At.Outside => OutsideTree, _ => null };
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", tree, kind, 0, "main", null)
        {
            IgnoredShared = ignoredShared,
            Unread = unread,
            Worked = worked,
            // A landed row here is the line's: its work is on the line itself (AUTOTIDY2a's wait is the next theory's).
            OnLine = kind == SweepKind.Landed,
        };

        Assert.Equal(goes, new SessionTrees(_home).GoesByItself(item));
    }

    /// <summary>
    /// AUTOTIDY2a: a landed branch goes by itself only once its work is on the line. Landed only on another branch of the
    /// person's (the branch Accept's landing made, one the person pushed), its pull request still open, the person may still say
    /// words to its session in its own tree: the look waits for the line, and the press may take it earlier by the person's choice.
    /// </summary>
    [Theory]
    [InlineData(At.Home, false)]
    [InlineData(At.None, false)]
    [InlineData(At.Home, true)]
    [InlineData(At.None, true)]
    public void A_landed_branch_goes_by_itself_only_once_the_line_has_it(At at, bool onLine)
    {
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", at == At.Home ? HomeTree : null, SweepKind.Landed, 2, "feature/q1-fix", null)
        {
            Worked = true,
            OnLine = onLine,
        };

        Assert.True(item.Removable);
        Assert.Equal(onLine, new SessionTrees(_home).GoesByItself(item));
    }

    /// <summary>
    /// TREEFOLLOW1: an agent that moved its tree onto a branch of its own leaves the session branch behind, which no tree has out.
    /// Empty it goes (the tree stays, on its branch), and the report says where the work is; carried it goes as the press would
    /// take it (AUTOTIDY2a); landed on the tree's own branch, which the line does not hold yet, it waits for the line; holding
    /// commits no branch of the person's holds it stays.
    /// </summary>
    [Theory]
    [InlineData(SweepKind.Empty, true)]
    [InlineData(SweepKind.Landed, false)]
    [InlineData(SweepKind.Carried, true)]
    [InlineData(SweepKind.Unlanded, false)]
    public void A_session_branch_whose_tree_moved_on_goes_as_the_press_would_take_it(string kind, bool goes)
    {
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", null, kind, 0, "main", null) { TreeOn = "feature/x" };

        Assert.Equal(goes, new SessionTrees(_home).GoesByItself(item));
    }

    [Fact]
    public void A_removed_branch_whose_tree_moved_on_says_where_the_session_s_work_is()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var moved = Item("daoris/s-1a2b3c4d") with { TreeOn = "feature/x" };

        var said = tidying.Said([Pass("engine", new SweepResult(moved, true, "removed"))], log);

        Assert.Equal(["tidy  engine: removed `daoris/s-1a2b3c4d`, which held nothing beyond `main`; the session's work is on `feature/x`."], said);
        Assert.Contains("\"on\":\"feature/x\"", Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>TREEFOLLOW1: the tree's current branch is an additive field of the ledger's entry, kept as written and absent when unset.</summary>
    [Fact]
    public void The_ledger_keeps_the_branch_a_tree_moved_onto_beside_where_the_session_branch_grew_from()
    {
        var ledger = new SessionBranches(_home);
        var grown = new GrownBranch("engine", "default", "daoris/s-1a2b3c4d", "main", "abc123", null, _now);
        ledger.Record(grown);
        Assert.Null(Assert.Single(ledger.All()).Now);
        Assert.DoesNotContain("\"now\"", File.ReadAllText(ledger.FilePath), StringComparison.Ordinal);

        ledger.Record(grown with { Now = "feature/x" });

        Assert.Equal("feature/x", ledger.Of("engine", "daoris/s-1a2b3c4d")!.Now);
        Assert.Contains("\"now\": \"feature/x\"", File.ReadAllText(ledger.FilePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// A squash merge's branch landed by content on the line goes by itself as the press takes it (AUTOTIDY2a), its commits kept
    /// on a ref of their own by the press's one path; a tree whose branch never moved stays the press's, as an empty one's does;
    /// and work a cherry-pick put on another branch of the person's waits for the line.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void A_branch_landed_by_content_goes_by_itself_once_the_line_holds_it(bool worked, bool onLine, bool goes)
    {
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", HomeTree, SweepKind.Landed, 1, "main", null)
        {
            HeldBy = new ContentHold(onLine ? "main" : "feature/x", Squash: onLine),
            Worked = worked,
            OnLine = onLine,
        };

        Assert.Equal(goes, new SessionTrees(_home).GoesByItself(item));
    }

    /// <summary>
    /// AUTOTIDY2a: a removal says why its kind let it go, as the clean-up's list words the kind: landed on a branch of the
    /// person's, landed by content with where its commits are kept, or carried by a completed pull request. The log names the
    /// kind, and the ref a content-held removal kept its commits on, which holds no path.
    /// </summary>
    [Fact]
    public void A_removal_says_why_its_kind_let_it_go_and_logs_the_kind()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine", "s-1-20261009T090000000Z");
        var landed = Item("daoris/s-1", HomeTree) with { Kind = SweepKind.Landed, Commits = 2, Where = "origin/main", OnLine = true };
        var held = Item("daoris/s-2") with { Kind = SweepKind.Landed, Commits = 1, HeldBy = new ContentHold("main", Squash: true) };
        var carried = Item("daoris/s-3") with { Kind = SweepKind.Carried, Commits = 1, Where = "feature/q1-fix" };
        const string Ref = "refs/daoris/discarded/daoris/s-2";

        var said = tidying.Said(
        [
            Pass("engine",
                new SweepResult(landed, true, "removed") { MovedTo = aside },
                new SweepResult(held, true, "removed") { KeptRef = Ref },
                new SweepResult(carried, true, "removed")),
        ], log);

        Assert.Equal(
        [
            $"tidy  engine: removed `daoris/s-1`, since its work is on `origin/main`; its tree's folder was moved aside to {aside}, "
            + "and goes once it is left untouched for 14 days.",
            "tidy  engine: removed `daoris/s-2`, since its work is on `main` by content (a squash merge). "
            + $"Its commits stay at `{Ref}` until you delete that ref; `git branch daoris/s-2 {Ref}` brings the branch back.",
            "tidy  engine: removed `daoris/s-3`, since `feature/q1-fix`'s completed pull request carried its work.",
        ], said);
        var lines = LogLines();
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"kind\":\"landed\",\"kept\":null", lines[0], StringComparison.Ordinal);
        Assert.Contains($"\"kind\":\"landed\",\"kept\":\"{Ref}\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"carried\",\"kept\":null", lines[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTOTIDY2b: a branch a landing made, taken by the look, is said in the report by its name and what proved its work is on
    /// the line, and in the machine log by its session alone: its name, and the ref its commits are kept on, carry the title's
    /// words, which the log never holds (the machine-log design §5). A removal that did not happen is said once, as a session
    /// branch's is.
    /// </summary>
    [Fact]
    public void A_landing_s_branch_is_said_by_name_in_the_report_and_by_its_session_in_the_log()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        const string Squashed = "feature/q2-fix-the-login";
        const string Ref = "refs/daoris/discarded/" + Squashed;
        var moved = new LandedResult(Landing("feature/q5-moved", LandedKind.OnLine, "main"), false, "it changed since the list, and is kept") { Session = "s5" };
        var pass = Pass("engine") with
        {
            Landed =
            [
                new LandedResult(Landing("feature/q1-add-a-page", LandedKind.Merged, "main"), true, "removed") { Session = "s1" },
                new LandedResult(Landing(Squashed, LandedKind.OnLine, "origin/main"), true, "removed") { Session = "s2", KeptRef = Ref },
                new LandedResult(Landing("feature/q3-a-step", LandedKind.Inside, Squashed), true, "removed") { Session = "s3", KeptRef = Ref + "-2" },
                new LandedResult(Landing("feature/q4-the-api", LandedKind.PullRequest, "origin/main"), true, "removed") { Session = "s4", KeptRef = Ref + "-3" },
                moved,
            ],
        };

        var said = tidying.Said([pass], log);
        var again = tidying.Said([Pass("engine") with { Landed = [moved] }], log);

        string Kept(string branch, string at) => $" Its commits stay at `{at}` until you delete that ref; `git branch {branch} {at}` brings the branch back.";
        Assert.Equal(
        [
            "tidy  engine: removed `feature/q1-add-a-page`, a branch a landing made, since its work is on `main`.",
            $"tidy  engine: removed `{Squashed}`, a branch a landing made, since its work is on `origin/main` by content (each file it changed reads the same there)."
            + Kept(Squashed, Ref),
            $"tidy  engine: removed `feature/q3-a-step`, a branch a landing made, since it is inside `{Squashed}`, whose work is on the line."
            + Kept("feature/q3-a-step", Ref + "-2"),
            "tidy  engine: removed `feature/q4-the-api`, a branch a landing made, since its pull request completed and its merge commit is on `origin/main`."
            + Kept("feature/q4-the-api", Ref + "-3"),
            "tidy  engine: `feature/q5-moved` stays, since it changed since the list.",
        ], said);
        Assert.Empty(again);
        var lines = LogLines();
        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.DoesNotContain("feature/", line, StringComparison.Ordinal));
        Assert.Contains("\"branch\":null,\"tree\":false,\"folder\":null,\"on\":null,\"kind\":\"merged\",\"kept\":null,\"session\":\"s1\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"on-line\",\"kept\":null,\"session\":\"s2\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"inside\",\"kept\":null,\"session\":\"s3\"", lines[2], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"pull-request\",\"kept\":null,\"session\":\"s4\"", lines[3], StringComparison.Ordinal);
        Assert.Contains("\"event\":\"branch.kept\"", lines[4], StringComparison.Ordinal);
        Assert.Contains("\"branch\":null,\"why\":\"refused\",\"session\":\"s5\"", lines[4], StringComparison.Ordinal);
    }

    /// <summary>What holds a whole repository holds every session branch in it now, not only the empty ones (AUTOTIDY2a).</summary>
    [Fact]
    public void A_repository_held_says_its_session_branches_stay()
    {
        var held = new TidyPass([], TidyKept.Sessions, "the service did not answer") { Repository = "engine", Workspace = "default" };

        var said = new BranchTidying(_home).Said([held], null);

        Assert.Equal(
            ["tidy  engine: its session branches stay this look, since the service did not say which sessions run: the service did not answer."],
            said);
    }

    [Fact]
    public void A_removal_is_said_once_in_the_report_and_written_to_the_machine_log()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var empty = Item("daoris/s-1a2b3c4d", HomeTree);
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine", "s-1a2b3c4d-20261009T090000000Z");

        var said = tidying.Said([Pass("engine", new SweepResult(empty, true, "removed") { MovedTo = aside })], log);

        Assert.Equal(
            [$"tidy  engine: removed `daoris/s-1a2b3c4d`, which held nothing beyond `main`; its tree's folder was moved aside to {aside}, "
             + "and goes once it is left untouched for 14 days."],
            said);
        var line = Assert.Single(LogLines());
        Assert.Contains("\"level\":\"info\",\"event\":\"branch.tidied\"", line, StringComparison.Ordinal);
        Assert.Contains(
            "\"repository\":\"engine\",\"workspace\":\"default\",\"branch\":\"daoris/s-1a2b3c4d\",\"tree\":true,"
            + "\"folder\":\"work/trees/.tidied/default/engine/s-1a2b3c4d-20261009T090000000Z\"",
            line, StringComparison.Ordinal);
    }

    /// <summary>
    /// KEEP1d: the report's two readers of the set-aside days, a tree's folder and a stray folder moved aside, say the days the person set
    /// in <c>driver.json</c>'s <c>keep</c>, read through the one effective policy.
    /// </summary>
    [Fact]
    public void The_report_says_the_set_aside_days_the_person_set()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        DriverConfig.Load(config).WithKeep(new KeepSet("set-aside", Days: 30)).Save(config);
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine", "s-1a2b3c4d-20261009T090000000Z");
        var stray = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine", "devtools-20261009T090000000Z");

        var said = new BranchTidying(_home).Said(
            [
                Pass("engine", new SweepResult(Item("daoris/s-1a2b3c4d", HomeTree), true, "removed") { MovedTo = aside }) with
                {
                    Strays = [new StrayResult(DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "devtools"), StrayKind.Folder, true, "") { MovedTo = stray }],
                },
            ],
            null);

        Assert.Equal(2, said.Count);
        Assert.All(said, line => Assert.EndsWith("and goes once it is left untouched for 30 days.", line, StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTOTIDY2c: a folder under the trees that git knows no tree in, moved aside, is said with where it went, and logged as
    /// <c>branch.tidied</c> with no branch, <c>tree</c> true, the folder it went to from the home, and <c>kind</c> <c>stray</c>.
    /// </summary>
    [Fact]
    public void A_folder_moved_aside_is_said_and_logged_with_no_branch()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "devtools");
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine", "devtools-20261009T090000000Z");

        var said = new BranchTidying(_home).Said(
            [Pass("engine") with { Strays = [new StrayResult(folder, StrayKind.Folder, true, "") { MovedTo = aside }] }], log);

        Assert.Equal(
            [$"tidy  engine: `devtools`, a folder under its trees that git knows no tree in, was moved aside to {aside}, and goes once it is left untouched for 14 days."],
            said);
        var line = Assert.Single(LogLines());
        Assert.Contains("\"level\":\"info\",\"event\":\"branch.tidied\"", line, StringComparison.Ordinal);
        Assert.Contains(
            "\"repository\":\"engine\",\"workspace\":\"default\",\"branch\":null,\"tree\":true,"
            + "\"folder\":\"work/trees/.tidied/default/engine/devtools-20261009T090000000Z\",\"on\":null,\"kind\":\"stray\",\"kept\":null",
            line, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPENCUT1: a tree whose opening was cut off, moved aside with its branch removed, is said by its branch and why its lock was
    /// a cut-off opening's, and logged as <c>branch.tidied</c> with that branch and <c>kind</c> <c>opening</c>.
    /// </summary>
    [Fact]
    public void A_tree_whose_opening_was_cut_off_is_said_and_logged_by_its_branch()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "s-0031408d");
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine", "s-0031408d-20261009T090000000Z");
        var cut = new StrayResult(folder, StrayKind.Opening, true, "git's own lock from the start of its add was left on it")
        {
            MovedTo = aside, Branch = "daoris/s-0031408d",
        };

        var said = new BranchTidying(_home).Said([Pass("engine") with { Strays = [cut] }], log);

        Assert.Equal(
            [$"tidy  engine: removed `daoris/s-0031408d`, whose tree's opening was cut off: git's own lock from the start of its add was left on it; "
             + $"its tree's folder was moved aside to {aside}, and goes once it is left untouched for 14 days."],
            said);
        // The folder said relative to the home: under `work/trees/` in a new home (HOMELAYOUT3).
        Assert.Contains(
            "\"branch\":\"daoris/s-0031408d\",\"tree\":true,\"folder\":\"work/trees/.tidied/default/engine/s-0031408d-20261009T090000000Z\","
            + "\"on\":null,\"kind\":\"opening\",\"kept\":null",
            Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>A folder under the trees that could not be moved aside is said once by its code, with no branch, while it lasts.</summary>
    [Fact]
    public void A_folder_under_the_trees_kept_is_said_once_by_its_code()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var busy = new StrayResult(
            DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "devtools"), StrayKind.Folder, false,
            "something on this machine is using it, so it could not be moved aside (in use); a later look tries again") { Kept = TidyKept.Busy };

        var said = tidying.Said([Pass("engine") with { Strays = [busy] }], log);
        var again = tidying.Said([Pass("engine") with { Strays = [busy] }], log);

        Assert.Equal(
            ["tidy  engine: the folder `devtools` under its trees stays, since something on this machine is using it, so it could not be moved aside (in use); a later look tries again."],
            said);
        Assert.Empty(again);
        var line = Assert.Single(LogLines());
        Assert.Contains("\"event\":\"branch.kept\"", line, StringComparison.Ordinal);
        Assert.Contains("\"branch\":null,\"why\":\"busy\"", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPENCUT1: a lock is an opening cut off only where it is git's own from an add, or Daoris's naming a process that no longer
    /// runs; a person's lock, with any other reason or none, never is.
    /// </summary>
    [Theory]
    [InlineData(TreeOpenings.GitsOwn, false, true)]
    [InlineData("Daoris is opening this tree (process 4242)", false, true)]
    [InlineData("Daoris is opening this tree (process 4242)", true, false)]
    [InlineData("Daoris is opening this tree (process )", false, false)]
    [InlineData("a person's", false, false)]
    [InlineData(null, false, false)]
    public void Only_an_opening_s_lock_no_running_process_holds_is_cut_off(string? reason, bool running, bool cutOff)
    {
        Assert.Equal(cutOff, TreeOpenings.CutOff(reason, process => process == 4242 && running));
        Assert.Equal("Daoris is opening this tree (process 4242)", TreeOpenings.LockReason(4242));
    }

    /// <summary>A last guard's keep is said once, by its own code: a tree in use (busy), a hidden change, a nested repository, a link.</summary>
    [Theory]
    [InlineData(TidyKept.Busy, "something on this machine is using its tree, so its folder could not be moved aside (in use); a later look tries again")]
    [InlineData(TidyKept.Hidden, "1 tracked path(s) in its tree are marked assume-unchanged or skip-worktree, which hides their changes from git: README.md")]
    [InlineData(TidyKept.Nested, "its tree holds another repository at docs/inner")]
    [InlineData(TidyKept.Linked, "its tree is reached through a link under the trees home")]
    public void A_last_guard_s_keep_is_said_once_by_its_code(string code, string why)
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var kept = new SweepResult(Item("daoris/s-1a2b3c4d", HomeTree), false, why) { Kept = code };

        var first = tidying.Said([Pass("engine", kept)], log);
        var second = tidying.Said([Pass("engine", kept)], log);

        Assert.Equal([$"tidy  engine: `daoris/s-1a2b3c4d` stays, since {why}."], first);
        Assert.Empty(second);
        Assert.Contains($"\"why\":\"{code}\"", Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>
    /// What git could not read keeps the branch, and the look says why once: the next look meets the same and says nothing, so
    /// a branch that stays for weeks is one line, not one a look.
    /// </summary>
    [Fact]
    public void A_branch_kept_because_git_could_not_read_a_guard_is_said_once_not_every_look()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var unread = Item("daoris/s-1a2b3c4d", HomeTree) with { Kind = SweepKind.Dirty, Detail = "git could not say what its tree holds" };
        var look = () => tidying.Said([Pass("engine", new SweepResult(unread with { Unread = true }, false, "kept"))], log);

        var first = look();
        var second = look();

        Assert.Equal(
            ["tidy  engine: `daoris/s-1a2b3c4d` stays, since git could not say what its tree holds."],
            first);
        Assert.Empty(second);
        var line = Assert.Single(LogLines());
        Assert.Contains("\"level\":\"warn\",\"event\":\"branch.kept\"", line, StringComparison.Ordinal);
        Assert.Contains("\"branch\":\"daoris/s-1a2b3c4d\",\"why\":\"unread\"", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each path that keeps a branch as <c>unread</c> logs which one it was as a code (AUTOTIDY1a2), so the page can word why; the
    /// sentence, with its folder, stays the console's, never the log's data (D47 §4).
    /// </summary>
    [Theory]
    [InlineData(TidyCause.Folder)]
    [InlineData(TidyCause.List)]
    [InlineData(TidyCause.Index)]
    [InlineData(TidyCause.Folders)]
    public void A_branch_kept_unread_logs_which_path_it_was_by_code_and_no_path(string cause)
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var kept = new SweepResult(Item("daoris/s-1a2b3c4d"), false, $"its folder at {_home} is here") { Kept = TidyKept.Unread, Cause = cause };

        tidying.Said([Pass("engine", kept)], log);

        var line = Assert.Single(LogLines());
        Assert.Contains($"\"why\":\"unread\",\"cause\":\"{cause}\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(_home), line, StringComparison.Ordinal);
    }

    /// <summary>A repository held unread names its path too: git's top level, its refs, or its working trees' list.</summary>
    [Theory]
    [InlineData(TidyCause.Toplevel)]
    [InlineData(TidyCause.Refs)]
    [InlineData(TidyCause.List)]
    public void A_repository_held_unread_logs_its_cause(string cause)
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var held = new TidyPass([], TidyKept.Unread, "git said no") { Repository = "engine", Workspace = "default", Cause = cause };

        new BranchTidying(_home).Said([held], log);

        Assert.Contains($"\"why\":\"unread\",\"cause\":\"{cause}\"", Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>
    /// A pass that threw is held unread with the code <c>pass</c> alone (AUTOTIDY1a3): the exception's message can carry a path, so
    /// neither it nor the exception's type is logged.
    /// </summary>
    [Fact]
    public void A_pass_that_threw_logs_the_pass_code_and_no_message()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var held = BranchTidying.Threw("engine", "default", new IOException($"cannot read {_home}"));

        new BranchTidying(_home).Said([held], log);

        var line = Assert.Single(LogLines());
        Assert.Contains($"\"why\":\"unread\",\"cause\":\"{TidyCause.Pass}\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(_home), line, StringComparison.Ordinal);
        Assert.DoesNotContain("IOException", line, StringComparison.Ordinal);
    }

    /// <summary>An item the judgement could not read (git gave no answer for its tree, tip or log) logs <c>judged</c> (AUTOTIDY1a3).</summary>
    [Fact]
    public void An_item_the_judgement_could_not_read_logs_the_judged_code()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var unread = Item("daoris/s-1a2b3c4d", HomeTree) with { Kind = SweepKind.Unlanded, Detail = $"git could not tell: {_home}", Unread = true };

        new BranchTidying(_home).Said([Pass("engine", new SweepResult(unread, false, "it changed since the list, and is kept"))], log);

        var line = Assert.Single(LogLines());
        Assert.Contains($"\"why\":\"unread\",\"cause\":\"{TidyCause.Judged}\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(_home), line, StringComparison.Ordinal);
    }

    /// <summary>A why that is not <c>unread</c> carries no cause, and a keep with none logs none (a field added, the old read kept).</summary>
    [Fact]
    public void Another_why_logs_no_cause()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var kept = new SweepResult(Item("daoris/s-1a2b3c4d", HomeTree), false, "its tree holds another repository at x") { Kept = TidyKept.Nested };

        new BranchTidying(_home).Said([Pass("engine", kept)], log);

        Assert.DoesNotContain("cause", Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>A removal git refused (a locked tree among them) is said once with git's words, as the press says it.</summary>
    [Fact]
    public void A_removal_git_refused_is_said_once_with_the_press_s_sentence()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var refused = new SweepResult(Item("daoris/s-1a2b3c4d", HomeTree), false, "git would not remove its tree: fatal: cannot remove a locked working tree");

        var first = tidying.Said([Pass("engine", refused)], log);
        var second = tidying.Said([Pass("engine", refused)], log);

        Assert.Equal(["tidy  engine: `daoris/s-1a2b3c4d` stays, since git would not remove its tree: fatal: cannot remove a locked working tree."], first);
        Assert.Empty(second);
        Assert.Contains("\"why\":\"refused\"", Assert.Single(LogLines()), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every other branch the look judged and kept (dirty, in use, commits no branch of the person's holds) is the press's, and
    /// says nothing: the look now judges every session branch (AUTOTIDY2a), and most of them are these.
    /// </summary>
    [Fact]
    public void A_branch_kept_for_what_it_holds_says_nothing()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);

        var said = tidying.Said(
        [
            Pass("engine",
                new SweepResult(Item("daoris/s-1") with { Kind = SweepKind.Dirty, Detail = "1 path(s) uncommitted" }, false, "kept"),
                new SweepResult(Item("daoris/s-2", HomeTree) with { Kind = SweepKind.InUse }, false, "kept"),
                new SweepResult(Item("daoris/s-3", HomeTree) with { Kind = SweepKind.Unlanded, Commits = 2 }, false, "kept")),
        ], log);

        Assert.Empty(said);
        Assert.Empty(LogLines());
    }

    /// <summary>
    /// What held a whole repository this look (a session starting in one of its trees, a ledger that did not answer) is said
    /// once while it lasts, and again only once it held again after a look it did not.
    /// </summary>
    [Fact]
    public void A_repository_held_is_said_once_while_it_lasts_and_again_after_it_cleared()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var held = new TidyPass([], TidyKept.Starting) { Repository = "engine", Workspace = "default" };

        var first = tidying.Said([held], log);
        var again = tidying.Said([held], log);
        var cleared = tidying.Said([Pass("engine")], log);
        var heldAgain = tidying.Said([held], log);

        Assert.Equal(
            ["tidy  engine: a session was starting in one of `engine`'s trees, so its branches were left as they were for another look."],
            first);
        Assert.Empty(again);
        Assert.Empty(cleared);
        Assert.Equal(first, heldAgain);
        Assert.Equal(2, LogLines().Count(line => line.Contains("\"why\":\"starting\"", StringComparison.Ordinal)));
    }

    /// <summary>The look tidies once each pace, at its first look, whatever the loop's own pace.</summary>
    [Fact]
    public void A_pass_is_due_at_the_first_look_and_then_once_each_pace()
    {
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.FromMinutes(5), Clock = () => _now };

        Assert.True(tidying.Due);
        tidying.Began();
        Assert.False(tidying.Due);
        _now = _now.AddMinutes(4);
        Assert.False(tidying.Due);
        _now = _now.AddMinutes(1);
        Assert.True(tidying.Due);
    }

    /// <summary>
    /// A review step in progress (REVIEWENV1d) keeps the tree it shows from: a build's folder served in it, or the tree a set-up
    /// waiting for the person was said from, counts as a session in use, whichever of its folders is named.
    /// </summary>
    [Fact]
    public void A_tree_a_review_shows_from_is_in_use_whatever_folder_of_it_is_named()
    {
        var trees = DaorisHome.Entry(_home, HomeEntry.Trees);
        var served = Path.Combine(trees, "default", "engine", "s-1a2b3c4d", "app", "dist");
        var waiting = Path.Combine(trees, "tools", "studio", "s-5e6f7a8b");
        var checkout = Path.Combine(Path.GetTempPath(), "a-checkout", "engine");

        var inUse = BranchTidying.TreesOf(trees, [served, waiting, checkout]);

        Assert.Equal(
            [Path.Combine(trees, "default", "engine", "s-1a2b3c4d"), waiting, checkout],
            inUse);
    }

    /// <summary>
    /// A set-up waiting for the person's verdict names the tree of the session that said it, read from the service's records as
    /// the review's gate reads them, with a desk or none: the headless host has none, and <i>Show it again</i> serves from that
    /// tree. A shell's desk adds each build it serves now.
    /// </summary>
    [Fact]
    public async Task A_set_up_waiting_for_the_person_names_its_tree_with_a_desk_or_none()
    {
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Working("s-setup", "engine", HomeTree, state: "completed");
        ledger.SetUpShown("q1", "s-setup");
        var folder = Path.Combine(HomeTree, "dist");
        var desk = new ReviewDesk(new ServingTabs(
            new ReviewServe("q1", ReviewDesk.TabTitle("q1"), folder, "http://localhost:5173", "/", "http://localhost:5173/")));

        Assert.Equal([HomeTree], await BranchTidying.ReviewingAsync(null, ledger.Client(), CancellationToken.None));
        Assert.Equal([folder, HomeTree], await BranchTidying.ReviewingAsync(desk, ledger.Client(), CancellationToken.None));
    }

    /// <summary>
    /// AUTOTIDY2d: a record that ended with the person's words still waiting on it names its tree, which the next loop goes on
    /// in (MSG1b, D137 §2.2); an ended record with none, and a running one (the active list's), name none here.
    /// </summary>
    [Fact]
    public async Task A_record_ended_with_words_waiting_names_its_tree()
    {
        using var ledger = new StandInLedger();
        var waiting = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "s-5e6f7a8b");
        ledger.Working("s-waiting", "engine", waiting, state: "completed");
        ledger.Working("s-done", "engine", HomeTree, state: "completed");
        ledger.Working("s-running", "engine", DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "s-9a8b7c6d"));
        using var client = ledger.Client();
        Assert.NotNull((await client.SayAsync("s-waiting", "fix the review comment")).Word);

        Assert.Equal([waiting], await client.WaitingTreesAsync());
    }

    /// <summary>
    /// 🔴 HELPERWAIT1 (Codex's #5 on COWORKUX1): a session that ended waiting on its own helper keeps its tree from the tidy while an ask of
    /// its own is open, since it goes on there with the answer; once none is open (handed, or refused and it came back to the person) it
    /// holds nothing of its own, and one that ended any other way never did.
    /// </summary>
    [Fact]
    public async Task A_session_waiting_on_its_own_helper_keeps_its_tree_while_its_ask_is_open()
    {
        using var ledger = new StandInLedger();
        var waiting = DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-5e6f7a8b");
        ledger.Working("s-waiting", "engine", waiting, state: "completed");
        ledger.Noted("s-waiting", "ended.awaits-helper");
        ledger.Working("s-done", "engine", HomeTree, state: "completed");
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(files.PathOf("a1b2c3d4", "ask"), new JsonObject
        {
            ["id"] = "a1b2c3d4", ["asker"] = "s-waiting", ["question"] = "Why?", ["agent"] = null, ["askedBy"] = HelperAsk.ByAgent,
            ["at"] = DateTimeOffset.UtcNow.ToString("O"),
        }.ToJsonString());
        File.WriteAllText(files.PathOf("e5f6a7b8", "ask"), new JsonObject
        {
            ["id"] = "e5f6a7b8", ["asker"] = "s-done", ["question"] = "Why?", ["agent"] = null, ["askedBy"] = HelperAsk.ByAgent,
            ["at"] = DateTimeOffset.UtcNow.ToString("O"),
        }.ToJsonString());
        using var client = ledger.Client();

        Assert.Equal([waiting], await BranchTidying.InUseAsync(_home, client, null, CancellationToken.None));

        files.WriteState(new HelperState("a1b2c3d4", HelperStates.Refused, HelperWhy.NoAnswer, null, DateTimeOffset.UtcNow));
        Assert.Empty(await BranchTidying.InUseAsync(_home, client, null, CancellationToken.None));
    }

    /// <summary>With no set-up waiting and no desk, nothing is reviewed here.</summary>
    [Fact]
    public async Task With_no_set_up_waiting_and_no_desk_nothing_is_reviewed()
    {
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");

        Assert.Empty(await BranchTidying.ReviewingAsync(null, ledger.Client(), CancellationToken.None));
    }

    /// <summary>A browser's half that serves what it was handed, and opens nothing.</summary>
    private sealed class ServingTabs(params ReviewServe[] serving) : IReviewTabs
    {
        public IReadOnlyList<ReviewServe> Serving => serving;

        public Task OpenAsync(string quest, string title, CancellationToken ct = default) => Task.CompletedTask;

        public Task ServeAsync(ReviewServe serve, CancellationToken ct = default) => Task.CompletedTask;

        public void Stop(string quest)
        {
        }
    }

    private static SweepItem Item(string branch, string? tree = null) =>
        new("engine", "default", branch, tree, SweepKind.Empty, 0, "main", null);

    /// <summary>A branch a landing made, as the landed group's judgement leaves it (AUTOTIDY2b).</summary>
    private static LandedItem Landing(string branch, string kind, string where) =>
        new("engine", "default", branch, kind, where, [], null, null, 1);

    private static TidyPass Pass(string repository, params SweepResult[] results) =>
        new(results) { Repository = repository, Workspace = "default" };

    private string[] LogLines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(StubFile.Lines)] : [];
    }
}
