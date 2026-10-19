using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver sync</c>'s pass through the running Daoris (RPCOPEN1, RPC1e, the terminal RPC design §7.2): each circle a
/// <c>SYNC_NOW</c>, the pass's report the same as the set's own, against a stand-in of the pipe that answers as the route does.
/// </summary>
public sealed class SyncThroughInstanceTests
{
    private static readonly string[] Circles = ["default", "team"];

    /// <summary>The set's own pass, as <c>RemoteSyncSet.RunOnceAsync</c> reports it: each circle's problems and notes prefixed by its name.</summary>
    private static Task<SyncReport> Alone(string? workspace, List<string?>? asked = null)
    {
        asked?.Add(workspace);
        var which = workspace is null ? Circles : [RemoteTarget.Workspace(workspace)];
        if (which.Any(circle => !Circles.Contains(circle)))
        {
            throw new DriverException(RemoteSyncSet.NoRemote(workspace!));
        }

        var problems = which.Where(circle => circle == "team").Select(circle => $"{circle}: the remote refused the push.").ToList();
        var notes = which.Select(circle => $"{circle}: 2 quests up").ToList();
        return Task.FromResult(new SyncReport(problems.Count == 0 ? null : string.Join(" · ", problems), notes));
    }

    private static object Answer(string route, JsonElement payload)
    {
        Assert.Equal("SYNC_NOW", route);
        var workspace = payload.GetProperty("workspace").GetString();
        var report = Alone(workspace).GetAwaiter().GetResult();
        return new { Workspace = RemoteTarget.Workspace(workspace), report.Problem, report.Notes };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("default")]
    [InlineData("team")]
    public async Task Through_the_instance_the_report_is_the_sets_own(string? workspace)
    {
        if (!OperatingSystem.IsWindows()) return;
        var expected = await Alone(workspace);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Respond = Answer;
        var asked = new List<string?>();

        var report = await SyncPass.RunAsync(workspace, Circles, name => Alone(name, asked), home.World("sync"));

        Assert.NotNull(report);
        Assert.Equal(expected.Problem, report.Problem);
        Assert.Equal(expected.Notes, report.Notes);
        // Nothing ran in this process: the instance's own set ran every pass.
        Assert.Empty(asked);
        Assert.Equal(
            workspace is null ? Circles : [workspace],
            standIn.Presses.Select(press => press.GetProperty("payload").GetProperty("workspace").GetString()).ToArray());
        Assert.All(standIn.Presses, press => Assert.Equal("DAORIS.DRIVER", press.GetProperty("module").GetString()));
    }

    [Fact]
    public async Task With_no_instance_it_is_the_sets_own_pass_whole_and_asked_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var asked = new List<string?>();

        var report = await SyncPass.RunAsync(null, Circles, name => Alone(name, asked), home.World("sync"));

        Assert.NotNull(report);
        Assert.Equal([null], asked);
        Assert.Equal((await Alone(null)).Notes, report.Notes);
    }

    [Fact]
    public async Task A_circle_this_machine_has_no_remote_for_is_the_sets_refusal_and_nothing_is_pressed()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Respond = Answer;

        var thrown = await Assert.ThrowsAsync<DriverException>(() => SyncPass.RunAsync("ghost", Circles, name => Alone(name), home.World("sync")));

        Assert.Contains("this machine has no remote for `ghost`", thrown.Message);
        Assert.Empty(standIn.Presses);
    }

    [Fact]
    public async Task An_instance_that_does_not_answer_is_said_and_no_pass_runs_here()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        using var running = RunningAddress.Write(home.Home, "http://localhost:5177", home.Pipe);
        var asked = new List<string?>();
        var instance = home.World("sync");

        var report = await SyncPass.RunAsync(null, Circles, name => Alone(name, asked), new InstanceWorld
        {
            Home = instance.Home, Environment = instance.Environment, BaseDirectory = instance.BaseDirectory, Status = instance.Status,
            Out = instance.Out, Error = instance.Error, Verb = "sync", Answer = TimeSpan.FromSeconds(1),
        });

        Assert.Null(report);
        Assert.Empty(asked);
        Assert.Contains("did not answer; nothing was done.", home.Error.ToString());
    }
}
