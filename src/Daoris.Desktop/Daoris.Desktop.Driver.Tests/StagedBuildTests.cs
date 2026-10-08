using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UPDATE1 (D139 §4–§6): a build staged beside an install is checked before anything is replaced, swapped in by the
/// launcher with a journal while the application is closed, and rolled back to the build before it when it fails.
/// </summary>
/// <remarks>
/// Real folders in a scratch install, with the application's start and liveness handed in: nothing here starts a process.
/// The launcher compiles the same <see cref="StagedBuild"/> source, so what is held here is what it runs.
/// </remarks>
public sealed class StagedBuildTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly string _install = Path.Combine(Path.GetTempPath(), "daoris-staged-" + Guid.NewGuid().ToString("N")[..8]);

    public StagedBuildTests()
    {
        Write("INSTALLED.md", "# Daoris — installed desktop\nold\n");
        Write("Daoris.exe", "old launcher");
        Write("app/Daoris.Desktop.exe", "old application");
        Write("app/Daoris.Desktop.App.dll", "old library");
        Write("data/driver.json", "{ \"drivable\": [] }\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_install, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ——— The check (§4).

    [Fact]
    public void Nothing_staged_is_no_build_and_no_problem()
    {
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.Null(StagedBuild.Read(_install, out var problem));
        Assert.Null(problem);
        Assert.Null(StagedBuild.Check(_install));
    }

    [Fact]
    public void A_build_whose_files_are_its_manifest_s_passes_the_check_and_reads_its_identity()
    {
        Stage();

        Assert.Null(StagedBuild.Check(_install));
        var manifest = StagedBuild.Read(_install, out _)!;
        Assert.Equal("b1", manifest.Id);
        Assert.Equal("0.0.1", manifest.Version);
        Assert.Equal("abc1234", manifest.Commit);
        Assert.Equal(4, manifest.Files.Count);
    }

    [Fact]
    public void A_file_the_manifest_names_and_the_build_lacks_is_missing()
    {
        Stage();
        File.Delete(Staged("app/Daoris.Desktop.App.dll"));

        Assert.Equal("missing", StagedBuild.Check(_install)?.Code);
    }

    [Fact]
    public void A_file_of_another_size_is_refused_by_its_size()
    {
        Stage();
        File.AppendAllText(Staged("app/Daoris.Desktop.exe"), "!");

        Assert.Equal("size", StagedBuild.Check(_install)?.Code);
    }

    [Fact]
    public void A_file_of_the_same_size_and_other_bytes_is_refused_by_its_hash()
    {
        Stage();
        File.WriteAllText(Staged("app/Daoris.Desktop.exe"), "NEW application");

        Assert.Equal("hash", StagedBuild.Check(_install)?.Code);
    }

    [Fact]
    public void A_file_the_manifest_does_not_name_is_refused()
    {
        Stage();
        File.WriteAllText(Staged("app/stray.dll"), "x");

        var problem = StagedBuild.Check(_install);
        Assert.Equal("unlisted", problem?.Code);
        Assert.Contains("app/stray.dll", problem!.Sentence);
    }

    [Fact]
    public void A_build_without_the_application_s_library_is_refused_whatever_its_manifest_says()
    {
        Stage();
        File.Delete(Staged("app/Daoris.Desktop.App.dll"));
        Manifest();

        var problem = StagedBuild.Check(_install);
        Assert.Equal("required", problem?.Code);
        Assert.Contains("app/Daoris.Desktop.App.dll", problem!.Sentence);
    }

    [Fact]
    public void An_install_carrying_its_host_refuses_a_build_staged_without_one()
    {
        Write("app/daoris-knowledge-http/daoris-knowledge-http.exe", "old host");
        Stage();

        Assert.Equal("host", StagedBuild.Check(_install)?.Code);

        Write("update/staged/app/daoris-knowledge-http/daoris-knowledge-http.exe", "new host");
        Manifest();
        Assert.Null(StagedBuild.Check(_install));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("app\\back.dll")]
    [InlineData("app//double.dll")]
    [InlineData("C:/drive.dll")]
    public void A_manifest_path_that_leaves_the_build_is_refused(string path)
    {
        Stage();
        var manifest = JsonNode.Parse(File.ReadAllText(Staged("build.json")))!.AsObject();
        manifest["files"]!.AsArray().Add(new JsonObject { ["path"] = path, ["size"] = 1, ["sha256"] = new string('0', 64) });
        File.WriteAllText(Staged("build.json"), manifest.ToJsonString());

        Assert.Equal("path", StagedBuild.Check(_install)?.Code);
    }

    [Theory]
    [InlineData("""{ "schema": 2, "id": "b1", "version": "0.0.1", "files": [] }""", "schema")]
    [InlineData("""{ "id": "b1", "version": "0.0.1", "files": [] }""", "schema")]
    [InlineData("""{ "schema": 1, "version": "0.0.1", "files": [] }""", "manifest")]
    [InlineData("""{ "schema": 1, "id": "b1", "version": "0.0.1", "files": [ { "path": "Daoris.exe" } ] }""", "manifest")]
    [InlineData("{ torn", "manifest")]
    public void A_manifest_that_does_not_read_at_this_schema_is_refused_by_its_own_code(string text, string code)
    {
        Stage();
        File.WriteAllText(Staged("build.json"), text);

        Assert.True(StagedBuild.IsStaged(_install));
        Assert.Equal(code, StagedBuild.Check(_install)?.Code);
    }

    // ——— The swap (§5).

    [Fact]
    public void The_swap_moves_the_staged_build_in_starts_it_and_once_it_confirms_removes_the_build_before_it()
    {
        Stage();
        IReadOnlyList<string>? handed = null;
        var swap = new InstallSwap(_install, start: arguments =>
        {
            handed = arguments;
            Assert.Equal("new application", Read("app/Daoris.Desktop.exe"));
            Assert.True(StagedBuild.Confirm(_install, 4242));
            return 4242;
        }, alive: _ => true);

        var outcome = swap.Run(["--from", "test"]);

        Assert.Equal(SwapPhase.Installed, outcome.Phase);
        Assert.False(outcome.StartOld);
        Assert.Equal(["--from", "test"], handed);
        Assert.Equal("new launcher", Read("Daoris.exe"));
        Assert.Equal("new library", Read("app/Daoris.Desktop.App.dll"));
        Assert.StartsWith("# Daoris — installed desktop\nnew", Read("INSTALLED.md"));
        Assert.Equal("{ \"drivable\": [] }\n", Read("data/driver.json"));
        Assert.False(Directory.Exists(Path.Combine(_install, "update", "staged")));
        Assert.False(Directory.Exists(Path.Combine(_install, "update", "previous")));

        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.Installed, journal.Phase);
        Assert.Equal("b1", journal.Id);
        Assert.True(journal.Confirmed);
        // The application that confirmed said it was updated; the launcher keeps the mark as it finishes.
        Assert.True(journal.Told);
    }

    [Fact]
    public void A_new_application_that_ends_before_it_confirms_is_moved_aside_and_the_build_before_it_put_back()
    {
        Stage();
        var swap = new InstallSwap(_install, start: _ => 4242, alive: _ => false);

        var outcome = swap.Run([]);

        Assert.Equal(SwapPhase.RolledBack, outcome.Phase);
        Assert.Equal("exited", outcome.Reason);
        Assert.True(outcome.StartOld);
        AssertTheBuildBefore();
        Assert.Equal("new application", Read("update/failed/app/Daoris.Desktop.exe"));
        Assert.Equal("new launcher", Read("update/failed/Daoris.exe"));
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.False(Directory.Exists(Path.Combine(_install, "update", "previous")));

        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.RolledBack, journal.Phase);
        Assert.Equal("exited", journal.Reason);
        Assert.False(journal.Told);
        Assert.Empty(journal.Moves);
    }

    [Fact]
    public void A_new_application_that_will_not_start_is_rolled_back_by_start()
    {
        Stage();

        var outcome = new InstallSwap(_install, start: _ => null, alive: _ => false).Run([]);

        Assert.Equal(SwapPhase.RolledBack, outcome.Phase);
        Assert.Equal("start", outcome.Reason);
        AssertTheBuildBefore();
    }

    [Fact]
    public void A_new_application_still_running_and_silent_when_the_wait_ends_is_installed_and_said_unconfirmed()
    {
        Stage();
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var swap = new InstallSwap(_install, start: _ => 4242, alive: _ => true, clock: () => now, sleep: span => now += span)
        {
            ConfirmWithin = TimeSpan.FromSeconds(5),
        };

        var outcome = swap.Run([]);

        Assert.Equal(SwapPhase.Installed, outcome.Phase);
        Assert.Equal("unconfirmed", outcome.Reason);
        Assert.Equal("new application", Read("app/Daoris.Desktop.exe"));
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.False(journal.Confirmed);
        Assert.False(journal.Told);
    }

    [Fact]
    public void A_build_the_launcher_s_check_refuses_moves_nothing_of_the_install_and_is_moved_aside()
    {
        Stage();
        File.WriteAllText(Staged("app/Daoris.Desktop.exe"), "NEW application");
        var started = false;

        var outcome = new InstallSwap(_install, start: _ => { started = true; return 1; }, alive: _ => true).Run([]);

        Assert.Equal(SwapPhase.Refused, outcome.Phase);
        Assert.Equal("hash", outcome.Reason);
        Assert.True(outcome.StartOld);
        Assert.False(started);
        AssertTheBuildBefore();
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.True(File.Exists(Path.Combine(_install, "update", "failed", "build.json")));
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.Refused, journal.Phase);
        Assert.Equal("hash", journal.Reason);
    }

    [Fact]
    public void Nothing_staged_swaps_nothing_and_the_launcher_starts_the_application_as_it_is()
    {
        var outcome = new InstallSwap(_install, start: _ => throw new InvalidOperationException("not started"), alive: _ => true).Run([]);

        Assert.Same(SwapOutcome.Nothing, outcome);
        Assert.Null(StagedBuild.ReadJournal(_install));
    }

    /// <summary>
    /// A file in <c>app/</c> held for longer than the wait: the swap rolls back <c>busy</c>, which is true, since nothing
    /// was replaced, and the journal names what was held and for how long (SWAP2). The clock is the test's, so the two
    /// minutes pass in a moment against a real hold.
    /// </summary>
    [Fact]
    public void A_file_held_in_app_past_the_wait_rolls_the_swap_back_busy_with_nothing_changed_and_the_hold_journalled()
    {
        // Only Windows refuses to move a folder holding an open file; elsewhere the rename succeeds, so there is nothing
        // to hold here.
        if (!OperatingSystem.IsWindows()) return;
        Stage();
        var now = Start;

        SwapOutcome outcome;
        InstallSwap swap;
        using (new FileStream(Path.Combine(_install, "app", "Daoris.Desktop.App.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            swap = new InstallSwap(_install, start: _ => 1, alive: _ => true, clock: () => now, sleep: span => now += span);
            outcome = swap.Run([]);
        }

        Assert.Equal(SwapPhase.RolledBack, outcome.Phase);
        Assert.Equal("busy", outcome.Reason);
        AssertTheBuildBefore();
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal("busy", journal.Reason);
        var hold = Assert.Single(journal.Holds!);
        Assert.Equal(("app", "update/previous/app", false), (hold.From, hold.To, hold.Released));
        Assert.True(hold.Ms >= swap.HeldWithin.TotalMilliseconds, $"held {hold.Ms} ms");
    }

    /// <summary>
    /// The scanner opens a new build's files and lets go within seconds; a move it refuses for a moment is tried again,
    /// so an update is not rolled back for a hold that gives way (FIX-LOG 2026-10-07, the deployment rehearsal's update).
    /// </summary>
    [Fact]
    public void A_file_held_in_app_for_a_moment_does_not_roll_the_swap_back()
    {
        if (!OperatingSystem.IsWindows()) return;
        Stage();

        var held = new FileStream(Path.Combine(_install, "app", "Daoris.Desktop.App.dll"), FileMode.Open, FileAccess.Read, FileShare.None);
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(300);
            held.Dispose();
        });
        var outcome = new InstallSwap(_install, start: _ =>
        {
            Assert.True(StagedBuild.Confirm(_install, 4242));
            return 4242;
        }, alive: _ => true)
        {
            HeldWithin = TimeSpan.FromSeconds(10), HeldWait = TimeSpan.FromMilliseconds(50),
        }.Run([]);
        letGo.Wait();

        Assert.Equal(SwapPhase.Installed, outcome.Phase);
        Assert.Equal("new library", Read("app/Daoris.Desktop.App.dll"));
        Assert.True(Assert.Single(StagedBuild.ReadJournal(_install)!.Holds!).Released);
    }

    // ——— Holds outlasted, measured, and said as what they were (SWAP2).

    /// <summary>
    /// The deployment rehearsal twice met a hold longer than the ten seconds the swap once waited (50 tries, 200 ms apart),
    /// each with a build running beside it. A real file in <c>app/</c>, held for twenty-five seconds by the test's clock and
    /// let go when they have passed, no longer rolls the update back with the wait as it stands.
    /// </summary>
    [Fact]
    public void A_hold_longer_than_the_old_ten_seconds_still_swaps_and_the_journal_says_how_long_it_lasted()
    {
        if (!OperatingSystem.IsWindows()) return;
        Stage();
        var now = Start;
        var held = new FileStream(Path.Combine(_install, "app", "Daoris.Desktop.App.dll"), FileMode.Open, FileAccess.Read, FileShare.None);
        SwapOutcome outcome;
        try
        {
            outcome = new InstallSwap(_install, start: _ =>
            {
                Assert.True(StagedBuild.Confirm(_install, 4242));
                return 4242;
            }, alive: _ => true, clock: () => now, sleep: span =>
            {
                now += span;
                if (now - Start >= TimeSpan.FromSeconds(25)) held.Dispose();
            }).Run([]);
        }
        finally
        {
            held.Dispose();
        }

        Assert.Equal(SwapPhase.Installed, outcome.Phase);
        Assert.Equal("new library", Read("app/Daoris.Desktop.App.dll"));
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.Installed, journal.Phase);
        Assert.Null(journal.Reason);
        var hold = Assert.Single(journal.Holds!);
        Assert.Equal(("app", "update/previous/app", true), (hold.From, hold.To, hold.Released));
        Assert.InRange(hold.Ms, 25_000, 26_000);
    }

    /// <summary>The same on any platform, the hold handed in: the staged folder refused for forty seconds as it comes in.</summary>
    [Fact]
    public void A_staged_folder_held_past_the_old_bound_as_it_comes_in_still_swaps()
    {
        Stage();
        var now = Start;
        var swap = new InstallSwap(_install, start: _ =>
        {
            Assert.True(StagedBuild.Confirm(_install, 4242));
            return 4242;
        }, alive: _ => true, clock: () => now, sleep: span => now += span)
        {
            Mover = (from, to, overwrite) =>
            {
                if (Is(from, "update/staged/app") && now - Start < TimeSpan.FromSeconds(40)) throw Refused();
                InstallSwap.MoveOnDisk(from, to, overwrite);
            },
        };

        var outcome = swap.Run([]);

        Assert.Equal(SwapPhase.Installed, outcome.Phase);
        Assert.Equal("new application", Read("app/Daoris.Desktop.exe"));
        var hold = Assert.Single(StagedBuild.ReadJournal(_install)!.Holds!);
        Assert.Equal(("update/staged/app", "app", true), (hold.From, hold.To, hold.Released));
        Assert.InRange(hold.Ms, 40_000, 41_000);
        Assert.True(hold.Tries > 50, $"{hold.Tries} tries");
    }

    /// <summary>
    /// <c>busy</c> is a hold that outlasted the wait and nothing else: a move that fails for another reason fails at once,
    /// waits for nothing, and is said as <c>move</c>, the build before it put back.
    /// </summary>
    [Fact]
    public void A_move_that_fails_for_another_reason_rolls_back_at_once_as_move_never_busy()
    {
        Stage();
        var slept = 0;
        var swap = new InstallSwap(_install, start: _ => throw new InvalidOperationException("never started"), alive: _ => true,
            sleep: _ => slept++)
        {
            Mover = (from, to, overwrite) =>
            {
                // ERROR_ALREADY_EXISTS: not a hold, and no wait makes it one.
                if (Is(from, "update/staged/Daoris.exe")) throw new IOException("Cannot create a file when that file already exists.", unchecked((int)0x800700B7));
                InstallSwap.MoveOnDisk(from, to, overwrite);
            },
        };

        var outcome = swap.Run([]);

        Assert.Equal(SwapPhase.RolledBack, outcome.Phase);
        Assert.Equal("move", outcome.Reason);
        Assert.Equal(0, slept);
        AssertTheBuildBefore();
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal("move", journal.Reason);
        Assert.Contains("already exists", journal.Detail);
        Assert.Null(journal.Holds);
    }

    /// <summary>
    /// A new build that exits is rolled back as <c>exited</c> even when putting it back meets a hold that outlasts the wait:
    /// the journal stays under way with the moves still to undo and the reason it undoes for, a start then confirms
    /// nothing, and the next start finishes the undo with that reason, never <c>busy</c> nor <c>interrupted</c>.
    /// </summary>
    [Fact]
    public void A_roll_back_held_past_the_wait_keeps_its_reason_and_the_next_start_finishes_it()
    {
        Stage();
        var now = Start;
        var letGo = false;
        void Mover(string from, string to, bool overwrite)
        {
            if (!letGo && Is(from, "app") && Is(to, "update/failed/app")) throw Refused();
            InstallSwap.MoveOnDisk(from, to, overwrite);
        }

        var first = new InstallSwap(_install, start: _ => 4242, alive: _ => false, clock: () => now, sleep: span => now += span)
        {
            Mover = Mover,
        };
        Assert.ThrowsAny<IOException>(() => first.Run([]));

        var under = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.Started, under.Phase);
        Assert.Equal("exited", under.Reason);
        Assert.Equal(new SwapMove("update/staged/app", "app"), under.Moves[^1]);
        Assert.Equal(4, under.Moves.Count);
        Assert.False(StagedBuild.Confirm(_install, 4242));
        Assert.Equal(SwapPhase.Started, StagedBuild.ReadJournal(_install)!.Phase);

        letGo = true;
        var put = new InstallSwap(_install, start: _ => 1, alive: _ => false, clock: () => now, sleep: span => now += span)
        {
            Mover = Mover,
        }.Recover(appRunning: false);

        Assert.Equal(SwapPhase.RolledBack, put);
        AssertTheBuildBefore();
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal("exited", journal.Reason);
        Assert.Contains("ended before it came up", journal.Detail);
        Assert.Contains(journal.Holds!, hold => (hold.From, hold.To, hold.Released) == ("app", "update/failed/app", false));
        Assert.Equal("new application", Read("update/failed/app/Daoris.Desktop.exe"));
        Assert.Equal("new launcher", Read("update/failed/Daoris.exe"));
    }

    /// <summary>
    /// The launcher's catch (D139 §6): an error part way through a swap is put right from its journal, as a launcher that
    /// died is; only with no swap under way does an update asked for refuse the staged build, and then as <c>error</c>.
    /// </summary>
    [Fact]
    public void An_error_puts_a_swap_under_way_right_from_its_journal_and_refuses_as_error_only_when_none_is()
    {
        Stage();
        Directory.CreateDirectory(Path.Combine(_install, "update", "previous"));
        Directory.Move(Path.Combine(_install, "app"), Path.Combine(_install, "update", "previous", "app"));
        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.Swapping, "b1", "0.0.1", null, Start, [new SwapMove("app", "update/previous/app")]));

        var put = new InstallSwap(_install, start: _ => null, alive: _ => false).AfterError(appRunning: false, updating: true);

        Assert.Equal(SwapPhase.RolledBack, put.Phase);
        Assert.Equal("interrupted", put.Reason);
        Assert.True(put.StartOld);
        AssertTheBuildBefore();

        Stage();
        var refused = new InstallSwap(_install, start: _ => null, alive: _ => false).AfterError(appRunning: false, updating: true);

        Assert.Equal(SwapPhase.Refused, refused.Phase);
        Assert.Equal("error", refused.Reason);
        Assert.Equal("error", StagedBuild.ReadJournal(_install)!.Reason);
        Assert.False(StagedBuild.IsStaged(_install));
        AssertTheBuildBefore();
    }

    /// <summary>
    /// A refusal never writes over a swap under way: its journal is what puts the install right, and a <c>refused</c>
    /// over it would lose the moves it made and say <c>busy</c> of a swap that went on.
    /// </summary>
    [Fact]
    public void A_refusal_never_writes_over_a_swap_under_way()
    {
        Stage();
        var started = new SwapRecord(
            SwapPhase.Started, "b1", "0.0.1", null, Start, [new SwapMove("app", "update/previous/app")], Pid: 4242);
        StagedBuild.WriteJournal(_install, started);
        var swap = new InstallSwap(_install, start: _ => null, alive: _ => true);

        swap.Held("something still ran from app/ after the application closed, so nothing was replaced.");
        var after = swap.AfterError(appRunning: true, updating: true);

        Assert.False(after.StartOld);
        var journal = StagedBuild.ReadJournal(_install)!;
        Assert.Equal(SwapPhase.Started, journal.Phase);
        Assert.Null(journal.Reason);
        Assert.Equal(started.Moves, journal.Moves);
        Assert.True(StagedBuild.IsStaged(_install));
    }

    /// <summary>
    /// A journal a reader holds open for a moment without delete sharing is still written (AtomicFile's REV3 measurement):
    /// the launcher reads it every poll while the new application writes its confirmation.
    /// </summary>
    [Fact]
    public void A_journal_a_reader_holds_for_a_moment_is_still_written()
    {
        if (!OperatingSystem.IsWindows()) return;
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Started, "b1", "0.0.1", null, Start, []));

        var reader = new FileStream(StagedBuild.JournalOf(_install), FileMode.Open, FileAccess.Read, FileShare.Read);
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(150);
            reader.Dispose();
        });
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Confirmed, "b1", "0.0.1", null, Start, [], Pid: 4242));
        letGo.Wait();

        Assert.Equal(SwapPhase.Confirmed, StagedBuild.ReadJournal(_install)!.Phase);
    }

    /// <summary>A journal written before SWAP2 has no holds, and reads as it did; one with holds reads them back.</summary>
    [Fact]
    public void A_journal_s_holds_read_back_and_one_without_reads_as_before()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Installed, "b1", "0.0.1", null, Start, []));
        Assert.DoesNotContain("holds", File.ReadAllText(StagedBuild.JournalOf(_install)));
        Assert.Null(StagedBuild.ReadJournal(_install)!.Holds);

        SwapHold[] holds = [new("app", "update/previous/app", 12_345, 62, true), new("update/staged/app", "app", 120_000, 601, false)];
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.RolledBack, "b1", "0.0.1", null, Start, [], Reason: "busy", Holds: holds));

        Assert.Equal(holds, StagedBuild.ReadJournal(_install)!.Holds);
    }

    /// <summary>
    /// An update asked for while something still runs from <c>app/</c> after the wait is refused and the build moved aside:
    /// started again on the build before it, the application would drain and close for the same build, round and round.
    /// </summary>
    [Fact]
    public void An_update_that_finds_app_still_held_refuses_the_build_so_the_application_is_not_closed_for_it_again()
    {
        Stage();

        var outcome = new InstallSwap(_install, start: _ => 1, alive: _ => true).Held("a process still runs from app/.");

        Assert.Equal(SwapPhase.Refused, outcome.Phase);
        Assert.Equal("busy", outcome.Reason);
        Assert.True(outcome.StartOld);
        AssertTheBuildBefore();
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.Equal("busy", StagedBuild.ReadJournal(_install)!.Reason);
        Assert.Same(SwapOutcome.Nothing, new InstallSwap(_install, start: _ => 1, alive: _ => true).Held("nothing staged"));
    }

    // ——— What a launcher that died left (§6).

    [Fact]
    public void A_launcher_that_died_mid_swap_is_undone_by_the_next_start_from_its_journal()
    {
        Stage();
        Directory.CreateDirectory(Path.Combine(_install, "update", "previous"));
        Directory.Move(Path.Combine(_install, "app"), Path.Combine(_install, "update", "previous", "app"));
        File.Move(Path.Combine(_install, "Daoris.exe"), Path.Combine(_install, "update", "previous", "Daoris.exe"));
        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.Swapping, "b1", "0.0.1", null, DateTimeOffset.UtcNow,
            [new SwapMove("app", "update/previous/app"), new SwapMove("Daoris.exe", "update/previous/Daoris.exe")]));

        var put = new InstallSwap(_install, start: _ => 1, alive: _ => true).Recover(appRunning: false);

        Assert.Equal(SwapPhase.RolledBack, put);
        AssertTheBuildBefore();
        Assert.Equal("interrupted", StagedBuild.ReadJournal(_install)!.Reason);
    }

    [Fact]
    public void A_swap_started_and_never_confirmed_is_undone_once_nothing_of_it_runs_and_left_while_it_does()
    {
        Stage();
        new InstallSwap(_install, start: _ => 4242, alive: _ => true, clock: () => DateTimeOffset.UtcNow, sleep: _ => throw new Stop())
        { ConfirmWithin = TimeSpan.FromHours(1) }.RunUntilStopped();
        Assert.Equal(SwapPhase.Started, StagedBuild.ReadJournal(_install)!.Phase);

        var swap = new InstallSwap(_install, start: _ => 1, alive: _ => true);
        Assert.Null(swap.Recover(appRunning: true));
        Assert.Equal("new application", Read("app/Daoris.Desktop.exe"));

        Assert.Equal(SwapPhase.RolledBack, swap.Recover(appRunning: false));
        AssertTheBuildBefore();
        Assert.Equal("exited", StagedBuild.ReadJournal(_install)!.Reason);
    }

    [Fact]
    public void A_swap_confirmed_and_never_finished_is_installed_by_the_next_start()
    {
        Stage();
        new InstallSwap(_install, start: _ => 4242, alive: _ => true, clock: () => DateTimeOffset.UtcNow, sleep: _ => throw new Stop())
        { ConfirmWithin = TimeSpan.FromHours(1) }.RunUntilStopped();
        Assert.True(StagedBuild.Confirm(_install, 4242));

        Assert.Equal(SwapPhase.Installed, new InstallSwap(_install, start: _ => 1, alive: _ => true).Recover(appRunning: true));

        Assert.Equal("new application", Read("app/Daoris.Desktop.exe"));
        Assert.False(Directory.Exists(Path.Combine(_install, "update", "previous")));
        Assert.True(StagedBuild.ReadJournal(_install)!.Told);
    }

    [Fact]
    public void Only_a_started_swap_is_confirmed_and_an_outcome_is_told_once()
    {
        Assert.False(StagedBuild.Confirm(_install, 1));

        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.RolledBack, "b1", "0.0.1", null, DateTimeOffset.UtcNow, [], Reason: "exited"));
        Assert.False(StagedBuild.Confirm(_install, 1));
        Assert.False(StagedBuild.ReadJournal(_install)!.Told);

        StagedBuild.Tell(_install);
        Assert.True(StagedBuild.ReadJournal(_install)!.Told);
        Assert.Equal(SwapPhase.RolledBack, StagedBuild.ReadJournal(_install)!.Phase);
    }

    [Fact]
    public void Unstaging_removes_the_staged_build_and_keeps_the_journal()
    {
        Stage();
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Installed, "b0", "0.0.1", null, DateTimeOffset.UtcNow, []));

        StagedBuild.Unstage(_install);

        Assert.False(StagedBuild.IsStaged(_install));
        Assert.Equal("b0", StagedBuild.ReadJournal(_install)!.Id);
    }

    private void AssertTheBuildBefore()
    {
        Assert.Equal("old launcher", Read("Daoris.exe"));
        Assert.Equal("old application", Read("app/Daoris.Desktop.exe"));
        Assert.Equal("old library", Read("app/Daoris.Desktop.App.dll"));
        Assert.Equal("# Daoris — installed desktop\nold\n", Read("INSTALLED.md"));
        Assert.Equal("{ \"drivable\": [] }\n", Read("data/driver.json"));
    }

    /// <summary>A build staged as the publish stages one: the four files every build carries, and its manifest.</summary>
    private void Stage()
    {
        Write("update/staged/INSTALLED.md", "# Daoris — installed desktop\nnew\n");
        Write("update/staged/Daoris.exe", "new launcher");
        Write("update/staged/app/Daoris.Desktop.exe", "new application");
        Write("update/staged/app/Daoris.Desktop.App.dll", "new library");
        Manifest();
    }

    /// <summary>The manifest of whatever the staged folder holds now, as the publish writes it.</summary>
    private void Manifest()
    {
        var staged = Path.Combine(_install, "update", "staged");
        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/');
            if (relative == "build.json") continue;
            files.Add(new JsonObject
            {
                ["path"] = relative,
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1,
            ["id"] = "b1",
            ["version"] = "0.0.1",
            ["commit"] = "abc1234",
            ["at"] = "2026-10-03T12:00:00Z",
            ["files"] = files,
        }.ToJsonString());
    }

    private string Staged(string relative) => Path.Combine(_install, "update", "staged", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Whether a full path a move is handed is this install-relative one.</summary>
    private bool Is(string full, string relative) =>
        string.Equals(full, Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar)), StringComparison.OrdinalIgnoreCase);

    /// <summary>What Windows answers a move of a folder a scanner holds a file in: access denied.</summary>
    private static UnauthorizedAccessException Refused() => new("Access to the path is denied.");

    private string Read(string relative) => File.ReadAllText(Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar)));

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

/// <summary>Thrown by a test's sleep to stop a swap where a launcher would have died: waiting for its application.</summary>
internal sealed class Stop : Exception;

internal static class InstallSwapTestExtensions
{
    /// <summary>Run a swap until its first wait, where the test's sleep stops it as a launcher dying there would stop.</summary>
    public static void RunUntilStopped(this InstallSwap swap)
    {
        try { swap.Run([]); }
        catch (Stop) { }
    }
}
