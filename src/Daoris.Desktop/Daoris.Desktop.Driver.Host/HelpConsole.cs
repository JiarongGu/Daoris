namespace Daoris.Driver.Host;

/// <summary>
/// Ask Daoris's history from a terminal (ASKHIST1, D50): <c>daoris-driver help list|resume|rename|pin|unpin|delete</c>. The
/// words are the library's (<see cref="HelpCommand"/>); this wires the real world as <c>sessions</c> does: the service, the
/// home's choices and files, the configured agent's wire, and this host's machine log, where a delete says the terminal's door.
/// </summary>
internal static class HelpConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (HelpCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"help: {problem}");
            Console.Error.WriteLine(HelpCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var adapters = AdapterSet.Built();

        return await HelpCommand.RunAsync(
            ask, new SessionsWorld(service, home, config, SessionsConsole.Wire(config), log), adapter => Resumes(adapters, adapter),
            Console.Out).ConfigureAwait(false);
    }

    /// <summary>Whether an adapter this build carries resumes a conversation by its id; one it does not carry cannot.</summary>
    private static bool Resumes(AdapterSet adapters, string adapter)
    {
        try
        {
            return adapters.Resolve(adapter).Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
