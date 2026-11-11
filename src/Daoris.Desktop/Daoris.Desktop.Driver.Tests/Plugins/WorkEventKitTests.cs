using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kit's entry for <c>work/event</c>: the last point, an observation whose sample is the
/// driver's own frame under the sample run, and a scaffold that gives a plugin on it a handler.
/// </summary>
public sealed class WorkEventKitTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "event-kit", Guid.NewGuid().ToString("N")[..8]);

    public WorkEventKitTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public void The_kit_lists_the_kinds_of_its_points_in_the_drivers_order_the_events_last()
    {
        Assert.Equal(["decision", "observation", "query", "observation", "observation", "event", "act", "observation"], PluginKit.Points.Select(point => point.Kind));
        Assert.Equal(HookPoints.Event, PluginKit.Points[^1].Name);
    }

    [Fact]
    public void The_samples_frame_is_the_one_the_driver_builds_under_the_sample_run()
    {
        var point = PluginKit.Find(HookPoints.Event)!;
        var built = HookFrames.Event(PluginKit.SampleEvent, WorkEventGrammar.Sample, 1, 0, new HashSet<string>());

        Assert.Null(built.Unsent);
        Assert.Equal("sample", point.Frame["run"]!.GetValue<string>());
        Assert.Equal(JsonSerializer.SerializeToNode(built.Frame)!.ToJsonString(), point.Frame.ToJsonString());
    }

    [Fact]
    public void A_scaffold_on_the_events_point_answers_it()
    {
        var plan = PluginKit.Plan("acme-listener", [HookPoints.Event], _scratch);

        Assert.Contains("'work/event'", plan.Files.Single(file => file.Name == PluginKit.Script).Content);
    }
}
