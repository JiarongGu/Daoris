namespace Daoris.Driver;

/// <summary>Starts a system's adapter: the plugin that declares it, as its own process, answering the system points.</summary>
public static class SystemAdapters
{
    /// <summary>The adapter of a place among the plugins installed in <paramref name="home"/>, started; refused where none installed and on adapts it.</summary>
    public static Func<SystemPlace, CancellationToken, Task<IHookChannel>> Starter(string home, Action<string>? onLine = null) => async (place, ct) =>
    {
        var entry = PluginCatalog.Load(home).Plugins.FirstOrDefault(each => each.Contributes && each.Manifest.Id == place.Plugin
            && each.Manifest.Systems.Any(system => system.Id == place.System))
            ?? throw new DriverException($"no plugin installed and on adapts system `{place}`: install `{place.Plugin}`, or switch it on.");
        return await HookProcess.StartAsync(entry, home, onLine ?? (_ => { }), ct).ConfigureAwait(false);
    };

    public static Func<SystemPlace, string> Guard(string home) => place =>
        PluginCatalog.Load(home).Plugins.Where(each => each.Contributes && each.Manifest.Id == place.Plugin)
            .SelectMany(each => each.Manifest.Systems).FirstOrDefault(system => system.Id == place.System)?.Preconditions
        ?? SystemPreconditions.Etag;
}
