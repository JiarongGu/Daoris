using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree land --entry|--ask`: a line of work's one Accept, or an ask's.</summary>
internal static class TreesEntryLandVerb
{
    /// <summary>
    /// A line of work's one Accept, or an ask's (ACCEPTALL1e, D167's amendment, D50): the page's `LAND_ENTRY` and `LAND_ASK` from a
    /// terminal, over the library's one code (`EntryAccepts`); --plan says where the parts would land and does nothing.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string home, string configPath, MachineLog? log)
    {
        if (TreesCommand.ReadEntryLand(args, out var problem) is not { } ask) return Usage(problem);
        // RPCOPEN1: the plan is the running Daoris's where one answers (ENTRY_LANDING, ASK_LANDING), in the alone path's words.
        if (ask.Plan && await InstanceDoor.TryReadAsync(
                ask.IsAsk ? OpenRoutes.AskLanding : OpenRoutes.EntryLanding,
                ask.IsAsk ? new { ask = ask.Id, said = true } : (object)new { entry = ask.Id, said = true },
                TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } viaInstance)
        {
            return viaInstance;
        }

        using var service = ServiceClient.FromEnvironment();
        var config = DriverConfig.Load(configPath);
        var accepts = new EntryAccepts(
            service, home,
            new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => Console.WriteLine($"  plugin:{plugin}  {line}"), log: log)),
            new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));
        var (found, rows) = await SessionEntries.LookAsync(
            service, config, SessionsConsole.Unadopted(config), lastLook: null, waits: null, home,
            ask.IsAsk ? entry => entry.Origin == $"ask:{ask.Id}" : entry => entry.Key == ask.Id, default).ConfigureAwait(false);
        // An ask's are its own entries, one per repository, with work to land; an entry's is the one named.
        var entries = ask.IsAsk
            ? [.. EntryAccepts.EntriesOf(ask.Id, found).Where(entry => EntryAccepts.PartsOf(entry, rows).Count > 0)]
            : found.Take(1).ToList();
        if (entries.Count == 0)
        {
            var none = TreesWords.EntryNone(ask.IsAsk, ask.Id);
            none.Print(Console.Out, Console.Error);
            return none.Code;
        }

        if (ask.Plan)
        {
            foreach (var entry in entries)
            {
                var (plan, held) = await accepts.PlanAsync(entry, rows, default).ConfigureAwait(false);
                foreach (var line in TreesPlanWords.EntryPlanned(entry, plan, held)) Console.WriteLine(line);
            }

            return 0;
        }

        var landings = new List<(string Repository, EntryLanding Landed)>();
        foreach (var entry in entries)
        {
            // One repository's refusal, said or thrown, holds only its own entry; the next is landed all the same (point 4).
            try
            {
                landings.Add((entry.Repository, await accepts.LandAsync(entry, rows, ask.LeaveOut, default).ConfigureAwait(false)));
            }
            catch (DriverException error)
            {
                landings.Add((entry.Repository, TreesWords.Unlanded(error.Message)));
            }
        }

        Told(home, ask.IsAsk ? PressesTold.LandAsk : PressesTold.LandEntry);
        // The plugins' lines and the notes were said as they came; the sentences and the exit are the routes' words too (RPC1b4).
        var said = TreesWords.EntryLand(landings, []);
        said.Print(Console.Out, Console.Error);
        await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
        return said.Code;
    }
}
