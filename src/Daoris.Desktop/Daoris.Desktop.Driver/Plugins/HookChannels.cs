namespace Daoris.Driver;

/// <summary>
/// A plugin's hook started and spoken to (D64 §4): the channel the hook set and the landing start through, so neither,
/// nor another module, names the process behind it (STRUCT3a).
/// </summary>
public static class HookChannels
{
    /// <summary>
    /// The plugin's program started in the home as the driver starts it, its handshake made:
    /// <see cref="HookProcess"/>, behind the port. Throws with the reason where it does not start or does not answer.
    /// </summary>
    public static async Task<IHookChannel> StartAsync(
        PluginEntry plugin, string home, Action<string> onLine, CancellationToken ct, TimeSpan? patience = null, TimeProvider? clock = null) =>
        await HookProcess.StartAsync(plugin, home, onLine, ct, patience, clock).ConfigureAwait(false);
}
