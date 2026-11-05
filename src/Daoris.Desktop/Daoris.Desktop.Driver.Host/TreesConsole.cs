namespace Daoris.Driver.Host;

/// <summary>
/// The session trees, from a terminal: `daoris tree [list | remove &lt;path&gt; [--force] | clean | land | hand | state | sync]`.
/// </summary>
/// <remarks>
/// <para><b>Why here and not in the `daoris` CLI.</b> The tree lifecycle is git spawned against real
/// checkouts, and the CLI's discipline is that it spawns nothing but this program (`driverprogram.ts`) — so the verbs live
/// on the binary that already owns git (D50's parity is between surfaces, not between packages). The
/// standing OPT-IN is the CLI's (`daoris place trees &lt;repo&gt; on|off`), because that is a file
/// edit; the trees themselves are this side's, because they are processes and disk.</para>
///
/// <para>Exit codes keep the family contract: 0 clean · 1 refused (work would be lost) · 2 tool error.</para>
/// </remarks>
internal static class TreesConsole
{
    /// <summary>
    /// What a press changed, told to the open windows (STATEHUB1e, D169 point 8): this process has no bus of the desktop's, so it leaves
    /// a note in the request folder, and the shell's loop publishes the change the window's press of <paramref name="route"/> declares
    /// at its next look. Told once the press has answered, whatever it answered; never a failure of the press.
    /// </summary>
    internal static void Told(string home, string route, string? session = null) =>
        new PressesTold(home).Tell(new PressTold(route, session, DateTimeOffset.UtcNow));

    /// <summary>A tree named by its path, as `remove` always took one, rather than a session or its branch (LAND3).</summary>
    internal static bool LooksLikePath(string named) => SessionBranchDoors.NamesAPath(named);

    /// <summary>The service this terminal finds, or none where it finds none: a verb that can judge from the files alone goes on.</summary>
    internal static ServiceClient? ServiceOrNone()
    {
        try
        {
            return ServiceClient.FromEnvironment();
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>What a `trees land` or `trees hand` says once before it waits behind another landing or push (LANDHOLD2).</summary>
    internal const string WaitingBehind = "trees: waiting for another landing of this work to end…";

    /// <summary>
    /// What a `trees land` or `trees hand` says while it waits (LANDHOLD2): naming the session and branch it waits behind where that
    /// one said who it is (LANDHOLD3), else <see cref="WaitingBehind"/>.
    /// </summary>
    internal static string WaitingLine(LandingHolder? behind) => behind is null
        ? WaitingBehind
        : $"trees: waiting for session {behind.Session}'s landing{(behind.Branch is { Length: > 0 } branch ? $" of `{branch}`" : "")} to end…";

    /// <summary>
    /// Says <see cref="WaitingLine"/> to <paramref name="write"/> once a wait begins, and again only where it comes to name a holder it
    /// had not named, for the landings and hand-offs this call makes, until disposed.
    /// </summary>
    internal static IDisposable SayWaiting(Action<string> write)
    {
        var said = false;
        LandingHolder? named = null;
        return LandingHoldNotice.Told((now, behind) =>
        {
            if (!now || (said && (behind is null || behind == named))) return;
            said = true;
            named = behind ?? named;
            write(WaitingLine(behind));
        });
    }

    /// <summary>
    /// The lines this press moved, registered from the line at once (WSSETUP5, D124 §3.1) rather than at a loop's next
    /// look, so a terminal's landing reads as registered when it returns; each outcome to this host's log. What changed or
    /// must be fixed is said. A service that did not answer leaves them due, for the next loop to follow.
    /// </summary>
    internal static async Task FollowMovedLinesAsync(ServiceClient service, string home, MachineLog? log)
    {
        var followed = await TreesWords.FollowMovedAsync(service, home, () => DriverConfig.Load(DriverConfig.ResolvePath()), log).ConfigureAwait(false);
        foreach (var line in followed.Out) Console.WriteLine(line);
        foreach (var line in followed.Error) Console.Error.WriteLine(line);
    }

    /// <param name="log">This host's machine log: a landing's and a hand-off's plugin frame is written there (PLUGUI1d).</param>
    public static async Task<int> RunAsync(string[] args, MachineLog? log = null)
    {
        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        var trees = new SessionTrees(home);
        // The looks that may ask a plugin about a pull request (PLUGHOOK1a/1c): its lines said under its name and its frames written
        // to the machine log, as a landing's are (D64 §4, PLUGUI1d).
        var asking = new SessionTrees(home, new LandingPlugins(home, say: (id, line) => Console.WriteLine($"  plugin:{id}  {line}"), log: log));

        switch (args)
        {
            case [] or ["list", ..]:
            {
                var listed = await trees.ListAsync().ConfigureAwait(false);
                if (listed.Count == 0)
                {
                    Console.WriteLine($"trees: none — nothing under {trees.TreesRoot}.");
                    Console.WriteLine("  `daoris place trees <repository> on` opts a repository in;");
                    Console.WriteLine("  `daoris chat --own-tree` opens one for a conversation.");
                    return 0;
                }

                // KEEP1a: the days are the one effective policy's, as the purge reads them.
                var days = KeepPolicy.Of(home).SetAside.Days;
                foreach (var tree in listed)
                {
                    // AUTOTIDY2c: a folder the look moved aside is listed so, with when, until the purge deletes it.
                    Console.WriteLine(tree.MovedAside is { } moved
                        ? $"  {tree.Workspace}/{tree.Repository}  {tree.Branch} on {moved.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}, "
                          + $"deleted once it is left untouched for {days.TotalDays:0} days"
                          + (tree.Slimmed.Count == 0 ? "" : $"; its package folders went an hour after it moved: {string.Join(", ", tree.Slimmed)}")
                        : $"  {tree.Workspace}/{tree.Repository}  {tree.Branch}");
                    Console.WriteLine($"    {tree.Path}");
                }

                return 0;
            }

            case ["remove", var named, ..]:
                return await TreesRemoveVerb.RunAsync(args, named, home, trees).ConfigureAwait(false);

            case ["clean", ..]:
                return await TreesCleanVerb.RunAsync(args, home, trees, asking).ConfigureAwait(false);

            // Bringing repositories up to date after a pull request merged (WSR6, D109): the list first — it fetches
            // each line, which moves only origin's refs — and with --yes the press. A workspace's Branches tab is the
            // other door (D50), and --workspace takes that workspace's checkouts alone, as the tab does (BRSCOPE1a). The
            // checkouts and the sessions in use are the service's. It takes the repositories holding Daoris's branches
            // (D112): --all takes every one, and a repository named is taken.
            case ["sync", ..]:
            {
                if (TreesCommand.Read(args, out var problem) is not { } ask) return Usage(problem);
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

                var inUse = await BranchTidying.HeldTreesAsync(service, CancellationToken.None).ConfigureAwait(false);

                if (!ask.Yes)
                {
                    // RPCOPEN1: the list is the running Daoris's where one answers (TREES_SYNC_PLAN: its fetch and its plugin asks, as the
                    // person's look, D148, which this verb still is), in the words the alone path prints from the same function.
                    if (await InstanceDoor.TryReadAsync(
                            OpenRoutes.TreesSyncPlan, new { repository = ask.Repository, workspace = ask.Workspace, all = ask.All, said = true },
                            TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } listed)
                    {
                        return listed;
                    }

                    // Its fetch first, then its plugin asks where git cannot tell (PLUGHOOK1c, design §2.1 occasion 3).
                    var plan = await asking.SyncPlanAsync(repositories, inUse, fetch: true, scope: scope).ConfigureAwait(false);
                    // What was not fetched, said once and first (WSR7), rather than at the end of every row.
                    var listing = TreesPlanWords.SyncPlan(plan, ask, DateTimeOffset.UtcNow);
                    listing.Print(Console.Out, Console.Error);
                    return listing.Code;
                }

                // The sessions in use, asked again while each repository's trees are held for its replays (LEFT2).
                var done = await trees.SyncAsync(repositories, inUse, only: null, fetch: true,
                    inUseNow: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase), scope: scope,
                    beside: token => BranchTidying.BesideAsync(service, token)).ConfigureAwait(false);
                Told(home, PressesTold.TreesSync);
                // The words are the route's too (RPC1b3): what was not fetched, each row, what was left apart, the registry's
                // following of the lines moved, and the count, 1 where something the proofs cleared did not happen.
                var followed = await TreesWords.FollowMovedAsync(service, home, () => DriverConfig.Load(DriverConfig.ResolvePath()), log).ConfigureAwait(false);
                var said = TreesWords.Sync(done, DateTimeOffset.UtcNow, followed);
                said.Print(Console.Out, Console.Error);
                return said.Code;
            }

            case ["land", "--entry" or "--ask", ..]:
                return await TreesEntryLandVerb.RunAsync(args, home, configPath, log).ConfigureAwait(false);

            // Accepting a session's work (WSR1, D87): the review's Accept, from a terminal — D50's second
            // door, which the landing had not had. The workspace's rule decides the form (merged into the
            // line, or a branch for the person to push, or for the rule's plugin to push — D100), and --plan
            // says where it would go and does nothing.
            case ["land", var session, ..]:
            {
                // LANDNAME1a: the name the person enters replaces the rule's. A --branch with no name is the usage, not the rule's name.
                var entered = Option(args, "--branch");
                if (args.Contains("--branch") && entered is null) return Usage("`--branch` takes a branch name.");
                // RPCOPEN1: the plan is the running Daoris's where one answers (LANDING), in the alone path's words.
                if (args.Contains("--plan") && await InstanceDoor.TryReadAsync(
                        OpenRoutes.Landing, new { id = session, branch = entered, said = true },
                        TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } viaInstance)
                {
                    return viaInstance;
                }

                using var service = ServiceClient.FromEnvironment();
                var (tree, _) = await service.SessionGroundAsync(session).ConfigureAwait(false);

                // A session whose review reads as landed (REVIEW2, D113) — its landed branch stands, or its tree is gone —
                // is said as the review says it, and lands nothing again: the review offers no Accept there at all.
                // The reading is the route's too (RPC1b4): both doors refuse a second landing in the same words.
                var before = await TreesWords.LandedBeforeAsync(trees, service, session).ConfigureAwait(false);
                var gone = TreeFolders.TreeGone(tree);
                if (before is not null && before.ReadsAsLanded(gone))
                {
                    if (args.Contains("--plan"))
                    {
                        var landedPlan = TreesPlanWords.LandedPlan(session, before);
                        landedPlan.Print(Console.Out, Console.Error);
                        return landedPlan.Code;
                    }

                    var again = TreesWords.NotAgain(session, before, gone);
                    again.Print(Console.Out, Console.Error);
                    return again.Code;
                }

                if (string.IsNullOrWhiteSpace(tree))
                {
                    var none = TreesWords.TreeNamedForSession(await service.SessionRecordsAsync().ConfigureAwait(false), session)
                               ?? TreesWords.NoTreeToLand(session);
                    none.Print(Console.Out, Console.Error);
                    return none.Code;
                }

                // COAGENT1b (D199 point 3): a partner's record lands nothing; its piece lands through its lead.
                if (TreesWords.PartnerLands(await service.SessionRecordsAsync().ConfigureAwait(false), session) is { } partner)
                {
                    partner.Print(Console.Out, Console.Error);
                    return partner.Code;
                }

                var recordsJson = await service.SessionRecordsJsonAsync().ConfigureAwait(false);
                // CHATFLOW1i: a chat that took a quest is gated by that quest's chain, as the window's press reads it.
                var questId = await service.SessionQuestAsync(session).ConfigureAwait(false) ?? ChatRuns.TakenOf(session, recordsJson);
                var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
                // A plugin's own lines, said as they come, under its name — as the console says them (D64 §4).
                var landing = new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => Console.WriteLine($"  plugin:{plugin}  {line}"), log: log));

                // CHATFLOW1d (the chat-flow design §5.2 point 1, Codex's finding 11): a chat in a tree of its own is gated by its chat run, and
                // its press is the driver's one press operation, as the window's is; its plan reads that gate and writes nothing (point 5).
                var chatRun = questId is null ? ChatRuns.ChatOf(session, recordsJson, landing.Holds) : null;
                if (chatRun is not null && !args.Contains("--plan"))
                {
                    return await ChatLandCommand.RunAsync(
                        new ChatLandWorld(service, home, landing, InstanceHost.World(home, "trees"), Console.Out, Console.Error) { Log = log },
                        session, tree, chatRun, recordsJson, entered).ConfigureAwait(false);
                }

                // The landing gate (XAGENT1f, REVIEWENV1c): the second opinion, then the review, read once for the plan, and handed to
                // the landing, which refuses the first that holds.
                var gate = await landing.GateAsync(tree, questId, new ServiceReviewWorld(service), session, run: chatRun).ConfigureAwait(false);
                // Named for the chain's first quest (WSR5), as the review's press names it, and a part of an entry with others for its
                // entry's first part, on the entry's one branch (ACCEPTALL1f, D167's 2026-10-15 amendment point 2): the driver's one call.
                // FABRIC-CLOSE1h3: its quests read once, as the route reads them.
                var quests = new PressQuests(service.FindQuestAsync);
                var subject = await landing.OwnSubjectAsync(
                    tree, session, questId, await service.SessionRecordsAsync().ConfigureAwait(false), quest => quests.FindAsync(quest),
                    named => events.Openings([named]).GetValueOrDefault(named), gate.Process).ConfigureAwait(false);
                subject = subject with { Entered = entered };

                if (args.Contains("--plan"))
                {
                    // How it lands, by the process the gate read (WORKFLOW1f): a named workflow's landing step, or the rule.
                    var plan = await landing.PlanAsync(tree, subject, process: gate.Process).ConfigureAwait(false);
                    // The plan's lines, which the route answers as `said` too: what the line holds of it, the gate, the review's reading of
                    // an earlier landing and where it is due (RPCOPEN1, §7.3).
                    var planned = await TreesPlanWords.LandPlanAsync(landing, home, session, tree, plan, gate, before, []).ConfigureAwait(false);
                    planned.Print(Console.Out, Console.Error);
                    return planned.Code;
                }

                // WORKFLOWCLEAN1 1c (the cleanup design §2.1): a terminal with no loop keeps the word under the verb's lease and never performs a
                // run's `land`: the running Daoris lands it at its next pass.
                if (TreesWords.AcceptedForPass(home, gate) is { } accepted)
                {
                    accepted.Print(Console.Out, Console.Error);
                    return accepted.Code;
                }

                // FABRIC-CLOSE1h3 (D173's note): the result the press is made on, judged again under the tree's hold and before the push,
                // as the route's press is.
                var pressed = await quests.PressedAsync(session, questId).ConfigureAwait(false);
                // LANDHOLD2: said once where the landing has to wait behind another landing of this session's work.
                using var waiting = SayWaiting(Console.WriteLine);
                // The sessions in use, asked when the rule's tidy reaches the other session branches the work holds (LAND3).
                var landed = await landing.LandAsync(tree, subject, inUse: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                    .Select(each => each.Tree).OfType<string>().Where(each => each.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase), gate: gate, joins: subject.Joins,
                    judged: pressed?.Judged, beforePush: pressed?.BeforePush,
                    beside: token => BranchTidying.BesideAsync(service, token)).ConfigureAwait(false);
                Told(home, PressesTold.Land, session);
                // The plugin's lines were said as they came; the landing's sentence and the exit are the route's words too (RPC1b3).
                var said = TreesWords.Land(landed, []);
                // LANDBACK1 part 1 (D87's note, after LANDREBASE2): uncommitted work or a conflict is the session's to clear, and the Accept is
                // the person's yes already. The words go to the session itself, as Daoris's, never through `sessions say` (which would make
                // them the person's, and wait for their Confirm); the yes is kept on it and its work lands once its tree is clean. The
                // exit is 0: the person's act took effect. Words the door keeps nothing of leave the refusal as it was, with why.
                if (LandCarry.WhyOf(landed) is not null
                    && await LandCarry.CarryAsync(
                        // FOUNDATION1d-ii: said at the head of the session's line where it went on, under its key.
                        async (named, text, key, token) => (await WordsHead.FollowAsync(
                            named, at => service.SayAsync(at, text, null, token, from: DaorisWords.From, key: key)).ConfigureAwait(false)).Said,
                        home, session, tree, landed,
                        (await service.SessionRecordsAsync().ConfigureAwait(false)).FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.OrdinalIgnoreCase)),
                        DateTimeOffset.UtcNow).ConfigureAwait(false) is { } carried)
                {
                    if (carried.Told)
                    {
                        Console.WriteLine($"trees: {carried.Sentence}");
                        await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
                        return 0;
                    }

                    said.Print(Console.Out, Console.Error);
                    Console.WriteLine($"trees: {carried.Sentence}");
                }
                else
                {
                    said.Print(Console.Out, Console.Error);
                }

                if (log is not null && LandingGateWords.Held(session, landing.Owner(tree), gate, landed, ReviewDoors.Terminal) is { } held)
                {
                    SessionLog.WriteLanding(log, held);
                }
                // Kept where the conversation is kept, as the review's press keeps it (D100).
                if (landed.Landed) events.Keep(session, LandingNotes.Note(landed), line => Console.Error.WriteLine($"trees: {line}"));
                await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
                // 1 where the step the rule asked for did not happen: refused, or the plugin did not push.
                return said.Code;
            }

            case ["hand", var named, ..]:
                return await TreesHandVerb.RunAsync(args, named, home, trees, log).ConfigureAwait(false);

            // The milestone of every unit before the plugin's ask below, which takes a name.
            case ["state"] or ["state", "--entries"]:
                return await TreesStateVerb.AllAsync(args, home).ConfigureAwait(false);

            case ["state", var named, ..]:
                return await TreesStateVerb.AskAgainAsync(args, named, home, trees, asking).ConfigureAwait(false);

            default:
                return Usage(null);
        }
    }

    /// <summary>The verb's usage, after what was not understood where something was said; exit 2.</summary>
    internal static int Usage(string? problem) => TreesUsage.Say(problem);

    /// <summary>The word after <paramref name="name"/>, or null where it is absent or ends the line.</summary>
    internal static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
    }
}
