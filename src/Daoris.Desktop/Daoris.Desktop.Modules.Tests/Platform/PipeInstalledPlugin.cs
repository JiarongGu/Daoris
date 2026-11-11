using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A plugin installed in a test home from a source folder beside it, whose manifest then moves on a version, and the stand-in machine that
/// holds it: what a press's card judges an update from at the ask. The source folder is removed with it.
/// </summary>
internal sealed class PipeInstalledPlugin : IDisposable
{
    private const string Manifest = """{ "id": "acme.watch", "name": "Watch", "version": "1.0.0", "hooks": { "command": ["node", "${plugin}/run.mjs"], "points": ["work/land"] } }""";

    private readonly string _source;

    public PipeInstalledPlugin(string home)
    {
        _source = home + "-watch-source";
        Directory.CreateDirectory(_source);
        var manifest = Path.Combine(_source, PluginCatalog.ManifestName);
        File.WriteAllText(manifest, Manifest);
        PluginInstall.Add(home, _source, []);
        File.WriteAllText(manifest, Manifest.Replace("1.0.0", "1.1.0", StringComparison.Ordinal));
        Grounds.Facts = Grounds.Facts with { Home = home, Plugins = PluginCatalog.Load(home) };
    }

    public StubGrounds Grounds { get; } = new();

    public void Dispose() => Directory.Delete(_source, recursive: true);
}
