using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree hand`: a branch a landing made, handed to a landing plugin afterwards.</summary>
internal static class TreesHandVerb
{
    /// <summary>
    /// The review's *hand it to*, from a terminal (D50). Named by the session that landed it or by the branch; the plugin is the one
    /// --plugin names, else the repository's landing rule's. --plan says what it would do.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string named, string home, SessionTrees trees, MachineLog? log)
    {
        var repository = Option(args, "--repository");
        var plugin = Option(args, "--plugin");
        // RPCOPEN1: the plan is the running Daoris's where one answers (HANDOFF_PLAN), in the alone path's words.
        if (args.Contains("--plan") && await InstanceDoor.TryReadAsync(
                OpenRoutes.HandoffPlan, new { branch = named, repository, plugin, said = true },
                TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } viaInstance)
        {
            return viaInstance;
        }

        var found = trees.Recorded.Find(named, repository);
        if (found.Count == 0)
        {
            Console.Error.WriteLine($"trees: {TreeHand.NotLanded(named)}");
            return 1;
        }

        // A session means its newest landing; a branch name in two repositories means the person says which.
        var repositories = found.Select(entry => entry.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (repositories.Count > 1)
        {
            Console.Error.WriteLine($"trees: {TreesWords.NamesSeveral(named, repositories)}");
            return 1;
        }

        var entry = found[0];
        using var service = ServiceClient.FromEnvironment();
        var root = (await service.RegistryAsync().ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase))?.Root;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            Console.Error.WriteLine($"trees: {TreesWords.HandNoCheckout(entry.Repository, entry.Branch)}");
            return 1;
        }

        // A plugin's own lines, said as they come, under its name — as `trees land` says them (D64 §4).
        var handing = new SessionTrees(home, new LandingPlugins(home, say: (id, line) => Console.WriteLine($"  plugin:{id}  {line}"), log: log));
        if (args.Contains("--plan"))
        {
            var plan = await handing.HandPlanAsync(root, entry, plugin).ConfigureAwait(false);
            var handPlan = TreesPlanWords.HandPlan(plan, []);
            handPlan.Print(Console.Out, Console.Error);
            return handPlan.Code;
        }

        // FABRIC-CLOSE1h8 (D173's note): judged again just before its plugin is told, on the result of the session that last
        // moved the branch, as the route's hand-off is.
        var pressed = await PressQuests.HandedAsync(entry, service).ConfigureAwait(false);
        using var waiting = SayWaiting(Console.WriteLine);
        var handed = await handing.HandAsync(root, entry, plugin, beforePush: pressed.BeforePush).ConfigureAwait(false);
        Told(home, PressesTold.HandOff, entry.Session);
        // The plugin's lines were said as they came; the hand-off's sentence and the exit are the route's words too (RPC1b3).
        var said = TreesWords.Hand(handed, []);
        said.Print(Console.Out, Console.Error);
        // Kept where the conversation is kept, as the review's press keeps it (D100).
        if (handed.Plugin is not null)
        {
            new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions))
                .Keep(entry.Session, LandingRules.HandNote(handed), line => Console.Error.WriteLine($"trees: {line}"));
        }

        // 1 where the branch was not pushed: refused, or the plugin's step did not complete.
        return said.Code;
    }
}
