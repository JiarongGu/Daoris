namespace Daoris.Driver;

/// <summary>
/// A declared command found and run one round at a time (<see cref="CommandWire"/>), the one path of the terminal, the window and an
/// adoption: an installed plugin, on and sound, its <c>commands</c> interface on, a command its manifest declares.
/// </summary>
public static class PluginCommandRun
{
    /// <summary>Starts a plugin's process for one round.</summary>
    public delegate Task<IHookChannel> Starter(PluginEntry plugin, CancellationToken ct);

    /// <summary>The plugin and its command.</summary>
    /// <exception cref="DriverException">No such plugin, it is off or refused, its commands are off, or it declares no such command.</exception>
    public static (PluginEntry Plugin, PluginCommandDeclared Command) Find(string home, string id, string command)
    {
        var entry = PluginCatalog.Load(home).Plugins.FirstOrDefault(each => string.Equals(each.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException($"no plugin `{id}` is installed.");
        var named = entry.Manifest.Id;
        if (!entry.Enabled) throw new DriverException($"plugin `{named}` is switched off: switch it on first (`daoris plugin enable {named}`).");
        if (entry.Problem is { } problem) throw new DriverException($"plugin `{named}` is refused: {problem}");
        var commands = entry.Manifest.Interfaces.Commands;
        var declared = commands.FirstOrDefault(each => each.Name == command)
            ?? throw new DriverException(commands.Count == 0
                ? $"plugin `{named}` runs no commands."
                : $"plugin `{named}` has no command `{command}`: one of {string.Join(", ", commands.Select(each => each.Name))}.");
        if (!PluginInterfaceSwitch.IsOn(home, named, PluginInterfaces.Commands))
            throw new DriverException($"plugin `{named}`'s commands are switched off: switch them on in Plugins.");
        return (entry, declared);
    }

    /// <summary>One round: the plugin's process started for it, asked, and stopped.</summary>
    /// <exception cref="DriverException">The process did not start or answer, or answered in another shape.</exception>
    public static async Task<CommandRound> RoundAsync(
        string home, PluginEntry plugin, string command, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> answers, string from,
        CancellationToken ct, Starter? start = null)
    {
        var channel = await (start ?? ((entry, token) => HookChannels.StartAsync(entry, home, _ => { }, token)))(plugin, ct).ConfigureAwait(false);
        await using (channel.ConfigureAwait(false))
        {
            var answer = await channel.CommandAsync(CommandWire.Frame(command, args, answers, from), ct).ConfigureAwait(false);
            return CommandWire.Read(plugin.Manifest.Id, answer);
        }
    }
}
