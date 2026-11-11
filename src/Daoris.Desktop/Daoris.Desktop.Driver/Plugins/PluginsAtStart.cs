namespace Daoris.Driver;

/// <summary>
/// The plugins a loop starts with, the desktop's and the headless host's (<c>drive</c>, <c>--once</c> among them) alike: the renames the
/// installed manifests declare moved first (<see cref="PluginRenames.Move"/>), so no old id's copy is started beside its new one, then
/// the hook set, each place's hand judging its work process's start (<see cref="PlaceHands"/>).
/// </summary>
public static class PluginsAtStart
{
    public static HookSet Set(string home, Action<string, string>? say, SessionOutput? output, MachineLog? log, PluginHealth? health = null)
    {
        PluginRenames.Move(home, log);
        return new HookSet(home, output, say: say, log: log, health: health, hand: PlaceHands.For(home));
    }
}
