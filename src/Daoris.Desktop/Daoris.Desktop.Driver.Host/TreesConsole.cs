namespace Daoris.Driver.Host;

/// <summary>
/// The session trees, from a terminal: `daoris-driver trees [list | remove &lt;path&gt; [--force]]`.
/// </summary>
/// <remarks>
/// <para><b>Why here and not in the `daoris` CLI.</b> The tree lifecycle is git spawned against real
/// checkouts, and the CLI's discipline is that only `toolchain.ts` spawns anything — so the verbs live
/// on the binary that already owns git (D50's parity is between surfaces, not between packages). The
/// standing OPT-IN is the CLI's (`daoris driver trees &lt;repo&gt; on|off`), because that is a file
/// edit; the trees themselves are this side's, because they are processes and disk.</para>
///
/// <para>Exit codes keep the family contract: 0 clean · 1 refused (work would be lost) · 2 tool error.</para>
/// </remarks>
internal static class TreesConsole
{
    /// <summary>One branch of the clean-up's list, in words: whose, which, and what it holds.</summary>
    internal static string Describe(SweepItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        SweepKind.Empty => $"nothing beyond `{item.Where}`",
        SweepKind.Landed => $"landed{(item.Where is { } where ? $" on `{where}`" : "")}",
        SweepKind.Unlanded => item.Commits > 0
            ? $"{item.Commits} commit(s) no branch of yours holds"
            : $"unlanded — {item.Detail}",
        SweepKind.Dirty => $"its tree has uncommitted work ({item.Detail})",
        SweepKind.InUse => "a session still running or waiting holds its tree",
        _ => item.Kind,
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        var trees = new SessionTrees(home);

        switch (args)
        {
            case [] or ["list", ..]:
            {
                var listed = await trees.ListAsync().ConfigureAwait(false);
                if (listed.Count == 0)
                {
                    Console.WriteLine($"trees: none — nothing under {trees.TreesRoot}.");
                    Console.WriteLine("  `daoris driver trees <repository> on` opts a repository in;");
                    Console.WriteLine("  `daoris-driver chat --own-tree` opens one for a conversation.");
                    return 0;
                }

                foreach (var tree in listed)
                {
                    Console.WriteLine($"  {tree.Workspace}/{tree.Repository}  {tree.Branch}");
                    Console.WriteLine($"    {tree.Path}");
                }

                return 0;
            }

            case ["remove", var path, ..]:
            {
                var removal = await trees.RemoveAsync(path, force: args.Contains("--force"))
                    .ConfigureAwait(false);
                Console.WriteLine($"trees: {removal.Message}");
                // A refusal that preserves work is policy doing its job, not a tool error.
                return removal.Removed ? 0 : 1;
            }

            // The clean-up (WSR3, D88): the list first, and with --yes the press — the screen's Settings →
            // Workspace → Session branches is the other door (D50). The checkouts and the sessions in use
            // are the service's, so this one asks it.
            case ["clean", ..]:
            {
                using var service = ServiceClient.FromEnvironment();
                var repositories = (await service.RegistryAsync().ConfigureAwait(false))
                    .Where(row => !string.IsNullOrWhiteSpace(row.Root))
                    .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
                    .ToList();
                var inUse = (await service.ActiveSessionsAsync().ConfigureAwait(false))
                    .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!args.Contains("--yes"))
                {
                    var plan = await trees.SweepPlanAsync(repositories, inUse).ConfigureAwait(false);
                    if (plan.Count == 0)
                    {
                        Console.WriteLine("trees: no session branches in any repository with a checkout here.");
                        return 0;
                    }

                    foreach (var item in plan) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")} {Describe(item)}");
                    var going = plan.Count(item => item.Removable);
                    Console.WriteLine(going == 0
                        ? "trees: nothing to clean — every branch listed holds something of its own."
                        : $"trees: {going} branch(es) would go, with their trees. `daoris-driver trees clean --yes` removes them.");
                    return 0;
                }

                var results = await trees.SweepAsync(repositories, inUse).ConfigureAwait(false);
                foreach (var result in results)
                {
                    Console.WriteLine($"  {(result.Removed ? "removed" : "kept   ")} {Describe(result.Item)}"
                        + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
                }

                Console.WriteLine($"trees: removed {results.Count(result => result.Removed)} of {results.Count}.");
                return 0;
            }

            // Accepting a session's work (WSR1, D87): the review's Accept, from a terminal — D50's second
            // door, which the landing had not had. The workspace's rule decides the form (merged into the
            // line, or a branch for the person to push), and --plan says where it would go and does nothing.
            case ["land", var session, ..]:
            {
                using var service = ServiceClient.FromEnvironment();
                var (tree, _) = await service.SessionGroundAsync(session).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(tree))
                {
                    Console.Error.WriteLine($"trees: session `{session}` names no working tree on this machine, so there is nothing here to land.");
                    return 1;
                }

                var questId = await service.SessionQuestAsync(session).ConfigureAwait(false);
                var quest = questId is null ? null : await service.FindQuestAsync(questId).ConfigureAwait(false);
                var subject = new LandingSubject(
                    session, questId,
                    quest?.Title ?? new SessionEvents(Path.Combine(home, "sessions")).Openings([session]).GetValueOrDefault(session));

                if (args.Contains("--plan"))
                {
                    var plan = await trees.PlanAsync(tree, subject).ConfigureAwait(false);
                    Console.WriteLine($"trees: accepting `{session}` would {(plan.Form == "branch" ? "put its work on the branch" : "merge its work into")} `{plan.Target}` ({plan.Source}).");
                    return 0;
                }

                var landed = await trees.LandAsync(tree, subject).ConfigureAwait(false);
                Console.WriteLine($"trees: {landed.Message}");
                return landed.Landed ? 0 : 1;
            }

            default:
                Console.Error.WriteLine("usage: daoris-driver trees [list | remove <path> [--force] | clean [--yes] | land <session> [--plan]]");
                Console.Error.WriteLine("  A session's worktree (D51). Removal refuses while the tree holds");
                Console.Error.WriteLine("  uncommitted changes or work no branch of yours holds; --force means it.");
                Console.Error.WriteLine("  clean lists every session branch with what it holds; --yes removes those");
                Console.Error.WriteLine("  whose work is on a branch of yours, or that hold nothing (D88).");
                Console.Error.WriteLine("  land accepts a session's work as the review's Accept does, by the workspace's");
                Console.Error.WriteLine("  rule; --plan says where it would go and does nothing (D87).");
                return 2;
        }
    }
}
