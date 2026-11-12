using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class PluginCycleStaleTests : IDisposable
{
    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-cycle-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginCycleStaleTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(_scratch); attempt++)
        {
            try
            {
                GitSeed.Remove(_scratch);
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task A_performer_whose_step_declares_no_receipt_passes_the_cycle_s_stale_step()
    {
        var plan = PluginKit.Plan("acme-bare", [HookPoints.Perform], _scratch);
        PluginKit.Write(plan);
        var manifest = Path.Combine(plan.Folder, "plugin.json");
        var text = File.ReadAllText(manifest);
        var receipt = "\"evidence\": { \"schema\": \"acme-bare/receipt@1\" },";
        Assert.Contains(receipt, text);
        File.WriteAllText(manifest, text.Replace(receipt, "", StringComparison.Ordinal));

        var cycle = await PluginCycles.TryAsync(Path.Combine(_scratch, "home"), plan.Folder, CycleFixture.Default with { Deadline = TimeSpan.FromSeconds(1) });

        Assert.True(cycle.Passed, string.Join(" | ", cycle.Steps.Select(step => $"{(step.Ok ? "ok" : "FAIL")} {step.Name}: {step.Sentence}")));
    }
}
