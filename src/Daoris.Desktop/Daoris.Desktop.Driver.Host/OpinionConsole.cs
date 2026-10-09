namespace Daoris.Driver.Host;

/// <summary>
/// The second opinion's gate from a terminal (XAGENT1f, D155 point 10, D50): <c>daoris-driver opinion ask|show|stop|anyway|myself</c>.
/// The words are the library's (<see cref="OpinionCommand"/>), and every press the driver's own (<see cref="OpinionPresses"/>);
/// this wires the real world: the service, this home's gate and its sessions' processes.
/// </summary>
internal static class OpinionConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (OpinionCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris-driver: {problem}");
            Console.Error.WriteLine(OpinionCommand.Usage);
            return 2;
        }

        var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        using var service = ServiceClient.FromEnvironment();
        return await OpinionCommand.RunAsync(
            ask, new OpinionPresses(home, service), service, new SessionProcesses(Path.Combine(home, "sessions")), Console.Out).ConfigureAwait(false);
    }
}
