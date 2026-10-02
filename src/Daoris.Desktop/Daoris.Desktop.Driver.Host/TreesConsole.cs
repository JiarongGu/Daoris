namespace Daoris.Driver.Host;

/// <summary>
/// The session trees, from a terminal: `daoris-driver trees [list | remove &lt;path&gt; [--force] | clean | land | hand | sync]`.
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

    /// <summary>What accepting a session would do, in one line — who pushes it where a plugin does, and what would refuse it (D100).</summary>
    internal static string Planned(string session, LandingPlan plan)
    {
        var line = $"trees: accepting `{session}` would {(plan.Form == LandingForm.Branch ? "put its work on the branch" : "merge its work into")} `{plan.Target}` ({plan.Source})";
        if (plan.Plugin is { } plugin) line += $", then plugin `{plugin}` would push it and open the pull request";
        line += ".";
        return plan.Problem is { } problem ? $"{line} It would be refused now: {problem}" : line;
    }

    /// <summary>
    /// The lines this press moved, registered from the line at once (WSSETUP5, D124 §3.1) rather than at a loop's next
    /// look, so a terminal's landing reads as registered when it returns; each outcome to this host's log. What changed or
    /// must be fixed is said. A service that did not answer leaves them due, for the next loop to follow.
    /// </summary>
    internal static async Task FollowMovedLinesAsync(ServiceClient service, string home, MachineLog? log)
    {
        var due = RegistryFollowing.Due(home);
        if (due.Count == 0) return;
        if (log is not null) service.RegistryFollowed += followed => SessionLog.WriteFollowed(log, followed);
        try
        {
            var config = DriverConfig.Load(DriverConfig.ResolvePath());
            var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config), due).ConfigureAwait(false);
            foreach (var line in report.Followed.Select(RegistrationFollow.EventLine).OfType<string>()) Console.WriteLine($"  {line}");
            if (report.Refresh is { } refresh) Console.WriteLine($"  registry  the index was not read again after registering: {refresh}");
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"trees: the registry did not follow the lines this moved ({error.Message}); the driver follows them at its next look.");
        }
    }

    /// <param name="log">This host's machine log: a landing's and a hand-off's plugin frame is written there (PLUGUI1d).</param>
    public static async Task<int> RunAsync(string[] args, MachineLog? log = null)
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
                    var plan = await trees.CleanPlanAsync(repositories, inUse).ConfigureAwait(false);
                    if (plan.Sessions.Count == 0 && plan.Landed.Count == 0)
                    {
                        Console.WriteLine("trees: no session branches, and no branch a landing made, in any repository with a checkout here.");
                        return 0;
                    }

                    foreach (var item in plan.Sessions) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")} {Describe(item)}");
                    // The branches landings made (WSR5), in a group of their own: each goes where its work reached the line.
                    if (plan.Landed.Count > 0) Console.WriteLine("  landed branches — each goes once its work reads on the line:");
                    foreach (var item in plan.Landed) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")} {LandedWords.Describe(item)}");
                    var going = plan.Sessions.Count(item => item.Removable) + plan.Landed.Count(item => item.Removable);
                    Console.WriteLine(going == 0
                        ? "trees: nothing to clean — every branch listed holds something of its own."
                        : $"trees: {going} branch(es) would go, a session's with its tree. `daoris-driver trees clean --yes` removes them.");
                    return 0;
                }

                var done = await trees.CleanAsync(repositories, inUse).ConfigureAwait(false);
                foreach (var result in done.Sessions)
                {
                    Console.WriteLine($"  {(result.Removed ? "removed" : "kept   ")} {Describe(result.Item)}"
                        + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
                }

                foreach (var result in done.Landed)
                {
                    Console.WriteLine($"  {(result.Removed ? "removed" : "kept   ")} {LandedWords.Describe(result.Item)}"
                        + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
                }

                // Folders trees left behind where something held them open, tried again.
                foreach (var folder in done.Folders ?? []) Console.WriteLine($"  {folder}");
                var total = done.Sessions.Count + done.Landed.Count;
                Console.WriteLine($"trees: removed {done.Sessions.Count(result => result.Removed) + done.Landed.Count(result => result.Removed)} of {total}.");
                return 0;
            }

            // Bringing repositories up to date after a pull request merged (WSR6, D109): the list first — it fetches
            // each line, which moves only origin's refs — and with --yes the press. Settings → Workspace → Session
            // branches is the other door (D50). The checkouts and the sessions in use are the service's. It takes the
            // repositories holding Daoris's branches (D112): --all takes every one, and a repository named is taken.
            case ["sync", ..]:
            {
                var named = Option(args, "--repository");
                var scope = args.Contains("--all") ? SyncScope.Everything
                    : named is null ? SyncScope.Held
                    : SyncScope.Named([named]);
                using var service = ServiceClient.FromEnvironment();
                var repositories = (await service.RegistryAsync().ConfigureAwait(false))
                    .Where(row => !string.IsNullOrWhiteSpace(row.Root))
                    .Where(row => named is null || string.Equals(row.Repository, named, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(row => row.Repository, StringComparer.Ordinal)
                    .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
                    .ToList();
                if (repositories.Count == 0)
                {
                    Console.Error.WriteLine(named is null
                        ? "trees: no repository has a checkout here, so there is nothing to bring up to date."
                        : $"trees: `{named}` has no checkout here, so there is nothing to bring up to date.");
                    return 1;
                }

                var inUse = (await service.ActiveSessionsAsync().ConfigureAwait(false))
                    .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!args.Contains("--yes"))
                {
                    var plan = await trees.SyncPlanAsync(repositories, inUse, fetch: true, scope: scope).ConfigureAwait(false);
                    // What was not fetched, said once and first (WSR7), rather than at the end of every row.
                    foreach (var line in SyncWords.NotFetched(plan.Lines, DateTimeOffset.UtcNow)) Console.WriteLine(line);
                    foreach (var pull in plan.Lines) Console.WriteLine($"  {(pull.Moves ? "moves" : "stays")}  {SyncWords.Describe(pull)}");
                    foreach (var item in plan.Rebases) Console.WriteLine($"  {(item.Replays ? "moves" : "stays")}  {SyncWords.Describe(item)}");
                    if (plan.Deletes.Count > 0) Console.WriteLine("  landed branches whose work reached the line:");
                    foreach (var item in plan.Deletes) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")}  {LandedWords.Describe(item)}");
                    if (SyncWords.Apart(plan.Apart) is { } apart) Console.WriteLine(apart);
                    var acts = plan.Lines.Count(pull => pull.Moves) + plan.Rebases.Count(item => item.Replays) + plan.Deletes.Count(item => item.Removable);
                    Console.WriteLine(plan.Looked.Count == 0 ? "trees: no repository with a checkout here holds a branch of Daoris's."
                        : acts == 0 ? "trees: everything here is up to date."
                        : $"trees: {acts} thing(s) would change. `daoris-driver trees sync{Carried(args, named)} --yes` does them — Daoris fetches, and never pushes.");
                    return 0;
                }

                // The sessions in use, asked again while each repository's trees are held for its replays (LEFT2).
                var done = await trees.SyncAsync(repositories, inUse, only: null, fetch: true,
                    inUseNow: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase), scope: scope).ConfigureAwait(false);
                foreach (var line in SyncWords.NotFetched([.. done.Lines.Select(result => result.Pull)], DateTimeOffset.UtcNow)) Console.WriteLine(line);
                foreach (var result in done.Lines) Console.WriteLine($"  {(result.Moved ? "moved " : "stayed")}  {result.Pull.Repository}  {result.Message}");
                foreach (var result in done.Rebases) Console.WriteLine($"  {(result.Replayed ? "moved " : "stayed")}  {result.Item.Repository}  {result.Message}");
                foreach (var result in done.Deletes)
                {
                    Console.WriteLine($"  {(result.Removed ? "removed" : "kept   ")} {LandedWords.Describe(result.Item)}"
                        + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
                }

                if (SyncWords.Apart(done.Apart) is { } untouched) Console.WriteLine(untouched);
                await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);

                // 1 where something the proofs cleared did not happen: a conflict, or a branch or line that moved since.
                var missed = done.Lines.Count(result => result.Pull.Moves && !result.Moved)
                             + done.Rebases.Count(result => result.Item.Replays && !result.Replayed)
                             + done.Deletes.Count(result => result.Item.Removable && !result.Removed);
                Console.WriteLine($"trees: {done.Lines.Count(r => r.Moved)} line(s) moved, {done.Rebases.Count(r => r.Replayed)} branch(es) replayed, "
                    + $"{done.Deletes.Count(r => r.Removed)} removed." + (missed > 0 ? $" {missed} did not happen, each said above." : ""));
                return missed > 0 ? 1 : 0;
            }

            // Accepting a session's work (WSR1, D87): the review's Accept, from a terminal — D50's second
            // door, which the landing had not had. The workspace's rule decides the form (merged into the
            // line, or a branch for the person to push, or for the rule's plugin to push — D100), and --plan
            // says where it would go and does nothing.
            case ["land", var session, ..]:
            {
                using var service = ServiceClient.FromEnvironment();
                var (tree, _) = await service.SessionGroundAsync(session).ConfigureAwait(false);

                // A session whose review reads as landed (REVIEW2, D113) — its landed branch stands, or its tree is gone —
                // is said as the review says it, and lands nothing again: the review offers no Accept there at all.
                var landedBefore = trees.Recorded.Landing(session);
                LandedReview? before = null;
                var gone = SessionTrees.TreeGone(tree);
                if (landedBefore is not null)
                {
                    var checkout = (await service.RegistryAsync().ConfigureAwait(false))
                        .FirstOrDefault(row => string.Equals(row.Repository, landedBefore.Repository, StringComparison.OrdinalIgnoreCase))?.Root;
                    before = await trees.LandedReviewAsync(
                        string.IsNullOrWhiteSpace(checkout) || !Directory.Exists(checkout) ? null : checkout, landedBefore, changes: false).ConfigureAwait(false);
                    if (before.ReadsAsLanded(gone))
                    {
                        if (args.Contains("--plan"))
                        {
                            Console.WriteLine($"trees: {LandedReviewWords.Describe(session, before)}");
                            return 0;
                        }

                        Console.WriteLine($"trees: {LandedReviewWords.NotAgain(session, before, gone)}");
                        return 1;
                    }
                }

                if (string.IsNullOrWhiteSpace(tree))
                {
                    Console.Error.WriteLine($"trees: session `{session}` names no working tree on this machine, so there is nothing here to land.");
                    return 1;
                }

                var questId = await service.SessionQuestAsync(session).ConfigureAwait(false);
                var events = new SessionEvents(Path.Combine(home, "sessions"));
                // Named for the chain's first quest (WSR5), as the review's press names it.
                var subject = await LandingRules.SubjectAsync(
                    session, questId, quest => service.FindQuestAsync(quest),
                    events.Openings([session]).GetValueOrDefault(session)).ConfigureAwait(false);
                // A plugin's own lines, said as they come, under its name — as the console says them (D64 §4).
                var landing = new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => Console.WriteLine($"  plugin:{plugin}  {line}"), log: log));

                if (args.Contains("--plan"))
                {
                    var plan = await landing.PlanAsync(tree, subject).ConfigureAwait(false);
                    Console.WriteLine(Planned(session, plan));
                    // Landed before, its branch gone since, its tree still here: said, as the review's note says it.
                    if (before is not null) Console.WriteLine($"trees: {LandedReviewWords.Describe(session, before)}");
                    return 0;
                }

                var landed = await landing.LandAsync(tree, subject).ConfigureAwait(false);
                Console.WriteLine($"trees: {landed.Message}");
                // Kept where the conversation is kept, as the review's press keeps it (D100).
                if (landed.Landed) events.Keep(session, LandingRules.Note(landed), line => Console.Error.WriteLine($"trees: {line}"));
                await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
                // 1 where the step the rule asked for did not happen: refused, or the plugin did not push.
                return landed.Landed && landed.Plugin is not { Failed: true } and not { Pushed: false } ? 0 : 1;
            }

            // A branch a landing made, handed to a landing plugin afterwards (WSR5b): the review's *hand it to*,
            // from a terminal (D50). Named by the session that landed it or by the branch; the plugin is the
            // one --plugin names, else the repository's landing rule's. --plan says what it would do.
            case ["hand", var named, ..]:
            {
                var repository = Option(args, "--repository");
                var plugin = Option(args, "--plugin");
                var found = trees.Recorded.Find(named, repository);
                if (found.Count == 0)
                {
                    Console.Error.WriteLine($"trees: {SessionTrees.NotLanded(named)}");
                    return 1;
                }

                // A session means its newest landing; a branch name in two repositories means the person says which.
                var repositories = found.Select(entry => entry.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (repositories.Count > 1)
                {
                    Console.Error.WriteLine($"trees: `{named}` names a landed branch in {string.Join(", ", repositories.Select(r => $"`{r}`"))} — "
                        + "say which with `--repository <name>`.");
                    return 1;
                }

                var entry = found[0];
                using var service = ServiceClient.FromEnvironment();
                var root = (await service.RegistryAsync().ConfigureAwait(false))
                    .FirstOrDefault(row => string.Equals(row.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase))?.Root;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    Console.Error.WriteLine($"trees: `{entry.Repository}` has no checkout on this machine, so there is no `{entry.Branch}` here to hand on.");
                    return 1;
                }

                // A plugin's own lines, said as they come, under its name — as `trees land` says them (D64 §4).
                var handing = new SessionTrees(home, new LandingPlugins(home, say: (id, line) => Console.WriteLine($"  plugin:{id}  {line}"), log: log));
                if (args.Contains("--plan"))
                {
                    var plan = await handing.HandPlanAsync(root, entry, plugin).ConfigureAwait(false);
                    Console.WriteLine(HandPlanned(plan));
                    return 0;
                }

                var handed = await handing.HandAsync(root, entry, plugin).ConfigureAwait(false);
                Console.WriteLine($"trees: {handed.Message}");
                // Kept where the conversation is kept, as the review's press keeps it (D100).
                if (handed.Plugin is not null)
                {
                    new SessionEvents(Path.Combine(home, "sessions"))
                        .Keep(entry.Session, LandingRules.HandNote(handed), line => Console.Error.WriteLine($"trees: {line}"));
                }

                // 1 where the branch was not pushed: refused, or the plugin's step did not complete.
                return handed.Handed ? 0 : 1;
            }

            default:
                Console.Error.WriteLine("usage: daoris-driver trees [list | remove <path> [--force] | clean [--yes] | land <session> [--plan]");
                Console.Error.WriteLine("                           | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]");
                Console.Error.WriteLine("                           | sync [--repository <name>] [--all] [--yes]]");
                Console.Error.WriteLine("  A session's worktree (D51). Removal refuses while the tree holds");
                Console.Error.WriteLine("  uncommitted changes or work no branch of yours holds; --force means it.");
                Console.Error.WriteLine("  clean lists every session branch with what it holds; --yes removes those");
                Console.Error.WriteLine("  whose work is on a branch of yours, or that hold nothing (D88), and every");
                Console.Error.WriteLine("  branch a landing made whose files read on the line as it left them (WSR5).");
                Console.Error.WriteLine("  land accepts a session's work as the review's Accept does, by the workspace's");
                Console.Error.WriteLine("  rule; --plan says where it would go and does nothing (D87). A rule naming a");
                Console.Error.WriteLine("  plugin hands the branch to it to push and open the pull request (D100). A session");
                Console.Error.WriteLine("  that already landed, while its branch stands or once its tree is gone, is said");
                Console.Error.WriteLine("  as its review says it, and lands nothing again (D113).");
                Console.Error.WriteLine("  hand gives a branch a landing made to a landing plugin afterwards — the one");
                Console.Error.WriteLine("  --plugin names, else the rule's — to push and open the pull request (WSR5).");
                Console.Error.WriteLine("  sync brings each repository up to date after a pull request merged: it fetches");
                Console.Error.WriteLine("  the line and fast-forwards it, replays the branches still at work onto it (only");
                Console.Error.WriteLine("  their own commits), and deletes the landed branches whose work reached it. It");
                Console.Error.WriteLine("  lists first; --yes does it. Daoris fetches, and never pushes (WSR6). It takes");
                Console.Error.WriteLine("  the repositories holding Daoris's branches and names the rest; --all takes");
                Console.Error.WriteLine("  every one, and --repository the one named (D112).");
                return 2;
        }
    }

    /// <summary>What handing a landed branch on would do, in one line — and what would refuse it (WSR5b).</summary>
    internal static string HandPlanned(HandPlan plan)
    {
        var line = plan.Plugin is { } plugin
            ? $"trees: handing on `{plan.Branch}` ({plan.Repository}) would give plugin `{plugin}` its {plan.Commits} commit(s) to push and open the pull request for."
            : $"trees: `{plan.Branch}` ({plan.Repository}) has no plugin named to hand it to.";
        if (plan.PullRequest is { } pr) line += $" Its last pull request: {pr}";
        return plan.Problem is { } problem ? $"{line} It would be refused now: {problem}" : line;
    }

    /// <summary>The list's own scope, said again in the press it suggests (D112), so `--yes` takes what was listed.</summary>
    private static string Carried(string[] args, string? named) =>
        (named is null ? "" : $" --repository {named}") + (args.Contains("--all") ? " --all" : "");

    /// <summary>The word after <paramref name="name"/>, or null where it is absent or ends the line.</summary>
    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
    }
}
