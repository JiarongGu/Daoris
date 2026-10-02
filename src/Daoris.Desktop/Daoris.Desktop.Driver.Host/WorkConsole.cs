namespace Daoris.Driver.Host;

/// <summary>
/// Pausing and resuming a work from a terminal (PAUSE1b, D132 §7.2, D50): <c>daoris-driver ask --pause|--resume</c> and
/// <c>quest pause|resume</c>. The words are the library's (<see cref="WorkCommand"/>); this wires the real world: the service,
/// the home and its file, the home's process markers (this host runs no session here, so a live one is reached through the
/// request its loop takes), the configured agent's wire, and this host's machine log, where the pause says the terminal's door.
/// </summary>
internal static class WorkConsole
{
    public static async Task<int> RunAsync(WorkScope scope, string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (WorkCommand.Read(scope, args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris-driver: {problem}");
            Console.Error.WriteLine(WorkCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var world = new WorkWorld(service, home, configPath, new SessionProcesses(Path.Combine(home, "sessions")))
        {
            Door = SessionsConsole.Wire(config),
            Log = log,
        };

        return await WorkCommand.RunAsync(ask, world, Console.Out).ConfigureAwait(false);
    }
}
