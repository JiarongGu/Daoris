using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound to <i>Work in place</i>, a graph with no <c>land</c> that ends at the person's review, is a process like any other: nothing
/// holds it for landing nothing, the runtime lands nothing for it, and its holds are the graph's.
/// </summary>
public sealed class InPlaceProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "in-place-process", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_run_bound_to_work_in_place_is_held_by_nothing_and_lands_nothing()
    {
        var shipped = WorkflowDefaults.Of("in-place")!;
        var digest = RunHome.SaveGraph(_home, shipped.GraphJson, workflow: "in-place");
        RunHome.Bind(_home, "chat-in-place", digest, DateTimeOffset.UnixEpoch, workflow: "in-place");

        var process = WorkflowProcesses.Read(_home, DriverConfig.Empty, RunHome.Repository, null, "chat-in-place");

        Assert.True(process.Named);
        Assert.Null(process.Problem);
        Assert.NotNull(process.Graph);
        Assert.False(process.LandsByRuntime);
        Assert.False(process.JudgesHolds);
    }
}
