namespace Daoris.Driver;

/// <summary>
/// A declared command found and run one round at a time (<see cref="CommandWire"/>), the one path of the terminal, the window and an
/// adoption: an installed plugin, on and sound, its <c>commands</c> interface on, a command its manifest declares.
/// </summary>
public static class PluginCommandRun
{
    /// <summary>Starts a plugin's process for one round.</summary>
    public delegate Task<IHookChannel> Starter(PluginEntry plugin, CancellationToken ct);

    /// <summary>The plugin and its command, its commands on at <paramref name="place"/> (the machine where none is named).</summary>
    /// <exception cref="DriverException">No such plugin, it is off or refused, its commands are off there, or it declares no such command.</exception>
    public static (PluginEntry Plugin, PluginCommandDeclared Command) Find(string home, string id, string command, PluginPlace? place = null)
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
        if (!PluginSwitches.IsOn(home, named, PluginSwitches.Commands, place))
        {
            throw new DriverException(
                $"plugin `{named}`'s commands are switched off at {(place ?? PluginPlace.Machine).Key}: `daoris plugin switch {named} commands on --place {(place ?? PluginPlace.Machine).Key}` switches them on.");
        }
        return (entry, declared);
    }

    /// <summary>One round: the plugin's process started for it, asked, and stopped.</summary>
    /// <exception cref="DriverException">The process did not start or answer, or answered in another shape.</exception>
    /// <remarks>Every round is a <c>plugin.command</c> line by names alone, whichever door asked: its exit, null where it asked or did not end.</remarks>
    /// <param name="log">The plugin log the line goes to; null writes it to the driver's own file under <paramref name="home"/>.</param>
    public static async Task<CommandRound> RoundAsync(
        string home, PluginEntry plugin, CommandCall call, CancellationToken ct, Starter? start = null, PluginLog? log = null)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var own = log is null ? new MachineLog(home, "driver") : null;
        var said = log ?? new PluginLog(own);
        int? exit = null;
        try
        {
            var channel = await (start ?? ((entry, token) => HookChannels.StartAsync(entry, home, _ => { }, token)))(plugin, ct).ConfigureAwait(false);
            await using (channel.ConfigureAwait(false))
            {
                var answer = await channel.CommandAsync(CommandWire.Frame(call), ct).ConfigureAwait(false);
                var round = CommandWire.Read(plugin.Manifest.Id, answer);
                exit = round.Ask is null ? round.Exit : null;
                return round;
            }
        }
        finally
        {
            said.Commanded(plugin.Manifest.Id, call.Command, call.From, call.Session, exit, clock.ElapsedMilliseconds, call.Place.Key);
        }
    }
}
