using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The examples that work are the kit's own output filled in: their kit-owned files are what the kit writes now, so a change to the
/// kit changes every example in the same commit, and each performer passes its cycle through the driver.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PluginKitExampleTests : IDisposable
{
    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-kit-examples-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginKitExampleTests() => Directory.CreateDirectory(_scratch);

    private string Home => Path.Combine(_scratch, "home");

    public void Dispose()
    {
        try { GitSeed.Remove(_scratch); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The kit's files every working example keeps as the kit writes them.</summary>
    private static readonly string[] KitFiles = [PluginKit.WorkScript, PluginKitStorage.Library, PluginKitStorage.Test, PluginKit.Test, PluginKit.ContractTest];

    private static string Example(string name) => Path.Combine(GitSeed.RepoRoot(), "examples", "plugins", name);

    /// <summary>The kit's files are the same under every id, so a scaffold of a family of the test's own stands for Daoris's.</summary>
    private void KeepsTheKitFiles(string name, IEnumerable<string> files)
    {
        var made = PluginKit.Plan("acme-kit", [HookPoints.Perform], _scratch);
        foreach (var file in files)
        {
            Assert.Equal(made.Files.Single(each => each.Name == file).Content, File.ReadAllText(Path.Combine(Example(name), file)).Replace("\r\n", "\n"));
        }
    }

    /// <summary>The example copied out, so the cycle's journal and what it makes never land in the repository's folder.</summary>
    private async Task CyclePassesAsync(string name)
    {
        var copy = Path.Combine(_scratch, name);
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(Example(name))) File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        var cycle = await PluginCycles.TryAsync(Home, copy, PluginCycles.Read(Path.Combine(copy, PluginKit.CycleFixture)) with { Deadline = TimeSpan.FromSeconds(1) });
        Assert.True(cycle.Passed, string.Join(" | ", cycle.Steps.Select(s => $"{(s.Ok ? "ok" : "FAIL")} {s.Name}: {s.Sentence}")));
    }

    [Fact]
    public async Task The_example_performer_is_the_kit_s_scaffold_filled_in_and_passes_its_cycle()
    {
        KeepsTheKitFiles("local-preview", [.. KitFiles, PluginKit.CycleFixture]);

        var (manifest, problem) = PluginCatalog.ReadFolder(Example("local-preview"));
        Assert.Null(problem);
        Assert.Equal("local-preview/perform", Assert.Single(manifest.Steps).Kind);
        await CyclePassesAsync("local-preview");
    }

    /// <summary>
    /// <c>examples/plugins/local-release</c> keeps the kit's files as the kit writes them (its cycle fixture is its own, naming
    /// the tail), and declares find, release and publish as three performed, idempotent, reconciled steps.
    /// </summary>
    [Fact]
    public async Task The_release_example_keeps_the_kit_s_files_and_declares_three_performed_steps()
    {
        KeepsTheKitFiles("local-release", KitFiles);

        var (manifest, problem) = PluginCatalog.ReadFolder(Example("local-release"));
        Assert.Null(problem);
        Assert.Equal(["local-release/find", "local-release/release", "local-release/publish"], manifest.Steps.Select(step => step.Kind));
        Assert.All(manifest.Steps, step =>
        {
            Assert.Equal("performed", step.Reach);
            Assert.True(step.Perform!.Idempotent);
            Assert.True(step.Perform.Reconcile);
            Assert.Equal("local-release/receipt@1", step.Evidence);
        });
        Assert.Equal("local-release/release@1", manifest.Steps[0].Outputs["release"]);
        Assert.Equal("local-release/release@1", manifest.Steps[1].Inputs["release"]);
        Assert.Equal("local-release/release@1", manifest.Steps[2].Inputs["release"]);

        // Each step says what it does in both languages, and none says it publishes anywhere: it copies into its own registry folder.
        Assert.All(manifest.Steps, step =>
        {
            Assert.NotNull(step.Title);
            Assert.NotNull(step.About);
            foreach (var words in step.Title!.Texts.Concat(step.About!.Texts))
            {
                Assert.DoesNotContain("publish", words, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("发布", words, StringComparison.Ordinal);
            }
        });
        Assert.Contains("registry", manifest.Steps[2].About!.In("en"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("publish", PluginCycles.Read(Path.Combine(Example("local-release"), PluginKit.CycleFixture)).Step);
        await CyclePassesAsync("local-release");
    }

    /// <summary>
    /// Daoris's own pull-request plugins keep the kit's work journal as the kit writes it, so their places' work processes, which share
    /// one data folder, keep each other's records.
    /// </summary>
    [Theory]
    [InlineData("daoris-azure-devops")]
    [InlineData("daoris-github")]
    public void Each_pull_request_example_keeps_the_kit_s_journal_and_its_store(string example) => KeepsTheKitFiles(example, KitFiles);

    /// <summary>The development-report example listens on `session/ended` alone and keeps its reports through the kit's store, declaring its two commands and its storage.</summary>
    [Fact]
    public void The_development_report_example_keeps_the_kit_s_store_and_declares_its_commands()
    {
        KeepsTheKitFiles("daoris-dev-report", [PluginKitStorage.Library, PluginKitStorage.Test]);
        var (manifest, problem) = PluginCatalog.ReadAsWritten("daoris-dev-report", Path.Combine(Example("daoris-dev-report"), PluginCatalog.ManifestName));
        Assert.Null(problem);
        Assert.Equal(["session/ended"], manifest.Hooks!.Points);
        Assert.Equal(["report", "inbox"], manifest.Interfaces.Commands.Select(each => each.Name));
        Assert.All(manifest.Interfaces.Commands, each => Assert.True(each.Tool));
        Assert.Empty(manifest.Interfaces.Refused);
        Assert.Equal(["note", "defect", "blocking"], manifest.Interfaces.Commands[0].Args.Single(each => each.Name == "severity").Choices);
    }
}
