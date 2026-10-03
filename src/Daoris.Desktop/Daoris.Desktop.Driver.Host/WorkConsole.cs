namespace Daoris.Driver.Host;

/// <summary>
/// Pausing, resuming and abandoning a work from a terminal (PAUSE1b, PAUSE1d, D132 §7.2, D50): <c>daoris-driver ask
/// --pause|--resume|--abandon</c> and <c>quest pause|resume|abandon</c>. The words are the library's (<see cref="WorkCommand"/>);
/// this wires the real world: the service, the home and its file, the home's process markers (this host runs no session here,
/// so a live one is reached through the request its loop takes), the configured agent's wire, this host's machine log, where
/// the act says the terminal's door, and the sync pass an abandon runs before it answers, the one `daoris-driver sync` runs.
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
        // The machine's remotes, re-read as every pass reads them (SYNC0d): a workspace with none answers null, and its
        // declines travel nowhere.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
        var world = new WorkWorld(service, home, configPath, new SessionProcesses(Path.Combine(home, "sessions")))
        {
            Door = SessionsConsole.Wire(config),
            Log = log,
            Sync = async (workspace, ct) => sync.Workspaces.Contains(RemoteTarget.Workspace(workspace), StringComparer.OrdinalIgnoreCase)
                ? await sync.RunOnceAsync(workspace, ct).ConfigureAwait(false)
                : null,
        };

        return await WorkCommand.RunAsync(ask, world, Console.Out).ConfigureAwait(false);
    }
}
