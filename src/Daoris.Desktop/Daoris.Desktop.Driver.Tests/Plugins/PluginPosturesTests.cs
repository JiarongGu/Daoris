using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A harness may name its own words for the asking and the full postures beside its one
/// <c>posture</c>, trimmed and never a reason to refuse the plugin; a blank or a non-text word is none, and the door speaks what the
/// manifest said, so a start in that posture is not refused for want of one.
/// </summary>
public sealed class PluginPosturesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugin-postures", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) GitSeed.Discard(_home);
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

        var adapters = ProviderRegistry.Built().WithPlugins(PluginCatalog.Load(_home, ProviderRegistry.Built().Names));
        var wide = adapters.Door("wide-agent");
        Assert.Equal(("ask", "everything"), (wide.Postures.Asking, wide.Postures.Full));
        var plain = adapters.Door("plain-agent");
        Assert.Equal(((string?)null, (string?)null), (plain.Postures.Asking, plain.Postures.Full));
    }
}
