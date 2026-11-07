using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree clean`: the clean-up's list, and with --yes the press.</summary>
internal static class TreesCleanVerb
{
    /// <summary>
    /// The list first, and with --yes the press — a workspace's Branches tab is the other door, and --workspace takes that
    /// workspace's checkouts alone, as the tab does. The checkouts and the sessions in use are the service's, so this one
    /// asks it, once its words read.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string home, SessionTrees trees, SessionTrees asking)
    {
        if (TreesCommand.Read(args, out var problem) is not { } ask) return Usage(problem);
        using var service = ServiceClient.FromEnvironment();
        var taken = TreesCommand.Take(await service.RegistryAsync().ConfigureAwait(false), ask);
        if (taken.Refusal is { } refusal)
        {
            Console.Error.WriteLine($"trees: {refusal}");
            return 1;
        }

        var repositories = taken.Repositories;
        // Every session of a quest judges its tree, a retry waiting on a limit among them.
        var inUse = await BranchTidying.HeldTreesAsync(service, CancellationToken.None).ConfigureAwait(false);

        if (!ask.Yes)
        {
            // The list is the running Daoris's where one answers (SWEEP_PLAN: its git fan-out is asked once for every
            // terminal and window, which ask the pull-request plugins as the person's occasion, here as ever: this verb
            // is still that person's look), in the words the alone path prints from the same function.
            if (await InstanceDoor.TryReadAsync(OpenRoutes.SweepPlan, new { workspace = ask.Workspace, said = true },
                    TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } listed)
            {
                return listed;
            }

            // The quests the landings name that the service says are closed, read once for the list.
            var closed = await LandedStanding.ClosedAsync(new LandedBranches(home).Entries(), service.EveryQuestAsync, CancellationToken.None).ConfigureAwait(false);
            var plan = await asking.CleanPlanAsync(repositories, inUse, closedQuests: closed).ConfigureAwait(false);
            // The session each kept branch's tree is, so its landing is offered beside the discard, as on its page.
            var records = plan.Sessions.Any(item => item.Kind == SweepKind.Unlanded && item.Tree is not null)
                ? await SessionBranchDoors.RecordsOrNoneAsync(service).ConfigureAwait(false)
                : [];
            var listing = TreesPlanWords.CleanPlan(plan, records, ask);
            listing.Print(Console.Out, Console.Error);
            return listing.Code;
        }

        var done = await trees.CleanAsync(repositories, inUse).ConfigureAwait(false);
        Told(home, PressesTold.Sweep);
        // The words are the route's too (RPC1b3): each removal or keeping, the folders left, the count.
        var said = TreesWords.Clean(done);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }
}
