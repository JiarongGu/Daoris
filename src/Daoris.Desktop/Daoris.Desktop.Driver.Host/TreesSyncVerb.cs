using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree sync`: bringing repositories up to date after a pull request merged, the list and with --yes the press.</summary>
internal static class TreesSyncVerb
{
    /// <summary>
    /// The list first, which fetches each line and moves only origin's refs, and with --yes the press. A workspace's Branches tab is
    /// the other door, and --workspace takes that workspace's checkouts alone, as the tab does. It takes the repositories holding
    /// Daoris's branches: --all takes every one, and a repository named is taken. The press is the running Daoris's <c>TREES_SYNC</c>
    /// where one answers, confirmed in its window, which fetches and has the registry follow the lines it moved as this verb does;
    /// alone where none runs.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string home, SessionTrees trees, SessionTrees asking, MachineLog? log)
    {
        if (TreesCommand.Read(args, out var problem) is not { } ask) return Usage(problem);
        if (!ask.Yes) return await AloneAsync(ask, home, trees, asking, log).ConfigureAwait(false);

        var payload = new { repository = ask.Repository, workspace = ask.Workspace, all = ask.All, fetch = true, follow = true };
        return await InstanceDoor.PressAsync(TreesRoutes.Sync, payload, () => AloneAsync(ask, home, trees, asking, log),
            answer => TreesSaid.ReadWords(Console.Out, Console.Error)(answer) ?? 2, InstanceHost.World(home, "trees")).ConfigureAwait(false);
    }

    private static async Task<int> AloneAsync(TreesAsk ask, string home, SessionTrees trees, SessionTrees asking, MachineLog? log)
    {
        var scope = TreesCommand.Scope(ask);
        using var service = ServiceClient.FromEnvironment();
        var taken = TreesCommand.Take(await service.RegistryAsync().ConfigureAwait(false), ask);
        if (taken.Refusal is { } refusal)
        {
            Console.Error.WriteLine($"trees: {refusal}");
            return 1;
        }

        var repositories = taken.Repositories;
        if (repositories.Count == 0)
        {
            Console.Error.WriteLine(TreesCommand.NothingToSync(ask));
            return 1;
        }

        var inUse = await BranchTidying.HeldTreesAsync(home, service, CancellationToken.None).ConfigureAwait(false);

        if (!ask.Yes)
        {
            // The list is the running Daoris's where one answers (TREES_SYNC_PLAN: its fetch and its plugin asks, as the
            // person's look, which this verb is), in the words the alone path prints from the same function.
            if (await InstanceDoor.TryReadAsync(
                    OpenRoutes.TreesSyncPlan, new { repository = ask.Repository, workspace = ask.Workspace, all = ask.All, said = true },
                    TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } listed)
            {
                return listed;
            }

            // Its fetch first, then its plugin asks where git cannot tell.
            var plan = await asking.SyncPlanAsync(repositories, inUse, fetch: true, scope: scope).ConfigureAwait(false);
            // What is not fetched, said once and first, rather than at the end of every row.
            var listing = TreesPlanWords.SyncPlan(plan, ask, DateTimeOffset.UtcNow);
            listing.Print(Console.Out, Console.Error);
            return listing.Code;
        }

        // The sessions in use, asked again while each repository's trees are held for its replays.
        var done = await trees.SyncAsync(repositories, inUse, only: null, fetch: true,
            inUseNow: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase), scope: scope,
            beside: token => BranchTidying.BesideAsync(home, service, token)).ConfigureAwait(false);
        Told(home, PressesTold.TreesSync);
        // The words are the route's too: what is not fetched, each row, what is left apart, the registry's
        // following of the lines moved, and the count, 1 where something the proofs cleared did not happen.
        var followed = await TreesWords.FollowMovedAsync(service, home, () => DriverConfig.Load(DriverConfig.ResolvePath()), log).ConfigureAwait(false);
        var said = TreesWords.Sync(done, DateTimeOffset.UtcNow, followed);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }
}
