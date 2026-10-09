using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 (D88's note): which session branches the look's own tidy takes without a press, what it says of each, and how
/// often. The decision reads the clean-up's own judgement (<see cref="SweepItem"/>); only the empty kind with nothing in its
/// tree goes by itself, and every other kind stays a press. Nothing here starts a process: the git half is
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

    private string HomeTree => Path.Combine(_home, "trees", "default", "engine", "s-1a2b3c4d");

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
        // An ignored path the checkout also holds goes with the tree at the press (SQUASHTIDY1c); never by itself.
        { SweepKind.Empty, At.Home, 2, false, true, false },
        // A tree a person checked the branch out in is the person's, whatever it holds.
        { SweepKind.Empty, At.Outside, 0, false, true, false },
        // What git could not read keeps it.
        { SweepKind.Empty, At.Home, 0, true, true, false },
        // Every other kind stays exactly as it is: landed (ancestry, or content after a squash), carried by a completed pull
        // request, holding commits no branch of the person's holds, dirty, or in use.
        { SweepKind.Landed, At.None, 0, false, true, false },
        { SweepKind.Landed, At.Home, 0, false, true, false },
        { SweepKind.Carried, At.None, 0, false, true, false },
        { SweepKind.Unlanded, At.Home, 0, false, true, false },
        { SweepKind.Unlanded, At.None, 0, true, false, false },
        { SweepKind.Dirty, At.Home, 0, false, true, false },
        { SweepKind.Dirty, At.Home, 0, true, false, false },
        { SweepKind.InUse, At.Home, 0, false, true, false },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Only_an_empty_branch_with_nothing_in_its_tree_goes_by_itself(
        string kind, At at, int ignoredShared, bool unread, bool worked, bool goes)
    {
        var tree = at switch { At.Home => HomeTree, At.Outside => OutsideTree, _ => null };
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", tree, kind, 0, "main", null)
        {
            IgnoredShared = ignoredShared,
            Unread = unread,
            Worked = worked,
        };

        Assert.Equal(goes, new SessionTrees(_home).GoesByItself(item));
    }

    /// <summary>A squash merge's carried branch stays a press, however it was proven (SQUASHTIDY1b's offer, PLUGHOOK1a's carriers).</summary>
    [Fact]
    public void A_branch_landed_by_content_stays_a_press()
    {
        var item = new SweepItem("engine", "default", "daoris/s-1a2b3c4d", null, SweepKind.Landed, 1, "main", null)
        {
            HeldBy = new ContentHold("main", Squash: true),
        };

        Assert.False(new SessionTrees(_home).GoesByItself(item));
    }

    [Fact]
    public void A_removal_is_said_once_in_the_report_and_written_to_the_machine_log()
    {
        using var log = new MachineLog(_home, "driver", () => _now);
        var tidying = new BranchTidying(_home);
        var empty = Item("daoris/s-1a2b3c4d", HomeTree);
        var aside = Path.Combine(_home, "trees", ".tidied", "default", "engine", "s-1a2b3c4d-20261009T090000000Z");

        var said = tidying.Said([Pass("engine", new SweepResult(empty, true, "removed") { MovedTo = aside })], log);

        Assert.Equal(
            [$"tidy  engine: removed `daoris/s-1a2b3c4d`, which held nothing beyond `main`; its tree's folder was moved aside to {aside}, "
             + "and goes once it is left untouched for 14 days."],
            said);
        var line = Assert.Single(LogLines());
        Assert.Contains("\"level\":\"info\",\"event\":\"branch.tidied\"", line, StringComparison.Ordinal);
        Assert.Contains(
            "\"repository\":\"engine\",\"workspace\":\"default\",\"branch\":\"daoris/s-1a2b3c4d\",\"tree\":true,"
            + "\"folder\":\"trees/.tidied/default/engine/s-1a2b3c4d-20261009T090000000Z\"",
            line, StringComparison.Ordinal);
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

    /// <summary>Every other branch the look judged and kept (dirty, in use, a build's output) is the press's, and says nothing.</summary>
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
                new SweepResult(Item("daoris/s-3", HomeTree) with { IgnoredShared = 3 }, false, "kept")),
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
        var trees = Path.Combine(_home, "trees");
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

    private static TidyPass Pass(string repository, params SweepResult[] results) =>
        new(results) { Repository = repository, Workspace = "default" };

    private string[] LogLines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(StubFile.Lines)] : [];
    }
}
