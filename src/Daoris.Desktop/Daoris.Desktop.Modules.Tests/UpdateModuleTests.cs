using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// UPDATE1 (D139 §3, D50): the screen's door to an install's update, <c>DAORIS.UPDATE</c> — <c>STATE</c> for the banner,
/// <c>SET</c> for *Update when idle*, *Update now* and *Not now*, written to the request `daoris-driver update` writes, and
/// <c>DISMISS</c> for an outcome already said.
/// </summary>
public sealed class UpdateModuleTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly string _install;

    private int _closed;

    public UpdateModuleTests()
    {
        _install = Path.Combine(Home, "install");
        Directory.CreateDirectory(Path.Combine(_install, "app"));
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    private string DataHome => Path.Combine(_install, "data");

    private (UpdateModule Module, InstallUpdater Updater) Module(UpdateWork? work = null)
    {
        var updater = new InstallUpdater(
            _install, DataHome, MachineLog.None, () => work ?? new UpdateWork(1, 0), relaunch: () => true, close: () => _closed++,
            bus: Bus, clock: () => Now, pid: 1);
        return (new UpdateModule(updater), updater);
    }

    [Fact]
    public async Task State_answers_what_the_banner_reads_in_the_page_s_shape()
    {
        Stage("b1");
        var (module, updater) = Module();
        using var _ = updater;
        updater.Look();

        var state = await AnswerAsync(module, "STATE");

        Assert.Equal("draining", state.GetProperty("state").GetString());
        Assert.Equal("b1", state.GetProperty("staged").GetProperty("id").GetString());
        Assert.Equal("0.0.1", state.GetProperty("staged").GetProperty("version").GetString());
        Assert.Equal("abc1234", state.GetProperty("staged").GetProperty("commit").GetString());
        Assert.Equal("when-idle", state.GetProperty("mode").GetString());
        Assert.Equal(1, state.GetProperty("driven").GetInt32());
        Assert.Equal(0, state.GetProperty("turns").GetInt32());
        // No machine path reaches the page: the install is this machine's.
        Assert.DoesNotContain(_install.Replace("\\", "\\\\"), state.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Set_writes_the_word_for_the_staged_build_as_the_terminal_does_and_answers_the_new_state()
    {
        Stage("b1");
        var (module, updater) = Module();
        using var _ = updater;

        var state = await AnswerAsync(module, "SET", new { mode = "not-now" });

        Assert.Equal("waiting", state.GetProperty("state").GetString());
        Assert.Equal(new UpdateRequest(UpdateMode.NotNow, "b1", Now), InstallUpdate.Read(DataHome));
        Assert.Equal(0, _closed);
    }

    [Fact]
    public async Task Set_refuses_a_mode_it_does_not_know_and_a_word_with_nothing_staged_by_their_codes()
    {
        var (module, updater) = Module();
        using var _ = updater;

        Assert.StartsWith(Refusals.UpdateNothingStaged, await RefusalAsync(module, "SET", new { mode = "now" }));
        Stage("b1");
        Assert.StartsWith(Refusals.UpdateModeUnknown, await RefusalAsync(module, "SET", new { mode = "later" }));
        Assert.StartsWith(Refusals.UpdateModeUnknown, await RefusalAsync(module, "SET", new { }));
        Assert.Equal(0, _closed);
    }

    [Fact]
    public async Task Dismiss_clears_an_outcome_already_said()
    {
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.RolledBack, "b1", "0.0.2", null, Now, [], Reason: "exited"));
        var (module, updater) = Module();
        using var _ = updater;
        updater.Started();
        Assert.Equal("rolled-back", (await AnswerAsync(module, "STATE")).GetProperty("outcome").GetProperty("phase").GetString());

        var state = await AnswerAsync(module, "DISMISS");

        Assert.Equal(JsonValueKind.Null, state.GetProperty("outcome").ValueKind);
    }

    [Fact]
    public async Task A_type_it_does_not_know_is_refused()
    {
        var (module, updater) = Module();
        using var _ = updater;

        var response = await AskAsync(module, "INSTALL");

        Assert.False(response.Success);
    }

    private void Stage(string id)
    {
        var staged = Path.Combine(_install, "update", "staged");
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = id, ["version"] = "0.0.1", ["commit"] = "abc1234", ["at"] = "2026-10-03T12:00:00Z",
            ["files"] = new JsonArray(),
        }.ToJsonString());
    }
}
