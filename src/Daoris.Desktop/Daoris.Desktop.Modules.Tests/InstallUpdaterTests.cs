using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// UPDATE1 (D139 §2–§4, §6): the application's half of an update. A staged build drains the loop by default, is installed
/// once no driven session runs and no turn is in flight, or at once on *Update now*, and waits on *Not now*; it is checked
/// before the application closes for it; and the next start says once how the swap ended, in the window and the log.
/// </summary>
/// <remarks>
/// A scratch install with a staged build, and the work, the launcher and the close handed in: nothing here starts a process
/// or closes a window.
/// </remarks>
public sealed class InstallUpdaterTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly string _install;

    private UpdateWork _work = new(0, 0);

    private int _relaunched;

    private int _closed;

    private bool _launches = true;

    public InstallUpdaterTests()
    {
        _install = Path.Combine(Home, "install");
        Write("INSTALLED.md", "# Daoris — installed desktop\n");
        Write("Daoris.exe", "old launcher");
        Write("app/Daoris.Desktop.exe", "old application");
        Write("app/Daoris.Desktop.App.dll", "old library");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater(string? install = "") => new(
        install == "" ? _install : install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => _work,
        relaunch: () =>
        {
            _relaunched++;
            return _launches;
        },
        close: () => _closed++,
        bus: Bus, clock: () => Now, pid: 4242);

    [Fact]
    public void A_workspace_build_is_no_install_and_never_drains()
    {
        Stage("b1");
        using var updater = Updater(install: null);

        var state = updater.Look();

        Assert.Equal(UpdateStates.None, state.State);
        Assert.False(updater.Draining);
        Assert.Equal(0, _relaunched);
    }

    [Fact]
    public void Nothing_staged_is_nothing_to_do()
    {
        using var updater = Updater();

        Assert.Equal(UpdateStates.None, updater.Look().State);
        Assert.False(updater.Draining);
        Assert.Null(updater.State.Staged);
        // No journal: no swap to tell of.
        Assert.Null(updater.State.Last);
    }

    [Fact]
    public void A_staged_build_drains_by_default_while_work_runs_and_installs_itself_once_none_does()
    {
        Stage("b1");
        _work = new UpdateWork(Driven: 1, Turns: 1);
        using var updater = Updater();

        var draining = updater.Look();
        Assert.Equal(UpdateStates.Draining, draining.State);
        Assert.True(updater.Draining);
        Assert.Equal(1, draining.Driven);
        Assert.Equal(1, draining.Turns);
        Assert.Equal(UpdateMode.WhenIdle, draining.Mode);
        Assert.Equal(new UpdateBuild("b1", "0.0.1", "abc1234", Now), draining.Staged);
        updater.Look();
        Assert.Equal(0, _relaunched);

        _work = new UpdateWork(0, 0);
        var applying = updater.Look();

        Assert.Equal(UpdateStates.Applying, applying.State);
        Assert.Equal(1, _relaunched);
        Assert.Equal(1, _closed);
        updater.Look();
        Assert.Equal(1, _relaunched);
        Assert.Equal(1, _closed);

        Assert.Single(Logged("update.staged"));
        Assert.Single(Logged("update.draining"));
        var applied = Logged("update.applying").Single();
        Assert.Equal("idle", applied["by"]!.GetValue<string>());
        Assert.Equal("b1", applied["build"]!.GetValue<string>());
    }

    [Fact]
    public void Update_now_installs_at_once_whatever_runs_and_the_request_is_cleared_as_it_closes()
    {
        Stage("b1");
        _work = new UpdateWork(Driven: 2, Turns: 0);
        using var updater = Updater();

        updater.Say(UpdateMode.Now, "screen");

        Assert.Equal(UpdateStates.Applying, updater.State.State);
        Assert.Equal(1, _relaunched);
        Assert.Equal(1, _closed);
        Assert.Null(InstallUpdate.Read(DataHome));
        Assert.Equal("now", Logged("update.applying").Single()["by"]!.GetValue<string>());
        var requested = Logged("update.requested").Single();
        Assert.Equal("now", requested["mode"]!.GetValue<string>());
        Assert.Equal("screen", requested["door"]!.GetValue<string>());
    }

    [Fact]
    public void Not_now_keeps_the_build_staged_and_starts_work_again_until_a_newer_build_is_staged()
    {
        Stage("b1");
        _work = new UpdateWork(Driven: 1, Turns: 0);
        using var updater = Updater();
        updater.Look();
        Assert.True(updater.Draining);

        var waiting = updater.Say(UpdateMode.NotNow, "screen");

        Assert.Equal(UpdateStates.Waiting, waiting.State);
        Assert.False(updater.Draining);
        Assert.Equal(new UpdateRequest(UpdateMode.NotNow, "b1", Now), InstallUpdate.Read(DataHome));
        _work = new UpdateWork(0, 0);
        Assert.Equal(UpdateStates.Waiting, updater.Look().State);
        Assert.Equal(0, _relaunched);

        Stage("b2");
        _work = new UpdateWork(Driven: 1, Turns: 0);
        Assert.Equal(UpdateStates.Draining, updater.Look().State);
        Assert.True(updater.Draining);
    }

    [Fact]
    public void A_build_that_fails_the_check_is_refused_before_anything_closes_and_the_drain_ends()
    {
        Stage("b1");
        File.WriteAllText(Path.Combine(_install, "update", "staged", "app", "Daoris.Desktop.exe"), "NEW application");
        using var updater = Updater();

        var refused = updater.Look();

        Assert.Equal(UpdateStates.Refused, refused.State);
        Assert.Equal("hash", refused.Problem?.Code);
        Assert.False(updater.Draining);
        Assert.Equal(0, _relaunched);
        Assert.Equal(0, _closed);
        Assert.Equal("old application", File.ReadAllText(Path.Combine(_install, "app", "Daoris.Desktop.exe")));
        updater.Look();
        var line = Logged("update.refused", "warn").Single();
        Assert.Equal("hash", line["reason"]!.GetValue<string>());
    }

    /// <summary>
    /// An application still open after it asked for the update — its close did not take, and the launcher, finding it
    /// running, moved the build aside — stops draining once the build is gone, rather than holding every start for ever.
    /// </summary>
    [Fact]
    public void An_application_left_open_after_applying_stops_draining_once_the_launcher_has_moved_the_build_aside()
    {
        Stage("b1");
        using var updater = Updater();
        Assert.Equal(UpdateStates.Applying, updater.Look().State);
        Assert.True(updater.Draining);

        Directory.Delete(Path.Combine(_install, "update", "staged"), recursive: true);

        Assert.Equal(UpdateStates.None, updater.Look().State);
        Assert.False(updater.Draining);
    }

    [Fact]
    public void A_launcher_that_will_not_start_refuses_the_update_and_the_application_stays_open()
    {
        Stage("b1");
        _launches = false;
        using var updater = Updater();

        var refused = updater.Look();

        Assert.Equal(UpdateStates.Refused, refused.State);
        Assert.Equal("launcher", refused.Problem?.Code);
        Assert.Equal(1, _relaunched);
        Assert.Equal(0, _closed);
        Assert.False(updater.Draining);
    }

    [Fact]
    public void A_start_that_confirms_a_swap_says_once_it_was_updated()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Started, "b1", "0.0.2", "def5678", Now, []));
        using var updater = Updater();

        updater.Started();

        Assert.Equal(new UpdateOutcome(SwapPhase.Installed, "b1", "0.0.2", "def5678", null, null), updater.State.Outcome);
        Assert.Equal(SwapPhase.Confirmed, StagedBuild.ReadJournal(_install)!.Phase);
        Assert.Equal(4242, StagedBuild.ReadJournal(_install)!.Pid);
        var line = Logged("update.installed").Single();
        Assert.Equal("b1", line["build"]!.GetValue<string>());
        Assert.True(line["confirmed"]!.GetValue<bool>());

        using var later = Updater();
        later.Started();
        Assert.Null(later.State.Outcome);
    }

    [Fact]
    public void A_start_after_a_roll_back_says_it_once_with_its_reason_and_marks_it_told()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.RolledBack, "b1", "0.0.2", null, Now, [], Reason: "exited",
            Detail: "the new build's application ended before it came up; the build before it was put back."));
        using var updater = Updater();

        updater.Started();

        Assert.Equal(SwapPhase.RolledBack, updater.State.Outcome?.Phase);
        Assert.Equal("exited", updater.State.Outcome?.Reason);
        Assert.True(StagedBuild.ReadJournal(_install)!.Told);
        Assert.Equal("exited", Logged("update.rolled-back", "warn").Single()["reason"]!.GetValue<string>());

        Assert.Null(updater.Dismiss().Outcome);
        using var later = Updater();
        later.Started();
        Assert.Null(later.State.Outcome);
    }

    /// <summary>
    /// UPDATE1d (D139's UPDATE1b note, D50): the last swap is the journal's record, told or not, on every state, apart from
    /// the outcome the banner says once. It outlives the banner's *Dismiss* and a later start that says nothing, as the
    /// terminal's plain <c>daoris-driver update</c> still says it from the same journal.
    /// </summary>
    [Fact]
    public void The_last_swap_outlives_a_dismissal_and_a_later_start_told_or_not()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.RolledBack, "b1", "0.0.2", "def5678", Now, [], Reason: "exited", Detail: "it ended before it came up."));
        var last = new UpdateOutcome(SwapPhase.RolledBack, "b1", "0.0.2", "def5678", "exited", "it ended before it came up.");
        using var updater = Updater();

        updater.Started();
        Assert.Equal(last, updater.State.Outcome);
        Assert.Equal(last, updater.State.Last);

        var dismissed = updater.Dismiss();
        Assert.Null(dismissed.Outcome);
        Assert.Equal(last, dismissed.Last);

        // Told by the start before, so this one says nothing: the swap is still the last one.
        using var later = Updater();
        later.Started();
        Assert.Null(later.State.Outcome);
        Assert.Equal(last, later.State.Last);
    }

    /// <summary>
    /// UPDATE1d: a swap this start confirmed is the last swap, installed, as the start says it, and stays so once the
    /// launcher finishes; a journal still under way names no ending, so it is no last swap yet.
    /// </summary>
    [Fact]
    public void A_swap_this_start_confirmed_is_the_last_swap_installed_and_one_under_way_is_none_yet()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Started, "b1", "0.0.2", "def5678", Now, []));
        var installed = new UpdateOutcome(SwapPhase.Installed, "b1", "0.0.2", "def5678", null, null);
        using var updater = Updater();

        updater.Started();
        Assert.Equal(SwapPhase.Confirmed, StagedBuild.ReadJournal(_install)!.Phase);
        Assert.Equal(installed, updater.State.Last);

        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.Installed, "b1", "0.0.2", "def5678", Now, [], Pid: 4242, Confirmed: true, Told: true));
        Assert.Equal(installed, updater.Look().Last);

        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Swapping, "b2", "0.0.3", null, Now, []));
        Assert.Null(updater.Look().Last);
    }

    /// <summary>
    /// The launcher finishes a swap with its own running launcher inside <c>update/previous/</c>, which Windows will not
    /// delete, so a whole build was left there until the next update. The application clears it once the swap is done.
    /// </summary>
    [Fact]
    public void A_build_left_in_previous_after_a_finished_swap_is_cleared_by_a_look_and_never_during_one()
    {
        Write("update/previous/Daoris.exe", "the launcher that swapped");
        Write("update/previous/app/Daoris.Desktop.exe", "the build before it");
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Swapping, "b1", "0.0.2", null, Now, []));
        using var updater = Updater();

        updater.Look();
        Assert.True(Directory.Exists(Path.Combine(_install, "update", "previous")), "never while the launcher swaps");

        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Installed, "b1", "0.0.2", null, Now, [], Confirmed: true));
        updater.Look();
        Assert.False(Directory.Exists(Path.Combine(_install, "update", "previous")));
    }

    [Fact]
    public void A_word_with_nothing_staged_or_in_a_mode_this_build_does_not_know_is_refused_and_nothing_is_written()
    {
        using var updater = Updater();

        Assert.Equal(
            Refusals.UpdateNothingStaged,
            Assert.Throws<Shenora.Core.Ipc.ShenoraException>(() => updater.Say(UpdateMode.Now, "screen")).Code);
        Stage("b1");
        Assert.Equal(
            Refusals.UpdateModeUnknown,
            Assert.Throws<Shenora.Core.Ipc.ShenoraException>(() => updater.Say("later", "screen")).Code);
        Assert.Null(InstallUpdate.Read(DataHome));
        Assert.Equal(0, _relaunched);
    }

    [Fact]
    public void The_page_is_told_each_change_once_and_not_a_look_that_changed_nothing()
    {
        Stage("b1");
        _work = new UpdateWork(Driven: 1, Turns: 0);
        using var updater = Updater();

        updater.Look();
        updater.Look();
        _work = new UpdateWork(Driven: 0, Turns: 1);
        updater.Look();

        var told = Raised.Where(message => message.Type == "UPDATE_STATE").ToList();
        Assert.Equal(2, told.Count);
    }

    private List<JsonObject> Logged(string name, string level = "info")
    {
        var folder = Path.Combine(DataHome, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(line => line["event"]!.GetValue<string>() == name && line["level"]!.GetValue<string>() == level)
            .Select(line => line["data"]!.AsObject())
            .ToList();
    }

    /// <summary>A build staged as the publish stages one, consistent with its manifest.</summary>
    private void Stage(string id)
    {
        var staged = Path.Combine(_install, "update", "staged");
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        Write("update/staged/INSTALLED.md", "# Daoris — installed desktop\nnew\n");
        Write("update/staged/Daoris.exe", "new launcher");
        Write("update/staged/app/Daoris.Desktop.exe", "new application");
        Write("update/staged/app/Daoris.Desktop.App.dll", "new library");
        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/'),
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = id, ["version"] = "0.0.1", ["commit"] = "abc1234", ["at"] = "2026-10-03T12:00:00Z", ["files"] = files,
        }.ToJsonString());
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
