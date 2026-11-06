using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1e (the co-agent design §3.4): a harness may name its own words for the asking and the full postures beside its one
/// <c>posture</c>, trimmed and never a reason to refuse the plugin; a blank or a non-text word is none, and the door speaks what the
/// manifest said, so a start in that posture is no longer refused for want of one.
/// </summary>
public sealed class PluginPosturesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugin-postures", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_declared_harness_may_name_its_asking_and_full_postures()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.postures");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme.postures",
              "harnesses": [ { "name": "wide-agent", "command": ["wide"], "posture": "ordinary", "askingPosture": " ask ", "fullPosture": "everything" },
                             { "name": "plain-agent", "command": ["plain"], "posture": "ordinary" },
                             { "name": "odd-agent", "command": ["odd"], "askingPosture": "", "fullPosture": 7 } ] }
            """);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Null(entry.Problem);
        Assert.Equal(
            [("ask", "everything"), (null, null), (null, null)],
            entry.Manifest.Harnesses.Select(harness => (harness.AskingPosture, harness.FullPosture)));

        var adapters = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
        var wide = adapters.Resolve("wide-agent");
        Assert.Equal(("ask", "everything"), (wide.AskingPosture, wide.FullPosture));
        var plain = adapters.Resolve("plain-agent");
        Assert.Equal(((string?)null, (string?)null), (plain.AskingPosture, plain.FullPosture));
    }
}
