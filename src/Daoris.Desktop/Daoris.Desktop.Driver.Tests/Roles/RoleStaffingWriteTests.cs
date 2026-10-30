using System.Globalization;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s parts 4 and 5 (the design §19.15, "A write", "The check", "What was found, offered"): every staffing write through
/// <see cref="RoleStaffing.Set"/>, the file before the store under both locks, then one check; the check at a start, a look and a write's end,
/// a turn-off found applied and every other change kept whole in a found row for the person's <i>Take it</i> or <i>Dismiss</i>. An older
/// build's write is its <c>ActorChoices.With</c> renamed with no look at its lock (<see cref="AtomicFile.WriteText"/>), as its save renames.
/// </summary>
public sealed class RoleStaffingWriteTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-staffing-write", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private const string Start =
        """
        {
          "cap": 3,
          "adapter": "claude-code-acp",
          "intakeAdapter": "codex-acp",
          "helperAdapter": "codex-acp"
        }
        """;

    private RoleStaffing Staffing(
        Action? beforeCommit = null, KeepSeams? seams = null, Action? afterFound = null, Action? afterRepair = null, TimeSpan? lockWait = null) =>
        new(_home, () => StoreSettled.Yes)
        {
            BeforeCommit = beforeCommit, Seams = seams, AfterFound = afterFound, AfterRepair = afterRepair,
            LockWait = lockWait ?? TimeSpan.FromSeconds(5), FileWait = TimeSpan.FromMilliseconds(200),
        };

    /// <summary>The home's file written and imported, the port wired as a root wires it.</summary>
    private RoleStaffing Begin(string json = Start)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, json);
        RoleStaffing.Wire(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, Staffing().ImportFile().How);
        return Staffing();
    }

    private DriverConfig Loaded => DriverConfig.Load(File_);

    private DriverConfig OnFile => DriverConfig.Parse(File.ReadAllText(File_));

    private string? First(string actor, DriverConfig config) => ActorChoices.Effective(actor, config).FirstOrDefault()?.Agent;

    /// <summary>An older build's write: its own change of the file as it reads it, renamed with no look at the lock.</summary>
    private void Older(Func<DriverConfig, DriverConfig> change) => AtomicFile.WriteText(File_, change(OnFile).ToJson());

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>The keeping lock as a live writer's fresh take of it leaves it.</summary>
    private void KeepHeld() =>
        File.WriteAllText(KeepLock.PathOf(File_), $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow)}\"}}\n");

    private int FoundRows()
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader!.Query("SELECT COUNT(*) FROM role_staffing_found", row => row.GetInt32(0))[0];
    }

    private static IReadOnlyList<ActorChoice> One(string agent) => [new ActorChoice(agent)];

    // ——— A write: the staffing lock, With, the file against `written`, the commit, the check.

    [Fact]
    public void A_press_writes_the_file_then_the_store_and_sets_written_chat_and_brainstorm_kept_where_builds_agent_moves()
    {
        var staffing = Begin();

        var kept = staffing.Set("build", One("other-acp"));

        Assert.Equal(["chat", "brainstorm"], kept);
        Assert.Equal("other-acp", Loaded.Adapter);
        Assert.Equal("claude-code-acp", First("chat", Loaded));
        Assert.Equal("other-acp", OnFile.Adapter);
        Assert.Equal("claude-code-acp", OnFile.Actors["brainstorm"][0].Agent);
        Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Equal(0, FoundRows());
    }

    [Fact]
    public void A_write_killed_before_its_rename_changes_nothing()
    {
        Begin();

        Assert.ThrowsAny<Exception>(() => Staffing(seams: new KeepSeams { BeforeLook = () => throw new InvalidOperationException("killed") })
            .Set("intake", []));

        Assert.Equal(Start, File.ReadAllText(File_));
        Assert.Equal("codex-acp", Loaded.IntakeAdapter);
    }

    [Fact]
    public void A_write_killed_after_its_rename_and_before_its_commit_a_turn_off_completed_by_the_next_check()
    {
        Begin();

        Assert.ThrowsAny<Exception>(() => Staffing(beforeCommit: () => throw new InvalidOperationException("killed")).Set("intake", []));
        Assert.Equal("codex-acp", Loaded.IntakeAdapter);

        Assert.Equal(StaffingChecks.Found, Staffing().Check(StaffingCheckAt.Start).How);

        Assert.Empty(Loaded.Actors["intake"]);
        Assert.Null(First("intake", Loaded));
        Assert.Equal(1, FoundRows());
    }

    [Fact]
    public void A_write_killed_after_its_rename_and_before_its_commit_a_turn_on_found_and_offered_never_applied()
    {
        var staffing = Begin();
        staffing.Set("intake", []);

        Assert.ThrowsAny<Exception>(() => Staffing(beforeCommit: () => throw new InvalidOperationException("killed")).Set("intake", One("codex-acp")));
        Assert.Equal("codex-acp", First("intake", OnFile));

        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Start).How);

        Assert.Null(First("intake", Loaded));
        Assert.Null(First("intake", OnFile));
        Assert.Single(staffing.Found());
    }

    [Fact]
    public async Task Two_writers_the_second_waits_while_the_first_holds_the_staffing_lock_and_is_kept_after_it()
    {
        var staffing = Begin();
        var waited = new TaskCompletionSource();
        void Heard(string path) => waited.TrySetResult();
        RecordLock.Waiting += Heard;
        try
        {
            Task second;
            using (RecordLock.Take(RecordLock.PathOf(_home, RoleStaffing.Record)))
            {
                second = Task.Run(() => staffing.Set("intake", []));
                await waited.Task.WaitAsync(Poll.Patience);
                Assert.False(second.IsCompleted);
            }

            await second.WaitAsync(Poll.Patience);
        }
        finally
        {
            RecordLock.Waiting -= Heard;
        }

        Assert.Empty(Loaded.Actors["intake"]);
        Assert.Empty(OnFile.Actors["intake"]);
    }

    [Fact]
    public void A_writer_paused_holding_the_staffing_lock_past_the_wait_refuses_the_other_with_nothing_written_and_said()
    {
        Begin();
        using var held = RecordLock.Take(RecordLock.PathOf(_home, RoleStaffing.Record));

        var refused = Assert.Throws<DriverException>(() => Staffing(lockWait: TimeSpan.FromMilliseconds(100)).Set("intake", []));

        Assert.Contains("nothing was written", refused.Message);
        Assert.Equal(Start, File.ReadAllText(File_));
    }

    [Fact]
    public void A_write_whose_keeping_lock_wait_expires_and_one_whose_commit_is_refused_each_followed_by_another_write_which_proceeds()
    {
        Begin();
        KeepHeld();
        Assert.Throws<DriverException>(() => Staffing().Set("intake", []));
        Assert.Equal(Start, File.ReadAllText(File_));
        File.Delete(KeepLock.PathOf(File_));

        Assert.Throws<DriverException>(() => Staffing(beforeCommit: () => throw new SqliteException("the store refused the commit", 5)).Set("chat", One("codex-acp")));

        Staffing().Set("intake", []);
        Assert.Empty(Loaded.Actors["intake"]);
        Assert.Empty(OnFile.Actors["intake"]);
    }

    // ——— The check: at `written` nothing; a change found kept whole, a turn-off in it applied, the file repaired.

    [Fact]
    public void The_checks_repair_cut_after_its_found_rows_commit_records_no_second_row_at_the_next_check_and_repairs()
    {
        Begin();
        Older(config => ActorChoices.With(ActorChoices.With(config, "intake", []), "build", One("other-acp")));

        Assert.ThrowsAny<Exception>(() => Staffing(afterFound: () => throw new InvalidOperationException("killed")).Check(StaffingCheckAt.Start));
        Assert.Equal(1, FoundRows());
        Assert.Equal("other-acp", OnFile.Adapter);
        Assert.Null(First("intake", Loaded));

        Assert.Equal(StaffingChecks.Found, Staffing().Check(StaffingCheckAt.Start).How);

        Assert.Equal(1, FoundRows());
        Assert.Equal(StaffingChecks.Same, Staffing().Check(StaffingCheckAt.Look).How);
        Assert.Empty(OnFile.Actors["intake"]);
        Assert.Equal("claude-code-acp", OnFile.Adapter);
    }

    [Fact]
    public void The_checks_repair_cut_after_the_files_replacement_the_next_check_sets_written_and_finds_nothing()
    {
        Begin();
        Older(config => ActorChoices.With(ActorChoices.With(config, "intake", []), "build", One("other-acp")));

        Assert.ThrowsAny<Exception>(() => Staffing(afterRepair: () => throw new InvalidOperationException("killed")).Check(StaffingCheckAt.Start));
        Assert.Equal("claude-code-acp", OnFile.Adapter);

        Assert.Equal(StaffingChecks.Written, Staffing().Check(StaffingCheckAt.Look).How);
        Assert.Equal(1, FoundRows());
        Assert.Equal(StaffingChecks.Same, Staffing().Check(StaffingCheckAt.Look).How);
    }

    [Fact]
    public void A_repair_whose_observed_baseline_an_older_late_rename_changed_is_refused_with_nothing_written_and_no_second_check()
    {
        Begin();
        File.WriteAllText(File_, Start.Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"other-acp\""));
        var late = Start.Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"late-acp\"");

        var looked = Staffing(afterFound: () => AtomicFile.WriteText(File_, late)).Check(StaffingCheckAt.Start);

        Assert.Equal(StaffingChecks.Found, looked.How);
        Assert.Equal(late, File.ReadAllText(File_));
        Assert.Equal(1, FoundRows());
        Assert.Equal(StaffingChecks.Found, Staffing().Check(StaffingCheckAt.Look).How);
        Assert.Equal(2, FoundRows());
    }

    [Fact]
    public void A_looks_check_finding_either_lock_held_completes_its_tick_and_the_next_look_compares()
    {
        Begin();
        Older(config => ActorChoices.With(config, "intake", []));
        var started = System.Diagnostics.Stopwatch.StartNew();

        using (RecordLock.Take(RecordLock.PathOf(_home, RoleStaffing.Record)))
        {
            Assert.Equal(StaffingChecks.Left, Staffing().Check(StaffingCheckAt.Look).How);
        }

        KeepHeld();
        Assert.Equal(StaffingChecks.Left, Staffing().Check(StaffingCheckAt.Look).How);
        // Neither waited out a lock's wait: a hung writer holds a look up for no wait at all.
        Assert.True(started.Elapsed < RecordLock.Wait + KeepLock.Wait);
        File.Delete(KeepLock.PathOf(File_));

        Assert.Equal(StaffingChecks.Found, Staffing().Check(StaffingCheckAt.Look).How);
        Assert.Null(First("intake", Loaded));
    }

    // ——— What was found, taken or dismissed: each changed path as the file shows it, no With and no Keep.

    [Fact]
    public void Two_older_set_actors_found_at_one_check_intake_off_at_once_both_in_one_row_builds_change_taken_with_its_pins()
    {
        var staffing = Begin();
        Older(config => ActorChoices.With(config, "intake", []));
        Older(config => ActorChoices.With(config, "build", One("other-acp")));

        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Null(First("intake", Loaded));
        Assert.Equal("claude-code-acp", Loaded.Adapter);
        var found = Assert.Single(staffing.Found());
        Assert.Contains(found.Changes, change => change.Role == "intake" && change.Applied);
        Assert.Contains(found.Changes, change => change.Role == "build" && !change.Applied);

        staffing.Take(found.Id);

        Assert.Equal("other-acp", First("build", Loaded));
        Assert.Equal("claude-code-acp", Loaded.Actors["chat"][0].Agent);
        Assert.Equal("claude-code-acp", Loaded.Actors["brainstorm"][0].Agent);
        Assert.Empty(staffing.Found());
        Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
    }

    [Fact]
    public void Two_older_set_actors_found_and_dismissed_leave_build_and_chat_and_brainstorm_as_before_on_the_stores_adapter()
    {
        var staffing = Begin();
        Older(config => ActorChoices.With(config, "intake", []));
        Older(config => ActorChoices.With(config, "build", One("other-acp")));
        staffing.Check(StaffingCheckAt.Look);

        staffing.Dismiss(Assert.Single(staffing.Found()).Id);

        Assert.Null(First("intake", Loaded));
        foreach (var actor in (string[])["build", "chat", "brainstorm"])
        {
            Assert.False(Loaded.Actors.ContainsKey(actor));
            Assert.Equal("claude-code-acp", First(actor, Loaded));
        }

        Assert.Empty(staffing.Found());
        Assert.Equal("claude-code-acp", OnFile.Adapter);
    }

    [Theory]
    [InlineData("build", "adapter")]
    [InlineData("intake", "intakeAdapter")]
    [InlineData("ask-daoris", "helperAdapter")]
    public void An_explicit_entry_an_older_build_moved_and_then_cleared_taken_leaves_the_role_as_before_on_the_older_builds_key(string actor, string key)
    {
        var staffing = Begin();
        staffing.Set(actor, One("a-acp"));
        Assert.Contains($"\"{key}\": \"a-acp\"", File.ReadAllText(File_));
        Older(config => ActorChoices.With(ActorChoices.With(config, actor, One("b-acp")), actor, null));

        staffing.Check(StaffingCheckAt.Look);
        staffing.Take(Assert.Single(staffing.Found()).Id);

        Assert.False(Loaded.Actors.ContainsKey(actor));
        Assert.Equal("b-acp", First(actor, Loaded));
        Assert.Equal("b-acp", ActorChoices.Effective(actor, Loaded)[0].Agent);
        Assert.Equal("before", ActorChoices.Effective(actor, Loaded)[0].From);
    }

    [Fact]
    public void Adapter_hand_edited_with_build_chat_and_brainstorm_as_before_taken_moves_all_three_none_made_explicit()
    {
        var staffing = Begin();
        File.WriteAllText(File_, Start.Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"other-acp\""));
        staffing.Check(StaffingCheckAt.Look);

        staffing.Take(Assert.Single(staffing.Found()).Id);

        Assert.Empty(Loaded.Actors);
        foreach (var actor in (string[])["build", "chat", "brainstorm"]) Assert.Equal("other-acp", First(actor, Loaded));
    }

    [Fact]
    public void Adapter_hand_edited_with_an_explicit_build_order_of_two_taken_keeps_that_order_and_moves_chat_and_brainstorm()
    {
        var staffing = Begin();
        staffing.Set("build", [new ActorChoice("claude-code-acp", "opus"), new ActorChoice("codex-acp")]);
        foreach (var actor in (string[])["chat", "brainstorm"]) staffing.Set(actor, null);
        File.WriteAllText(File_, File.ReadAllText(File_).Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"other-acp\""));
        staffing.Check(StaffingCheckAt.Look);

        staffing.Take(Assert.Single(staffing.Found()).Id);

        Assert.Equal(["claude-code-acp", "codex-acp"], ActorChoices.Effective("build", Loaded).Select(each => each.Agent));
        Assert.Equal("other-acp", First("chat", Loaded));
        Assert.Equal("other-acp", First("brainstorm", Loaded));
    }

    [Fact]
    public void A_hand_edit_of_a_key_is_found_and_offered_and_every_reader_reads_the_stores_agent_until_it_is_taken()
    {
        var staffing = Begin();
        staffing.Set("build", One("claude-code-acp"));
        File.WriteAllText(File_, File.ReadAllText(File_).Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"other-acp\"")
            .Replace("\"intakeAdapter\": \"codex-acp\"", "\"intakeAdapter\": \"other-acp\""));

        Assert.Equal("claude-code-acp", Loaded.Adapter);
        Assert.Equal("codex-acp", First("intake", Loaded));
        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Equal("claude-code-acp", ActorView.Of("build", Loaded).Choices[0].Agent);
        Assert.Equal("codex-acp", First("intake", Loaded));
        Assert.Single(staffing.Found());
    }

    // ——— The two windows against an older writer, decided and named, never proven closed.

    [Fact]
    public void An_older_save_with_intake_on_renamed_late_after_a_newer_turn_off_is_found_offered_and_the_file_narrowed_again()
    {
        var staffing = Begin();
        var older = OnFile.ToJson();
        staffing.Set("intake", []);

        AtomicFile.WriteText(File_, older);
        // The bound: from that rename to the next check the file is wider than the store, which an older build's start reads.
        Assert.Equal("codex-acp", First("intake", OnFile));

        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Null(First("intake", Loaded));
        Assert.Null(First("intake", OnFile));
        Assert.Single(staffing.Found());
    }

    [Fact]
    public void A_newer_write_whose_lock_an_older_save_turning_intake_off_took_before_its_last_look_is_dropped_and_the_turn_off_applied()
    {
        Begin();
        var seams = new KeepSeams
        {
            BeforeLook = () =>
            {
                KeepHeld();
                Older(config => ActorChoices.With(config, "intake", []));
                File.Delete(KeepLock.PathOf(File_));
            },
        };

        Assert.Throws<DriverException>(() => Staffing(seams: seams).Set("build", One("other-acp")));

        Assert.Null(First("intake", Loaded));
        Assert.Equal("claude-code-acp", Loaded.Adapter);
        Assert.Equal("claude-code-acp", OnFile.Adapter);
    }

    [Fact]
    public void A_newer_write_renaming_after_its_last_look_over_an_older_save_completed_in_that_instant_loses_it_the_bound_stated()
    {
        Begin();
        var seams = new KeepSeams { AfterLook = () => Older(config => ActorChoices.With(config, "intake", [])) };

        Staffing(seams: seams).Set("build", One("other-acp"));

        Assert.Equal("codex-acp", First("intake", OnFile));
        Assert.Equal("codex-acp", First("intake", Loaded));
    }

    [Fact]
    public void A_newer_save_of_an_unrelated_setting_whose_lock_an_older_turn_off_took_is_refused_and_the_next_check_applies_the_turn_off()
    {
        var staffing = Begin();
        var seams = new KeepSeams
        {
            BeforeLook = () =>
            {
                KeepHeld();
                Older(config => ActorChoices.With(config, "intake", []));
                File.Delete(KeepLock.PathOf(File_));
            },
        };

        Assert.Throws<KeepLockTakenException>(() => Loaded.WithCap(5).Save(File_, seams));
        Assert.Null(First("intake", OnFile));

        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Null(First("intake", Loaded));
    }

    // ——— What reads the staffing reads the store's.

    [Fact]
    public void A_set_intake_during_an_updates_drain_starts_no_intake_and_nothing_is_written()
    {
        Begin();

        var drained = InstallUpdate.Drained(Loaded);

        Assert.Null(First("intake", drained));
        Assert.Equal("codex-acp", First("intake", Loaded));
        Assert.Equal(Start, File.ReadAllText(File_));
    }

    [Fact]
    public void A_set_helpers_order_is_kept_across_a_change_of_builds_agent()
    {
        var staffing = Begin();
        staffing.Set("helper", [new ActorChoice("codex-acp", "o3"), new ActorChoice("claude-code-acp")]);

        staffing.Set("build", One("other-acp"));

        Assert.Equal(["codex-acp", "claude-code-acp"], ActorChoices.Effective("helper", Loaded).Select(each => each.Agent));
        Assert.Equal("o3", OnFile.Actors["helper"][0].Model);
    }

    [Fact]
    public void An_unreadable_entry_holds_its_starts_in_both_builds_and_a_write_repairs_it_in_both()
    {
        var staffing = Begin(Start.Replace("\"cap\": 3,", "\"cap\": 3, \"actors\": { \"chat\": \"claude-code-acp\" },"));
        Assert.Throws<ActorUnreadableException>(() => ActorChoices.Effective("chat", Loaded));
        staffing.Set("intake", []);
        Assert.Contains("chat", OnFile.ActorErrors.Keys);

        staffing.Set("chat", One("codex-acp"));

        Assert.Equal("codex-acp", First("chat", Loaded));
        Assert.Equal("codex-acp", First("chat", OnFile));
    }

    [Fact]
    public void Build_given_a_second_choice_and_a_model_a_build_reading_actors_walks_both_and_one_from_before_walks_adapter_alone()
    {
        var staffing = Begin();

        staffing.Set("build", [new ActorChoice("codex-acp", "o3"), new ActorChoice("claude-code-acp")]);

        Assert.Equal(["codex-acp", "claude-code-acp"], ActorChoices.Effective("build", OnFile).Select(each => each.Agent));
        Assert.Equal("codex-acp", OnFile.Adapter);
    }
}
