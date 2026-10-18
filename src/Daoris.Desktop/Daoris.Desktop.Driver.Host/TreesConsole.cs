namespace Daoris.Driver.Host;

/// <summary>
/// The session trees, from a terminal: `daoris-driver trees [list | remove &lt;path&gt; [--force] | clean | land | hand | state | sync]`.
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
    internal static string Describe(SweepItem item) => TreesWords.Describe(item);

    /// <summary>
    /// What a press changed, told to the open windows (STATEHUB1e, D169 point 8): this process has no bus of the desktop's, so it leaves
    /// a note in the request folder, and the shell's loop publishes the change the window's press of <paramref name="route"/> declares
    /// at its next look. Told once the press has answered, whatever it answered; never a failure of the press.
    /// </summary>
    private static void Told(string home, string route, string? session = null) =>
        new PressesTold(home).Tell(new PressTold(route, session, DateTimeOffset.UtcNow));

    /// <summary>The door offered beside a kept branch a failed or superseded attempt left (LAND3), or null.</summary>
    internal static string? Offered(SweepItem item) => SessionBranchDoors.RemovalOffered(item);

    /// <summary>A tree named by its path, as `remove` always took one, rather than a session or its branch (LAND3).</summary>
    internal static bool LooksLikePath(string named) => SessionBranchDoors.NamesAPath(named);

    /// <summary>
    /// A session branch the person removes by its door (LAND3): named by the branch (<c>daoris/s-…</c> or <c>s-…</c>) or by the
    /// session, its tree with it while it is here. The driver's one discard, the call the screen's <c>DISCARD_SESSION_BRANCH</c>
    /// ends in (LAND3c), so a branch a live session's tree holds is kept here as there. The service answers the sessions in use
    /// and the registry's checkout.
    /// </summary>
    private static async Task<TreeRemoval> RemoveSessionBranchAsync(SessionTrees trees, string named, string? repository, bool force)
    {
        using var service = ServiceClient.FromEnvironment();
        return await new SessionBranchDiscard(trees).DiscardNamedAsync(service, named, repository, force).ConfigureAwait(false);
    }

    /// <summary>What a `trees land` or `trees hand` says once before it waits behind another landing or push (LANDHOLD2).</summary>
    internal const string WaitingBehind = "trees: waiting for another landing of this work to end…";

    /// <summary>What accepting a session would do, in one line — who pushes it where a plugin does, and what would refuse it (D100).</summary>
    internal static string Planned(string session, LandingPlan plan)
    {
        // ACCEPTALL1g: a branch that already stands (an entry's or a chain's) is moved on, and its pull request grows, as the review says.
        var line = plan.Stands
            ? $"trees: accepting `{session}` would add its work to `{plan.Target}`, a branch that already stands, moving it on ({plan.Source})"
            : $"trees: accepting `{session}` would {(plan.Form == LandingForm.Branch ? "put its work on the branch" : "merge its work into")} `{plan.Target}` ({plan.Source})";
        if (plan.Plugin is { } plugin) line += plan.Stands ? $", then plugin `{plugin}` would push it to grow its pull request" : $", then plugin `{plugin}` would push it and open the pull request";
        line += ".";
        // LAND2b: the rule's switch lands it at its quest's done with no press, and a plugin it cannot use does not stop the branch.
        if (plan.AutoAccept) line += " The rule accepts automatically, so its quest's done lands it with no press.";
        return plan.Problem is { } problem
            ? $"{line} {(plan.AutoAccept ? "Its push would not be tried now" : "It would be refused now")}: {problem}"
            : line;
    }

    /// <summary>
    /// What an entry's Accept would do, in lines (ACCEPTALL1e): where its parts land, then each part's own words: what it shares,
    /// what it leaves out, what it conflicts with, what holds it and what a later part superseded; the page's plan in a terminal's words.
    /// </summary>
    internal static IEnumerable<string> EntryPlanned(SessionEntry entry, EntryPlan plan, IReadOnlyDictionary<string, string> held)
    {
        var count = plan.Parts.Count(part => part.SameTreeAs is null);
        yield return plan.Parts.Count == 0
            ? $"trees: `{entry.Key}` ({entry.Repository}) has no part with work to land here."
            : $"trees: accepting `{entry.Key}` ({entry.Repository}) would {(plan.Form == LandingForm.Branch ? "put its work on the branch" : "merge its work into")} "
              + $"`{plan.Target}`, {count} tree(s), oldest first.";
        foreach (var part in plan.Parts)
        {
            if (part.SameTreeAs is { } same) yield return $"  {part.Session}  shares `{same}`'s tree, which that part's landing carries.";
            if (part.Plan?.LeftOut is { Count: > 0 } left) yield return $"  {part.Session}  leaves out {left.Count} commit(s) the line holds.";
            if (part.Plan?.Conflict is { } conflict) yield return $"  {part.Session}  would conflict: {conflict.Says}";
            if (held.TryGetValue(part.Session, out var why)) yield return $"  {part.Session}  is held: {why}";
            if (part.Superseded is { } by)
            {
                yield return $"  {part.Session}  was redone by `{by.By}`: {(by.Conflicts ? "it conflicts after that part's work" : "it adds nothing after it")} "
                    + $"(`--leave-out {part.Session}` leaves it out): {by.Says}";
            }
        }
    }

    /// <summary>
    /// A session's entry on the due list (LAND2b, design §8), for <c>trees land --plan</c>: when it became due, and its last try
    /// by its code; null where it was never due.
    /// </summary>
    internal static string? Due(AutoLanding? entry)
    {
        if (entry is null) return null;
        var due = $"trees: due to land automatically since {Minute(entry.DueAt)} UTC";
        if (entry.Last is not { } last) return $"{due}; not tried yet — the loop's next look tries it.";
        var tried = $"{due}; last tried {Minute(last.At)} UTC: {last.Code}"
                    + (last.Branch is { } branch ? $" (`{branch}`)" : "");
        return entry.Closed is null
            ? $"{tried}. It waits: " + last.Code switch
            {
                AutoLandingCode.Held => "its quest's release lands it.",
                // FABRIC-CLOSE1h2: its branch is made and its push held back, so the release hands the branch on; nothing lands again.
                AutoLandingCode.PushHeld => "its quest's release hands its branch on, or `trees hand` does now.",
                _ => "a change in its tree, or your press lands it.",
            }
            : $"{tried}.";
    }

    private static string Minute(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

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
                    Console.WriteLine("  `daoris driver trees <repository> on` opts a repository in;");
                    Console.WriteLine("  `daoris-driver chat --own-tree` opens one for a conversation.");
                    return 0;
                }

                foreach (var tree in listed)
                {
                    // AUTOTIDY2c: a folder the look moved aside is listed so, with when, until the purge deletes it.
                    Console.WriteLine(tree.MovedAside is { } moved
                        ? $"  {tree.Workspace}/{tree.Repository}  {tree.Branch} on {moved.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}, "
                          + $"deleted once it is left untouched for {BranchTidying.SetAsideFor.TotalDays:0} days"
                        : $"  {tree.Workspace}/{tree.Repository}  {tree.Branch}");
                    Console.WriteLine($"    {tree.Path}");
                }

                return 0;
            }

            // A tree by its path, as it always was; or a session's branch by the session or the branch (LAND3): a failed or
            // superseded attempt's, which no landing's tidy and no clean-up takes since its commits are on no branch of the
            // person's. Its tree goes with it where it is still here. --force means it, except over a tree a session still
            // running or waiting holds, which the driver's one discard keeps for both doors (LAND3c).
            case ["remove", var named, ..]:
            {
                var force = args.Contains("--force");
                var removal = LooksLikePath(named)
                    ? await trees.RemoveAsync(named, force).ConfigureAwait(false)
                    : await RemoveSessionBranchAsync(trees, named, Option(args, "--repository"), force).ConfigureAwait(false);
                Told(home, LooksLikePath(named) ? PressesTold.DiscardTree : PressesTold.DiscardBranch);
                // A refusal that preserves work is policy doing its job, not a tool error. The words are the routes' too (RPC1b3).
                var said = TreesWords.Remove(removal);
                said.Print(Console.Out, Console.Error);
                return said.Code;
            }

            // The clean-up (WSR3, D88): the list first, and with --yes the press — a workspace's Branches tab is the
            // other door (D50), and --workspace takes that workspace's checkouts alone, as the tab does (BRSCOPE1a). The
            // checkouts and the sessions in use are the service's, so this one asks it, once its words read.
            case ["clean", ..]:
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
                var inUse = (await service.ActiveSessionsAsync().ConfigureAwait(false))
                    .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!ask.Yes)
                {
                    var plan = await asking.CleanPlanAsync(repositories, inUse).ConfigureAwait(false);
                    if (plan.Sessions.Count == 0 && plan.Landed.Count == 0)
                    {
                        Console.WriteLine(TreesCommand.NothingToClean(ask));
                        return 0;
                    }

                    // LAND4: the session each kept branch's tree is, so its landing is offered beside the discard, as on its page.
                    var records = plan.Sessions.Any(item => item.Kind == SweepKind.Unlanded && item.Tree is not null)
                        ? await SessionBranchDoors.RecordsOrNoneAsync(service).ConfigureAwait(false)
                        : [];
                    foreach (var item in plan.Sessions)
                    {
                        Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")} {Describe(item)}");
                        if (SessionBranchDoors.LandingOffered(item, SessionBranchDoors.SessionOfTree(records, item.Tree)) is { } landing)
                        {
                            Console.WriteLine($"         {landing}");
                        }

                        if (Offered(item) is { } offer) Console.WriteLine($"         {offer}");
                    }
                    // The branches landings made (WSR5), in a group of their own: each goes where its work reached the line.
                    if (plan.Landed.Count > 0) Console.WriteLine("  landed branches — each goes once its work reads on the line:");
                    foreach (var item in plan.Landed) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")} {LandedWords.Describe(item)}");
                    var going = plan.Sessions.Count(item => item.Removable) + plan.Landed.Count(item => item.Removable);
                    Console.WriteLine(going == 0
                        ? "trees: nothing to clean — every branch listed holds something of its own."
                        : $"trees: {going} branch(es) would go, a session's with its tree. `{TreesCommand.Press(ask)}` removes them.");
                    return 0;
                }

                var done = await trees.CleanAsync(repositories, inUse).ConfigureAwait(false);
                Told(home, PressesTold.Sweep);
                // The words are the route's too (RPC1b3): each removal or keeping, the folders left, the count.
                var said = TreesWords.Clean(done);
                said.Print(Console.Out, Console.Error);
                return said.Code;
            }

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

                var inUse = (await service.ActiveSessionsAsync().ConfigureAwait(false))
                    .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!ask.Yes)
                {
                    // Its fetch first, then its plugin asks where git cannot tell (PLUGHOOK1c, design §2.1 occasion 3).
                    var plan = await asking.SyncPlanAsync(repositories, inUse, fetch: true, scope: scope).ConfigureAwait(false);
                    // What was not fetched, said once and first (WSR7), rather than at the end of every row.
                    foreach (var line in SyncWords.NotFetched(plan.Lines, DateTimeOffset.UtcNow)) Console.WriteLine(line);
                    foreach (var pull in plan.Lines) Console.WriteLine($"  {(pull.Moves ? "moves" : "stays")}  {SyncWords.Describe(pull)}");
                    foreach (var item in plan.Rebases) Console.WriteLine($"  {(item.Replays ? "moves" : "stays")}  {SyncWords.Describe(item)}");
                    if (plan.Deletes.Count > 0) Console.WriteLine("  landed branches whose work reached the line:");
                    foreach (var item in plan.Deletes) Console.WriteLine($"  {(item.Removable ? "goes " : "kept ")}  {LandedWords.Describe(item)}");
                    if (SyncWords.Apart(plan.Apart) is { } apart) Console.WriteLine(apart);
                    var acts = plan.Lines.Count(pull => pull.Moves) + plan.Rebases.Count(item => item.Replays) + plan.Deletes.Count(item => item.Removable);
                    Console.WriteLine(plan.Looked.Count == 0 ? TreesCommand.NoneHeld(ask)
                        : acts == 0 ? "trees: everything here is up to date."
                        : $"trees: {acts} thing(s) would change. `{TreesCommand.Press(ask)}` does them — Daoris fetches, and never pushes.");
                    return 0;
                }

                // The sessions in use, asked again while each repository's trees are held for its replays (LEFT2).
                var done = await trees.SyncAsync(repositories, inUse, only: null, fetch: true,
                    inUseNow: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase), scope: scope).ConfigureAwait(false);
                Told(home, PressesTold.TreesSync);
                // The words are the route's too (RPC1b3): what was not fetched, each row, what was left apart, the registry's
                // following of the lines moved, and the count, 1 where something the proofs cleared did not happen.
                var followed = await TreesWords.FollowMovedAsync(service, home, () => DriverConfig.Load(DriverConfig.ResolvePath()), log).ConfigureAwait(false);
                var said = TreesWords.Sync(done, DateTimeOffset.UtcNow, followed);
                said.Print(Console.Out, Console.Error);
                return said.Code;
            }

            // A line of work's one Accept, or an ask's (ACCEPTALL1e, D167's amendment, D50): the page's `LAND_ENTRY` and `LAND_ASK` from a
            // terminal, over the library's one code (`EntryAccepts`); --plan says where the parts would land and does nothing.
            case ["land", "--entry" or "--ask", ..]:
            {
                if (TreesCommand.ReadEntryLand(args, out var problem) is not { } ask) return Usage(problem);
                using var service = ServiceClient.FromEnvironment();
                var config = DriverConfig.Load(configPath);
                var accepts = new EntryAccepts(
                    service, home,
                    new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => Console.WriteLine($"  plugin:{plugin}  {line}"), log: log)),
                    new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), (_, line) => Console.Error.WriteLine($"trees: {line}"),
                    async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                        .Select(each => each.Tree).OfType<string>().Where(each => each.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    log, ReviewDoors.Terminal);
                var (found, rows) = await EntryAccepts.LookAsync(
                    service, config, SessionsConsole.Wire(config), lastLook: null, waits: null, home,
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
                        foreach (var line in EntryPlanned(entry, plan, held)) Console.WriteLine(line);
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
                var said = TreesWords.EntryLand(landings, [], []);
                said.Print(Console.Out, Console.Error);
                await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
                return said.Code;
            }

            // Accepting a session's work (WSR1, D87): the review's Accept, from a terminal — D50's second
            // door, which the landing had not had. The workspace's rule decides the form (merged into the
            // line, or a branch for the person to push, or for the rule's plugin to push — D100), and --plan
            // says where it would go and does nothing.
            case ["land", var session, ..]:
            {
                // LANDNAME1a: the name the person enters replaces the rule's. A --branch with no name is the usage, not the rule's name.
                var entered = Option(args, "--branch");
                if (args.Contains("--branch") && entered is null) return Usage("`--branch` takes a branch name.");
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
                        Console.WriteLine($"trees: {LandedReviewWords.Describe(session, before)}");
                        if (Due(new AutoLandings(home).Of(session)) is { } landedDue) Console.WriteLine(landedDue);
                        return 0;
                    }

                    var again = TreesWords.NotAgain(session, before, gone);
                    again.Print(Console.Out, Console.Error);
                    return again.Code;
                }

                if (string.IsNullOrWhiteSpace(tree))
                {
                    var none = TreesWords.NoTreeToLand(session);
                    none.Print(Console.Out, Console.Error);
                    return none.Code;
                }

                var questId = await service.SessionQuestAsync(session).ConfigureAwait(false);
                var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
                // A plugin's own lines, said as they come, under its name — as the console says them (D64 §4).
                var landing = new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => Console.WriteLine($"  plugin:{plugin}  {line}"), log: log));

                // The landing gate (XAGENT1f, REVIEWENV1c): the second opinion, then the review, read once for the plan, and handed to
                // the landing, which refuses the first that holds.
                var gate = await landing.GateAsync(tree, questId, new ServiceReviewWorld(service), session).ConfigureAwait(false);
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
                    Console.WriteLine(Planned(session, plan));
                    // LANDSYNC1: what the press would leave out because the line holds it, judged against the last fetch.
                    foreach (var line in LandingSyncWords.Plan(plan)) Console.WriteLine(line);
                    // SQUASHTIDY1f: work the line holds by content, which the press refuses, said before it in the press's own sentence.
                    if (await landing.DiscardOfferAsync(tree).ConfigureAwait(false) is { } carried) Console.WriteLine($"trees: {carried.NotLanded}");
                    // What the gate says before the press (the second-agent design §8.6, the review design §3.5): what holds it, or
                    // what let it go, in the gate's order.
                    foreach (var line in LandingGateWords.Plan(gate)) Console.WriteLine(line);
                    // Landed before, its branch gone since, its tree still here: said, as the review's note says it.
                    if (before is not null) Console.WriteLine($"trees: {LandedReviewWords.Describe(session, before)}");
                    // Due to land at its quest's done (LAND2b): what its tries came to, which the review's note shows.
                    if (Due(new AutoLandings(home).Of(session)) is { } due) Console.WriteLine(due);
                    return 0;
                }

                // FABRIC-CLOSE1h3 (D173's note): the result the press is made on, judged again under the tree's hold and before the push,
                // as the route's press is.
                var pressed = await quests.PressedAsync(session, questId).ConfigureAwait(false);
                // LANDHOLD2: said once where the landing has to wait behind another landing of this session's work.
                using var waiting = LandingHoldNotice.Told(now => { if (now) Console.WriteLine(WaitingBehind); });
                // The sessions in use, asked when the rule's tidy reaches the other session branches the work holds (LAND3).
                var landed = await landing.LandAsync(tree, subject, inUse: async token => (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                    .Select(each => each.Tree).OfType<string>().Where(each => each.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase), gate: gate, joins: subject.Joins,
                    judged: pressed?.Judged, beforePush: pressed?.BeforePush).ConfigureAwait(false);
                Told(home, PressesTold.Land, session);
                // The plugin's lines were said as they came; the landing's sentence and the exit are the route's words too (RPC1b3).
                var said = TreesWords.Land(landed, []);
                said.Print(Console.Out, Console.Error);
                if (log is not null && LandingGateWords.Held(session, landing.Owner(tree), gate, landed, ReviewDoors.Terminal) is { } held)
                {
                    SessionLog.WriteLanding(log, held);
                }
                // Kept where the conversation is kept, as the review's press keeps it (D100).
                if (landed.Landed) events.Keep(session, LandingRules.Note(landed), line => Console.Error.WriteLine($"trees: {line}"));
                await FollowMovedLinesAsync(service, home, log).ConfigureAwait(false);
                // 1 where the step the rule asked for did not happen: refused, or the plugin did not push.
                return said.Code;
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
                    Console.WriteLine(HandPlanned(plan));
                    return 0;
                }

                using var waiting = LandingHoldNotice.Told(now => { if (now) Console.WriteLine(WaitingBehind); });
                var handed = await handing.HandAsync(root, entry, plugin).ConfigureAwait(false);
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

            // Ask again (PLUGHOOK1c, D148 point 2, design §2.1 occasion 4): the review's press, from a terminal (D50). The landed
            // branch's plugin is asked about its pull request whatever the kept answer's age, the answer kept, and what it proves here
            // said. Named by the session that landed it or by the branch, standing or a trace. It removes nothing: the clean-up and
            // bringing up to date act on what it kept. Exempt from Ask Daoris as a read (D110 §4).
            // The milestone of every unit (FABRIC-MILESTONE1a, D50): end reached, result kept, cleanup pending or completed, recovery retained,
            // derived read-only from the records the tree lifecycle already keeps. Before the plugin's ask below, which takes a name.
            case ["state"] or ["state", "--entries"]:
            {
                var (units, unread) = await WorkConsole.ReadAsync(home).ConfigureAwait(false);
                foreach (var line in unread) Console.Error.WriteLine($"trees: {line}");
                foreach (var line in WorkWords.All(units, args.Contains("--entries"))) Console.WriteLine(line);
                return 0;
            }

            case ["state", var named, ..]:
            {
                var repository = Option(args, "--repository");
                // The milestone of the unit it names, before the plugin is asked (FABRIC-MILESTONE1a); a name no unit has is said by the ask's own words.
                var (milestones, notRead) = await WorkConsole.ReadAsync(home).ConfigureAwait(false);
                foreach (var line in notRead) Console.Error.WriteLine($"trees: {line}");
                foreach (var unit in WorkInventories.Named(milestones, named)) foreach (var line in WorkWords.Milestone(unit)) Console.WriteLine(line);
                var found = trees.Recorded.FindAll(named, repository);
                if (found.Count == 0)
                {
                    Console.Error.WriteLine($"trees: `{named}` names no branch a landing made on this machine, so there is no pull request of it to ask about.");
                    return 1;
                }

                // A session means its newest landing; a branch name in two repositories means the person says which.
                var owners = found.Select(each => each.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (owners.Count > 1)
                {
                    Console.Error.WriteLine($"trees: `{named}` names a landed branch in {string.Join(", ", owners.Select(r => $"`{r}`"))} — "
                        + "say which with `--repository <name>`.");
                    return 1;
                }

                var entry = found[0];
                using var service = ServiceClient.FromEnvironment();
                var root = (await service.RegistryAsync().ConfigureAwait(false))
                    .FirstOrDefault(row => string.Equals(row.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase))?.Root;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    Console.Error.WriteLine($"trees: `{entry.Repository}` has no checkout on this machine, where its plugin would ask and git would judge the answer.");
                    return 1;
                }

                var again = await asking.AskAgainAsync(root, entry).ConfigureAwait(false);
                foreach (var line in PullRequestWords.AskedAgain(again)) Console.WriteLine($"trees: {line}");
                // 1 where nothing new is known: no plugin to ask, one not ready, or an ask that failed.
                return again.Answered || again.Final ? 0 : 1;
            }

            default:
                return Usage(null);
        }
    }

    /// <summary>The verb's usage, after what was not understood where something was said; exit 2.</summary>
    private static int Usage(string? problem)
    {
        if (problem is not null) Console.Error.WriteLine($"trees: {problem}");
        Console.Error.WriteLine("usage: daoris-driver trees [list | remove <path|session|branch> [--repository <name>] [--force]");
        Console.Error.WriteLine("                           | clean [--workspace <name>] [--yes] | land <session> [--plan] [--branch <name>]");
        Console.Error.WriteLine("                           | land --entry <id> | --ask <id> [--leave-out <session>]... [--plan]");
        Console.Error.WriteLine("                           | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]");
        Console.Error.WriteLine("                           | state [--entries]");
        Console.Error.WriteLine("                           | state <session|branch> [--repository <name>]");
        Console.Error.WriteLine("                           | sync [--repository <name>] [--workspace <name>] [--all] [--yes]]");
        Console.Error.WriteLine("  A session's worktree (D51). Removal refuses while the tree holds");
        Console.Error.WriteLine("  uncommitted changes or work no branch of yours holds; --force means it.");
        Console.Error.WriteLine("  A session or its branch removes a failed or superseded attempt's branch,");
        Console.Error.WriteLine("  with its tree where it is still here (LAND3); one whose tree a session");
        Console.Error.WriteLine("  still running or waiting holds is kept, even with --force (LAND3c).");
        Console.Error.WriteLine("  clean lists every session branch with what it holds; --yes removes those");
        Console.Error.WriteLine("  whose work is on a branch of yours, or that hold nothing (D88), and every");
        Console.Error.WriteLine("  branch a landing made whose files read on the line as it left them (WSR5).");
        Console.Error.WriteLine("  land accepts a session's work as the review's Accept does, by the workspace's");
        Console.Error.WriteLine("  rule; --plan says where it would go and does nothing (D87). A rule that tidies");
        Console.Error.WriteLine("  also removes every other session branch the landed work holds (LAND3). --branch names the");
        Console.Error.WriteLine("  branch itself, in place of the rule's name, where the rule makes a branch (LANDNAME1a). A rule naming a");
        Console.Error.WriteLine("  plugin hands the branch to it to push and open the pull request (D100). A session");
        Console.Error.WriteLine("  that already landed, while its branch stands or once its tree is gone, is said");
        Console.Error.WriteLine("  as its review says it, and lands nothing again (D113).");
        Console.Error.WriteLine("  land --entry or --ask is the one Accept of a line of work or an ask, as Sessions' list has it: every part");
        Console.Error.WriteLine("  lands in the order it ran, each by its own gate and the rule; --leave-out names a part a later one");
        Console.Error.WriteLine("  redid, which --plan says (D167's 2026-10-15 amendment). An ask lands each repository's line of work.");
        Console.Error.WriteLine("  hand gives a branch a landing made to a landing plugin afterwards — the one");
        Console.Error.WriteLine("  --plugin names, else the rule's — to push and open the pull request (WSR5).");
        Console.Error.WriteLine("  state alone says each unit of work's milestone: whether its end was reached, the result kept, the");
        Console.Error.WriteLine("  cleanup pending (what waits and why) or completed, and the recovery retained; --entries lists the");
        Console.Error.WriteLine("  inventory it is read from. It changes nothing (FABRIC-MILESTONE1a); `work state` says the same.");
        Console.Error.WriteLine("  state asks the plugin that pushed a landed branch, else the rule's, again whether");
        Console.Error.WriteLine("  its pull request completed, keeps the answer, and says what it proves here; the");
        Console.Error.WriteLine("  clean-up and sync act on what it kept, and ask too where git cannot tell (D148).");
        Console.Error.WriteLine("  sync brings each repository up to date after a pull request merged: it fetches");
        Console.Error.WriteLine("  the line and fast-forwards it, replays the branches still at work onto it (only");
        Console.Error.WriteLine("  their own commits), and deletes the landed branches whose work reached it. It");
        Console.Error.WriteLine("  lists first; --yes does it. Daoris fetches, and never pushes (WSR6). It takes");
        Console.Error.WriteLine("  the repositories holding Daoris's branches and names the rest; --all takes");
        Console.Error.WriteLine("  every one, and --repository the one named (D112).");
        Console.Error.WriteLine("  For clean and sync, --workspace takes one workspace's checkouts alone, as its");
        Console.Error.WriteLine("  Branches tab does; a workspace the registry does not name is refused (BRSCOPE1a).");
        return 2;
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

    /// <summary>The word after <paramref name="name"/>, or null where it is absent or ends the line.</summary>
    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
    }
}
