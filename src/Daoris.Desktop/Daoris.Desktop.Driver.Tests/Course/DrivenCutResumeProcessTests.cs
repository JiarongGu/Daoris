using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session Daoris cut while its turn ran, by the driver's close or by the sweep of a run nothing here runs any more, goes on in its
/// own record as that record's next turn at the next start (<see cref="CarryOnResume"/>): its own conversation resumed on its own agent and
/// account with Daoris's restart word, its quest never released, no part opened beside it, and the first turn's cut still counted as the
/// strike it is. The real watch comes up over the harness's home (<see cref="DrivenMoveHarness"/>), its stub resuming the first conversation.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class DrivenCutResumeProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-cut-resume") { Resumes = true };

    public Task InitializeAsync() => _h.InitializeAsync();

    public async Task DisposeAsync() => await _h.DisposeAsync();

    [Theory]
    [InlineData("shutdown")]
    [InlineData("sweep")]
    public async Task A_session_Daoris_cut_goes_on_in_its_own_record_as_its_next_turn(string cut)
    {
        await _h.CutAsync(sweep: cut == "sweep");
        var tree = JsonNode.Parse(_h.Service.Record("s1"))!["tree"]?.GetValue<string>();
        Assert.Equal("Taken", _h.Service.Status("q1"));

        _h.ComeUp();
        await _h.SettleAsync(ready: () => _h.Service.Status("q1") == "Done");
        await _h.StopAsync();

        var seen = _h.Seen();
        Assert.True((_h.Service.SessionCount, _h.Service.State("s1")) == (1, "completed"), seen);
        var resumed = Assert.Single(_h.Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        Assert.Single(_h.Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        Assert.Equal(1, _h.Carried("s1", ShellClose.Words));
        var (on, key) = Assert.Single(_h.Service.DaorisSaid);
        Assert.Equal("s1", on);
        Assert.StartsWith("restart/s1/", key);
        Assert.Empty(_h.Store("run-probe").Of("s1"));

        var record = JsonNode.Parse(_h.Service.Record("s1"))!.AsObject();
        Assert.Equal(tree, record["tree"]?.GetValue<string>());
        Assert.Null(record["from"]);
        var turns = record["turns"]!.AsArray();
        Assert.Equal(["start", "daoris"], turns.Select(turn => turn!["cause"]!.GetValue<string>()));
        Assert.Equal(("stopped", true), (turns[0]!["end"]!["state"]!.GetValue<string>(), turns[0]!["end"]!["interrupted"]!.GetValue<bool>()));
        Assert.Equal(1, (await new ServiceClient(_h.Service.Url, null).StrikesAsync())["q1"]);
    }

    /// <summary>The driver host ended outright mid-turn leaves a record working with nothing running it: the next start's sweep ends it, and it is carried on.</summary>
    [Fact]
    public async Task A_host_killed_mid_turn_is_carried_on_at_the_next_start_with_Daoris_s_restart_word()
    {
        await KillHostAsync();
        Assert.Equal("working", _h.Service.State("s1"));

        _h.ComeUp(markers: true);
        await _h.SettleAsync(ready: () => _h.Service.Status("q1") == "Done");
        await _h.StopAsync();

        Assert.True((_h.Service.SessionCount, _h.Service.State("s1")) == (1, "completed"), _h.Seen());
        Assert.Equal("conv-s1", Assert.Single(_h.Heard(), line => line["method"]!.GetValue<string>() == "session/resume")["conversation"]!.GetValue<string>());
        Assert.Equal(1, _h.Carried("s1", ShellClose.Words));
        var (_, key) = Assert.Single(_h.Service.DaorisSaid);
        Assert.StartsWith("restart/s1/", key);
        var turns = JsonNode.Parse(_h.Service.Record("s1"))!["turns"]!.AsArray();
        Assert.Equal(("stopped", true), (turns[0]!["end"]!["state"]!.GetValue<string>(), turns[0]!["end"]!["interrupted"]!.GetValue<bool>()));
    }

    /// <summary>The first run in a process of its own, <c>daoris-driver drive --once</c>, ended outright mid-turn with its agent: no stop, no close.</summary>
    private async Task KillHostAsync()
    {
        _h.SaveConfig();
        var info = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = _h.Home };
        info.ArgumentList.Add(HostBuild.Dll());
        info.ArgumentList.Add("drive");
        info.ArgumentList.Add("--once");
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList()) info.Environment.Remove(name);
        info.Environment[DaorisHome.Variable] = _h.Home;
        info.Environment[ServiceClient.UrlVariable] = _h.Service.Url;
        using var host = System.Diagnostics.Process.Start(info)!;
        try
        {
            await Poll.Until(() => _h.Heard().Any(line => line["method"]!.GetValue<string>() == "session/prompt") && _h.Service.Status("q1") == "Taken", _h.Seen);
        }
        finally
        {
            host.Kill(entireProcessTree: true);
            await host.WaitForExitAsync();
        }
    }

    /// <summary>The person's stop of a record nothing runs is never carried on, though the sweep's end of the same record is.</summary>
    [Fact]
    public async Task A_stop_the_person_asked_is_never_carried_on_where_a_cut_the_system_made_is()
    {
        await _h.CutAsync(sweep: false, person: true);

        _h.ComeUp();
        await _h.LooksAsync();
        await _h.StopAsync();

        Assert.Equal((1, "stopped", "Taken"), (_h.Service.SessionCount, _h.Service.State("s1"), _h.Service.Status("q1")));
        Assert.Empty(_h.Heard().Where(line => line["method"]!.GetValue<string>() == "session/resume"));
        Assert.Equal(0, _h.Carried("s1", ShellClose.Words));
        Assert.Empty(_h.Service.DaorisSaid);
    }
}
