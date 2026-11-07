namespace Daoris.Driver.Host;

/// <summary>
/// Pausing, resuming and abandoning a work from a terminal:<c>daoris ask
/// --pause|--resume|--abandon</c> and <c>quest pause|resume|abandon</c>. The words are the library's (<see cref="WorkCommand"/>);
/// this wires the real world: the service, the home and its file, the home's process markers (this host runs no session here,
/// so a live one is reached through the request its loop takes), the configured agent's wire, this host's machine log, where
/// the act says the terminal's door, and the sync pass an abandon runs before it answers, the one `daoris remote sync` runs.
/// </summary>
internal static class WorkConsole
{
    /// <summary>
    /// The milestone of every unit this home names, read-only: the home's records, the trees, and each repository's
    /// recovery refs and the sessions in use from the service. A service that does not answer is said, with what that leaves unread: a tree a
    /// session holds then reads as idle, and no recovery ref is listed.
    /// </summary>
    internal static async Task<(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<string> Unread)> ReadAsync(string home)
    {
        var (units, _, unread) = await ReadStateAsync(home).ConfigureAwait(false);
        return (units, unread);
    }

    /// <summary><see cref="ReadAsync"/>, with Daoris's own recoveries no unit names (<see cref="WorkInventory.Recoveries"/>).</summary>
    internal static async Task<(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<InventoryEntry> Recovery, IReadOnlyList<string> Unread)> ReadStateAsync(string home)
    {
        var unread = new List<string>();
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<RecordedTree> recorded = [];
        IReadOnlySet<string> closed = new HashSet<string>();
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

            // Each tree joined to the session that recorded it, as the page's work reads it.
            recorded = WorkInventories.RecordedOf(await service.SessionRecordsAsync().ConfigureAwait(false));
            // Which quests the landings the line does not hold name are closed, read once and only where one asks.
            closed = await LandedStanding.ClosedAsync(new LandedBranches(home).Entries(), service.EveryQuestAsync, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or TaskCanceledException)
        {
            unread.Add($"the service did not answer ({error.Message}), so recovery refs were not read and a tree a session holds may read as idle");
        }

        var (units, recovery) = await WorkInventories.ReadAsync(home, roots, inUse, unread, recorded: recorded, closedQuests: closed).ConfigureAwait(false);
        return (units, recovery, unread);
    }

    /// <summary>
    /// <c>daoris work state [&lt;unit&gt;] [--entries]</c>: each unit's milestone, from the inventory the
    /// records derive; a unit named by its id, its session or its branch. A look: it changes nothing and tells no window.
    /// Exit 0 clean; 1 where the unit named is none; 2 for words not understood.
    /// </summary>
    public static async Task<int> MilestonesAsync(string[] args)
    {
        var entries = args.Contains("--entries");
        var words = args.Where(word => word != "--entries").ToList();
        if (words.Count > 1 || words.Any(word => word.StartsWith('-')))
        {
            Console.Error.WriteLine($"daoris: `work state` takes one unit and `--entries`: {string.Join(" ", args)}");
            Console.Error.WriteLine("usage: daoris work state [<unit>] [--entries]");
            return 2;
        }

        var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        var (units, recovery, unread) = await ReadStateAsync(home).ConfigureAwait(false);
        foreach (var line in unread) Console.Error.WriteLine($"work: {line}");
        if (words.Count == 1) units = WorkInventories.Named(units, words[0]);
        if (words.Count == 1 && units.Count == 0)
        {
            Console.Error.WriteLine($"work: `{words[0]}` names no unit of work on this machine.");
            return 1;
        }

        foreach (var line in WorkWords.All(units, entries || words.Count == 1)) Console.WriteLine(line);
        // The recoveries no unit names, each by its rule, after the units; a named unit's look says only that unit.
        if (words.Count == 0 && recovery.Count > 0)
        {
            Console.WriteLine();
            foreach (var line in WorkWords.Recovery(recovery)) Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// <c>daoris work recovery drop &lt;ref&gt;... --repository &lt;name&gt; | --unheld [--repository &lt;name&gt;] [--plan]</c>:
    /// the person's delete of the recovery refs nothing holds, the screen's <c>RECOVERY_DROP</c> being the other door. Only the
    /// lease's holder acts (<see cref="RecoveryDropping.WithoutHome"/>); <c>--plan</c> lists what it would do and changes nothing, and counts
    /// the refs no journal record names (<see cref="RecoveryCensus"/>), with their sum over every checkout read. Exit
    /// 0 clean; 1 where refused or a ref named is kept; 2 for words not understood or a service that does not answer.
    /// </summary>
    public static async Task<int> RecoveryDropAsync(string[] args)
    {
        if (RecoveryCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris: {problem}");
            Console.Error.WriteLine(RecoveryCommand.Usage);
            return 2;
        }

        var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        IReadOnlyList<(string Repository, string Root, string? Workspace)> checkouts;
        try
        {
            using var service = ServiceClient.FromEnvironment();
            checkouts = [.. (await service.RegistryAsync().ConfigureAwait(false))
                .Where(row => !string.IsNullOrWhiteSpace(row.Root) && Directory.Exists(row.Root)
                              && (ask.Repository is null || string.Equals(row.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase)))
                .Select(row => (row.Repository, row.Root!, (string?)row.Workspace)).DistinctBy(row => row.Repository, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or TaskCanceledException)
        {
            Console.Error.WriteLine($"daoris: the service did not answer ({error.Message}), so no checkout is known to delete in.");
            return 2;
        }

        if (checkouts.Count == 0)
        {
            Console.Error.WriteLine($"work: {(ask.Repository is { } named ? $"`{named}` has no checkout on this machine" : "no repository has a checkout on this machine")}, so no recovery ref is read.");
            return 1;
        }

        var kept = false;
        var dropped = 0;
        var (unrecorded, unread) = (0, 0);
        var config = DriverConfig.Load(DriverConfig.ResolvePath());
        foreach (var (repository, root, workspace) in checkouts)
        {
            var line = await RecoveryWorks.LineAsync(root, CanonicalLine.Choose(config, repository, workspace, guess: null).Branch, CancellationToken.None).ConfigureAwait(false);
            if (ask.Plan)
            {
                foreach (var said in await RecoveryDropping.PlanAsync(home, root, repository, line, CancellationToken.None).ConfigureAwait(false)) Console.WriteLine(said);
                var count = await RecoveryCensus.CountAsync(home, root, repository, CancellationToken.None).ConfigureAwait(false);
                (unrecorded, unread) = (unrecorded + (count.Unrecorded?.Count ?? 0), unread + (count.Unread is null ? 0 : 1));
                continue;
            }

            var done = await RecoveryDropping.PressAsync(
                home, root, repository, ask.Unheld ? null : [.. ask.Refs.Select(each => new RecoveryDrop(each, null))], CancellationToken.None, line).ConfigureAwait(false);
            foreach (var said in RecoveryDropping.Words(repository, done)) Console.WriteLine(said);
            dropped += done.Dropped.Count;
            kept |= done.Refused is not null || (!ask.Unheld && done.Skipped.Count > 0);
        }

        if (ask.Plan)
        {
            Console.WriteLine($"census: {unrecorded} recovery {(unrecorded == 1 ? "ref" : "refs")} no journal record names, over {checkouts.Count} {(checkouts.Count == 1 ? "checkout" : "checkouts")}"
                + (unread > 0 ? $", {unread} not read" : "") + (unrecorded == 0 && unread == 0 ? ": none is left for the old list and this press." : "."));
        }

        if (dropped > 0) PressesTold.Tell(home, PressesTold.RecoveryDrop);
        return kept ? 1 : 0;
    }

    public static async Task<int> RunAsync(WorkScope scope, string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (WorkCommand.Read(scope, args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris: {problem}");
            Console.Error.WriteLine(WorkCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        // The machine's remotes, re-read as every pass reads them: a workspace with none answers null, and its
        // declines travel nowhere.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
        var world = new WorkWorld(service, home, configPath, new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids)))
        {
            Unadopted = SetupWorld.Unadopted(config, home),
            Log = log,
            // The running Daoris's client where one answers, the person's presses confirmed in its window.
            Instance = InstanceHost.World(home, "daoris"),
            Sync = async (workspace, ct) => sync.Workspaces.Contains(RemoteTarget.Workspace(workspace), StringComparer.OrdinalIgnoreCase)
                ? await sync.RunOnceAsync(workspace, ct).ConfigureAwait(false)
                : null,
        };

        return await WorkCommand.RunAsync(ask, world, Console.Out).ConfigureAwait(false);
    }
}
