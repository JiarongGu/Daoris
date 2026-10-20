namespace Daoris.Driver.Host;

/// <summary>
/// The driver's standing choices from a terminal (PUSH1f2a, D194, D50): <c>daoris-driver driver</c>, the words <c>daoris driver …</c>
/// hands this program. The grammar, the switch and the words are the library's (<see cref="DriverChoicesCommand"/>); this hands it the
/// process's terminal, the <c>driver.json</c> the host resolves, and the two facts a choice judges by when it acts alone: the service's
/// registry, for a review's procedure, and the planner's verdicts over the service's snapshot, for *Try again*.
/// </summary>
internal static class DriverChoicesConsole
{
    /// <summary>The verb's run, stopped by <paramref name="closing"/>: the person's Ctrl+C, which the families' one handler cancels.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken closing)
    {
        var path = DriverConfig.ResolvePath();
        var world = new DriverChoicesWorld
        {
            // Said under `daoris`, as the CLI said each of these verbs before it handed them here: the person typed `daoris driver …`.
            Instance = InstanceHost.World(DriverConfig.HomeOf(path), "daoris"),
            ConfigPath = path,
            Registry = RegistryAsync,
            Verdicts = VerdictsAsync,
        };

        try
        {
            return await DriverChoicesCommand.RunAsync(args, world, closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            // The token stopped a wait (the pipe's answer, the service's verdicts); a write under way completed, being atomic.
            await Console.Error.WriteLineAsync(
                "daoris: stopped at your Ctrl+C. A change already sent to Daoris's window may still be confirmed there, and "
                + "`daoris-driver result <id>` asks how it ended; nothing more was done here.").ConfigureAwait(false);
            return 2;
        }
    }

    /// <summary>The registry's checkouts, or why the service could not be read: a review's put is never refused for want of it.</summary>
    private static async Task<ChoicesRegistry> RegistryAsync(CancellationToken ct)
    {
        try
        {
            using var service = ServiceClient.FromEnvironment();
            var registry = await service.RegistryAsync(ct).ConfigureAwait(false);
            return new([.. registry.Select(row => (row.Repository, (string?)row.Workspace, row.Root))], null);
        }
        catch (Exception unread) when (unread is HttpRequestException or TaskCanceledException or DriverException or InvalidOperationException
                                           && !ct.IsCancellationRequested)
        {
            return new(null, unread.Message.TrimEnd('.'));
        }
    }

    /// <summary>
    /// The planner's verdicts over a fresh snapshot (<see cref="SessionGroups.VerdictsAsync"/>), as a window with no look of its own
    /// plans: the configured adapter's wire, this machine's plugins among the adapters, else the pipe.
    /// </summary>
    private static async Task<IReadOnlyList<Consideration>> VerdictsAsync(DriverConfig config, CancellationToken ct)
    {
        using var service = ServiceClient.FromEnvironment();
        var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        SessionWire wire;
        try
        {
            wire = AdapterSet.Built().WithPlugins(PluginCatalog.Load(home, AdapterSet.Built().Names)).Resolve(config.Adapter).Wire;
        }
        catch (DriverException)
        {
            wire = SessionWire.Pipe;
        }

        return await SessionGroups.VerdictsAsync(service, config, wire, lastLook: null, ct).ConfigureAwait(false);
    }
}
