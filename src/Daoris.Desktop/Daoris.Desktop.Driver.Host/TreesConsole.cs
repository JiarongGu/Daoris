namespace Daoris.Driver.Host;

/// <summary>The session trees, from a terminal: `daoris tree [list | remove &lt;path&gt; [--force] | clean | land | state | sync | build &lt;path&gt;]`.</summary>
/// <remarks>
/// <para><b>Why here and not in the `daoris` CLI.</b> The tree lifecycle is git spawned against real
/// checkouts, and the CLI's discipline is that it spawns nothing but this program (`driverprogram.ts`) — so the verbs live
/// on the binary that already owns git (parity is between surfaces, not between packages). The
/// standing opt-in is the workflow's <c>isolate</c> step, a file edit; the trees themselves are this side's, being processes and disk.</para>
/// <para>Exit codes keep the family contract: 0 clean · 1 refused (work would be lost) · 2 tool error.</para>
/// </remarks>
internal static class TreesConsole
{
    /// <summary>
    /// What a press changed, told to the open windows: this process has no bus of the desktop's, so it leaves
    /// a note in the request folder, and the shell's loop publishes the change the window's press of <paramref name="route"/> declares
    /// at its next look. Told once the press has answered, whatever it answered; never a failure of the press.
    /// </summary>
    internal static void Told(string home, string route, string? session = null) =>
        new PressesTold(home).Tell(new PressTold(route, session, DateTimeOffset.UtcNow));

    /// <summary>A tree named by its path, as `remove` takes one, rather than a session or its branch.</summary>
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

    /// <summary>What a `trees land` says once before it waits behind another landing or push.</summary>
    internal const string WaitingBehind = "trees: waiting for another landing of this work to end…";

    /// <summary>
    /// What a `trees land` says while it waits: naming the session and branch it waits behind where that
    /// one said who it is, else <see cref="WaitingBehind"/>.
    /// </summary>
    internal static string WaitingLine(LandingHolder? behind) => behind is null
        ? WaitingBehind
        : $"trees: waiting for session {behind.Session}'s landing{(behind.Branch is { Length: > 0 } branch ? $" of `{branch}`" : "")} to end…";

    /// <summary>
    /// Says <see cref="WaitingLine"/> to <paramref name="write"/> once a wait begins, and again only where it comes to name a holder it
    /// had not named, for the landings this call makes, until disposed.
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
    /// The lines this press moved, registered from the line at once rather than at a loop's next
    /// look, so a terminal's landing reads as registered when it returns; each outcome to this host's log. What changed or
    /// must be fixed is said. A service that did not answer leaves them due, for the next loop to follow.
    /// </summary>
    internal static async Task FollowMovedLinesAsync(ServiceClient service, string home, MachineLog? log)
    {
        var followed = await TreesWords.FollowMovedAsync(service, home, () => DriverConfig.Load(DriverConfig.ResolvePath()), log).ConfigureAwait(false);
        foreach (var line in followed.Out) Console.WriteLine(line);
        foreach (var line in followed.Error) Console.Error.WriteLine(line);
    }

    /// <param name="log">This host's machine log: a landing's and a hand-off's plugin frame is written there.</param>
    public static async Task<int> RunAsync(string[] args, MachineLog? log = null)
    {
        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        var trees = new SessionTrees(home);
        // The looks that may ask a plugin about a pull request: its lines said under its name and its frames written
        // to the machine log, as a landing's are.
        var asking = new SessionTrees(home, new LandingPlugins(home, say: (id, line) => Console.WriteLine($"  plugin:{id}  {line}"), log: log));

        switch (args)
        {
            case [] or ["list", ..]:
            {
                var listed = await trees.ListAsync().ConfigureAwait(false);
                if (listed.Count == 0)
                {
                    Console.WriteLine($"trees: none — nothing under {trees.TreesRoot}.");
                    Console.WriteLine("  a workflow that starts with an `isolate` step sets a repository's sessions apart;");
                    Console.WriteLine("  `daoris chat --own-tree` opens one for a conversation.");
                    return 0;
                }

                // The days are the one effective policy's, as the purge reads them.
                var days = KeepPolicy.Of(home).SetAside.Days;
                foreach (var tree in listed)
                {
                    // A folder the look moved aside is listed so, with when, until the purge deletes it.
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

            case ["build", var named]:
                return await TreeBaseBuilds.BuildAsync(home, named, Console.Out, Console.Error).ConfigureAwait(false);

            case ["build", ..]:
                return Usage("`build` takes one path: a kept base, the project folder beside it, or a file under it.");

            case ["sync", ..]:
                return await TreesSyncVerb.RunAsync(args, home, trees, asking, log).ConfigureAwait(false);

            case ["land", "--entry" or "--ask", ..]:
                return await TreesEntryLandVerb.RunAsync(args, home, configPath, log).ConfigureAwait(false);

            // Accepting a session's work: the review's Accept, from a terminal — its second door. The run's `land` lands it,
            // so the press keeps the word for the pass and lands nothing itself, and --plan says where it would go and does nothing.
            case ["land", var session, ..]:
            {
                // The name the person enters replaces the rule's. A --branch with no name is the usage, not the rule's name.
                var entered = Option(args, "--branch");
                if (args.Contains("--branch") && entered is null) return Usage("`--branch` takes a branch name.");
                // The plan is the running Daoris's where one answers (LANDING), in the alone path's words.
                if (args.Contains("--plan") && await InstanceDoor.TryReadAsync(
                        OpenRoutes.Landing, new { id = session, branch = entered, said = true },
                        TreesSaid.ReadWords(Console.Out, Console.Error), InstanceHost.World(home, "trees")).ConfigureAwait(false) is { } viaInstance)
                {
                    return viaInstance;
                }

                using var service = ServiceClient.FromEnvironment();
                var (tree, _) = await service.SessionGroundAsync(session).ConfigureAwait(false);

                // A session whose review reads as landed — its landed branch stands, or its tree is gone —
                // is said as the review says it, and lands nothing again: the review offers no Accept there at all.
                // The reading is the route's too: both doors refuse a second landing in the same words.
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

                // A partner's record lands nothing; its piece lands through its lead.
                if (TreesWords.PartnerLands(await service.SessionRecordsAsync().ConfigureAwait(false), session) is { } partner)
                {
                    partner.Print(Console.Out, Console.Error);
                    return partner.Code;
                }

                var recordsJson = await service.SessionRecordsJsonAsync().ConfigureAwait(false);
                // A chat that took a quest is gated by that quest's chain, as the window's press reads it.
                var questId = await service.SessionQuestAsync(session).ConfigureAwait(false) ?? ChatRuns.TakenOf(session, recordsJson);
                var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
                // No plugin wire: this verb lands nothing, so no plugin is told.
                var landing = new SessionTrees(home);

                // A chat in a tree of its own is gated by its chat run, and
                // its press is the driver's one press operation, as the window's is; its plan reads that gate and writes nothing.
                var chatRun = questId is null ? ChatRuns.ChatOf(session, recordsJson, landing.Holds) : null;
                if (chatRun is not null && !args.Contains("--plan"))
                {
                    return await ChatLandCommand.RunAsync(
                        new ChatLandWorld(service, home, landing, InstanceHost.World(home, "trees"), Console.Out, Console.Error) { Log = log },
                        session, tree, chatRun, recordsJson, entered).ConfigureAwait(false);
                }

                // The landing gate: the second opinion, then the review, read once for the plan, and handed to
                // the landing, which refuses the first that holds.
                var gate = await landing.GateAsync(tree, questId, new ServiceReviewWorld(service), session, run: chatRun).ConfigureAwait(false);
                // Named for the chain's first quest, as the review's press names it, and a part of an entry with others for its
                // entry's first part, on the entry's one branch: the driver's one call.
                // Its quests are read once, as the route reads them.
                var quests = new PressQuests(service.FindQuestAsync);
                var subject = await landing.OwnSubjectAsync(
                    tree, session, questId, await service.SessionRecordsAsync().ConfigureAwait(false), quest => quests.FindAsync(quest),
                    named => events.Openings([named]).GetValueOrDefault(named), gate.Process).ConfigureAwait(false);
                subject = subject with { Entered = entered };

                if (args.Contains("--plan"))
                {
                    // How it lands, by the process the gate read: a named workflow's landing step, or the rule.
                    var plan = await landing.PlanAsync(tree, subject, process: gate.Process).ConfigureAwait(false);
                    // The plan's lines, which the route answers as `said` too: what the line holds of it, the gate, the review's reading of
                    // an earlier landing and where it is due.
                    var planned = await TreesPlanWords.LandPlanAsync(landing, home, session, tree, plan, gate, before, []).ConfigureAwait(false);
                    planned.Print(Console.Out, Console.Error);
                    return planned.Code;
                }

                // A terminal with no loop lands nothing. It keeps the word under
                // the verb's lease for a run whose `land` the runtime performs, which the running Daoris lands at its next pass; any other is held.
                var accepted = TreesWords.AcceptedForPass(home, gate);
                accepted.Print(Console.Out, Console.Error);
                if (log is not null && gate.Refusal is { } refused
                    && LandingGateWords.Held(session, landing.Owner(tree), gate, refused, ReviewDoors.Terminal) is { } held)
                {
                    SessionLog.WriteLanding(log, held);
                }

                return accepted.Code;
            }

            // The milestone of every unit before the plugin's ask below, which takes a name.
            case ["state"] or ["state", "--entries"]:
                return await TreesStateVerb.AllAsync(args, home).ConfigureAwait(false);

            case ["state", var named, ..]:
                return await TreesStateVerb.AskAgainAsync(args, named, home, trees, asking).ConfigureAwait(false);

            default:
                return Usage(null);
        }
    }

    /// <summary>The verb's usage, after any problem the words had; exit 2.</summary>
    internal static int Usage(string? problem) => TreesUsage.Say(problem);

    /// <summary>The word after <paramref name="name"/>, or null where it is absent or ends the line.</summary>
    internal static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
    }
}
