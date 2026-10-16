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
    /// <summary>
    /// The milestone of every unit this home names (FABRIC-MILESTONE1a, D50), read-only: the home's records, the trees, and each repository's
    /// recovery refs and the sessions in use from the service. A service that does not answer is said, with what that leaves unread: a tree a
    /// session holds then reads as idle, and no recovery ref is listed.
    /// </summary>
    internal static async Task<(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<string> Unread)> ReadAsync(string home)
    {
        var unread = new List<string>();
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var service = ServiceClient.FromEnvironment();
            foreach (var row in await service.RegistryAsync().ConfigureAwait(false))
            {
                if (!string.IsNullOrWhiteSpace(row.Root)) roots.TryAdd(row.Repository, row.Root);
            }

            foreach (var session in await service.ActiveSessionsAsync().ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(session.Tree)) inUse.Add(session.Tree);
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or TaskCanceledException)
        {
            unread.Add($"the service did not answer ({error.Message}), so recovery refs were not read and a tree a session holds may read as idle");
        }

        var units = await WorkInventories.MilestonesAsync(home, roots, inUse, unread).ConfigureAwait(false);
        return (units, unread);
    }

    /// <summary>
    /// <c>daoris-driver work state [&lt;unit&gt;] [--entries]</c> (FABRIC-MILESTONE1a, D50): each unit's milestone, from the inventory the
    /// records derive; a unit named by its id, its session or its branch. A look: it changes nothing and tells no window. The page's
    /// is MILESTONE1b's. Exit 0 clean; 1 where the unit named is none; 2 for words not understood.
    /// </summary>
    public static async Task<int> MilestonesAsync(string[] args)
    {
        var entries = args.Contains("--entries");
        var words = args.Where(word => word != "--entries").ToList();
        if (words.Count > 1 || words.Any(word => word.StartsWith('-')))
        {
            Console.Error.WriteLine($"daoris-driver: `work state` takes one unit and `--entries`: {string.Join(" ", args)}");
            Console.Error.WriteLine("usage: daoris-driver work state [<unit>] [--entries]");
            return 2;
        }

        var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        var (units, unread) = await ReadAsync(home).ConfigureAwait(false);
        foreach (var line in unread) Console.Error.WriteLine($"work: {line}");
        if (words.Count == 1) units = WorkInventories.Named(units, words[0]);
        if (words.Count == 1 && units.Count == 0)
        {
            Console.Error.WriteLine($"work: `{words[0]}` names no unit of work on this machine.");
            return 1;
        }

        foreach (var line in WorkWords.All(units, entries || words.Count == 1)) Console.WriteLine(line);
        return 0;
    }

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
        var world = new WorkWorld(service, home, configPath, new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids)))
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
