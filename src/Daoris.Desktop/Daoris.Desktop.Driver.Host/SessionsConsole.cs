namespace Daoris.Driver.Host;

/// <summary>
/// Sessions from a terminal (SESSUX1g, D126 §7.1, D50): <c>daoris-driver sessions</c>. The words are the library's
/// (<see cref="SessionsCommand"/>); this wires the real world: the service, the home's choices and files, the configured
/// agent's wire, and this host's machine log, where an archive and a delete say the terminal's door.
/// </summary>
internal static class SessionsConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (SessionsCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"sessions: {problem}");
            Console.Error.WriteLine(SessionsCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var door = Wire(config);

        return await SessionsCommand.RunAsync(ask, new SessionsWorld(service, home, config, door, log), Console.Out).ConfigureAwait(false);
    }

    /// <summary>The configured agent's wire, as the loop plans by it (D70); the pipe, the stricter door, when it names none this build has.</summary>
    internal static SessionWire Wire(DriverConfig config)
    {
        try
        {
            return AdapterSet.Built().Resolve(config.Adapter).Wire;
        }
        catch (DriverException)
        {
            return SessionWire.Pipe;
        }
    }
}
