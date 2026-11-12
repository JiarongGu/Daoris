using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kit's system points: <c>plugins new</c> scaffolds an adapter of a system reached only through its console (its manifest's
/// <c>systems</c>, both points, the kit's <c>system.mjs</c> over the author's read and write), and <c>plugins try</c> asks it at
/// <c>plan/read</c> and <c>apply/perform</c> in the kit's stand-in world, reading each answer by the driver's own reader.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class PluginKitSystemTests : IDisposable
{
    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-kit-system-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginKitSystemTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_system_points_are_the_kit_s_last_with_a_sample_each()
    {
        Assert.Equal([HookPoints.PlanRead, HookPoints.ApplyPerform], HookPoints.All.TakeLast(2));
        Assert.Equal(HookPoints.System, PluginKit.Points.TakeLast(2).Select(point => point.Name));
        Assert.Equal(PluginKit.SampleSystem, (string?)PluginKit.Find(HookPoints.PlanRead)!.Frame["system"]);
        Assert.Equal(PluginKit.SampleSystem, (string?)PluginKit.Find(HookPoints.ApplyPerform)!.Frame["system"]);
    }

    [Fact]
    public void Naming_one_system_point_scaffolds_an_adapter_with_both_its_system_and_the_kit_s_library()
    {
        var plan = PluginKit.Plan("acme-store", [HookPoints.PlanRead], _scratch);

        Assert.Equal(HookPoints.System, plan.Points);
        Assert.Contains(plan.Files, file => file.Name == PluginKitSystems.Library);
        PluginKit.Write(plan);
        var (manifest, problem) = PluginCatalog.ReadFolder(plan.Folder);
        Assert.Null(problem);
        var system = Assert.Single(manifest.Systems);
        Assert.Equal((PluginKitSystems.SystemId, "Store", SystemPreconditions.Etag), (system.Id, system.Name, system.Preconditions));
        var script = File.ReadAllText(Path.Combine(plan.Folder, PluginKit.Script));
        Assert.Contains("'plan/read': system.points['plan/read']", script, StringComparison.Ordinal);
        Assert.Contains("'apply/perform': system.points['apply/perform']", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_scaffold_passes_its_trial_at_both_points_in_the_stand_in_world()
    {
        var plan = PluginKit.Plan("acme-store", [HookPoints.ApplyPerform], _scratch);
        PluginKit.Write(plan);
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(home);

        var trial = await PluginTrials.TryFolderAsync(home, plan.Folder);

        Assert.All(trial.Steps, step => Assert.True(step.Ok, $"{step.Name}: {step.Sentence}"));
        Assert.Contains("typed 1 operation", Assert.Single(trial.Steps, step => step.Name == HookPoints.PlanRead).Sentence, StringComparison.Ordinal);
        Assert.Contains("`applied`", Assert.Single(trial.Steps, step => step.Name == HookPoints.ApplyPerform).Sentence, StringComparison.Ordinal);
    }
}
