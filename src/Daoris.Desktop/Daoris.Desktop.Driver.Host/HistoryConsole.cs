namespace Daoris.Driver.Host;

/// <summary>
/// Clearing finished history from a terminal (HIST1d, D153 point 7, the history-clearing design §6.2, D50): <c>daoris-driver
/// history</c>, <c>history clear --workspace</c>, <c>quest clear [--failed]</c> and <c>ask --clear</c>. The words are the library's
/// (<see cref="HistoryCommand"/>); this wires the real world: the service, the home and its file, the home's process markers
/// (this host runs no session, so a live one any driver on the home marked keeps its unit), and this host's machine log, where
/// a clear's <c>history.cleared</c> says the terminal's door. Routed inside the host's one catch, so a missing home or service is
/// exit 2 and a sentence.
/// </summary>
internal static class HistoryConsole
{
    /// <summary>The words after <c>history</c>.</summary>
    public static Task<int> RunAsync(string[] args, MachineLog log) =>
        RunAsync(HistoryCommand.Read(args, out var problem), problem, log);

    /// <summary>The words after <c>quest</c> (<c>clear …</c>) or <c>ask</c> (<c>--clear …</c>).</summary>
    public static Task<int> RunAsync(WorkScope scope, string[] args, MachineLog log) =>
        RunAsync(HistoryCommand.Read(scope, args, out var problem), problem, log);

    private static async Task<int> RunAsync(HistoryAsk? ask, string? problem, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (ask is null)
        {
            Console.Error.WriteLine($"daoris-driver: {problem}");
            Console.Error.WriteLine(HistoryCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var world = new HistoryWorld(service, home, configPath, new SessionProcesses(Path.Combine(home, "sessions"))) { Log = log };
        return await HistoryCommand.RunAsync(ask, world, Console.Out).ConfigureAwait(false);
    }
}
