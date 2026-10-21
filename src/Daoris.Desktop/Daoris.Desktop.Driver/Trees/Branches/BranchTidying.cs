using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's own tidy of session branches (AUTOTIDY1, AUTOTIDY2a, D88's notes): a session branch the press would clear goes
/// without a press, its tree only where it holds no uncommitted or untracked path, and the look says so in its report and the
/// machine log; and after them each branch a landing made that the press would clear (AUTOTIDY2b). Held by the watch across its
/// looks, since each look's driver is built fresh: what it already said, and when it last looked.
/// </summary>
/// <remarks>
/// <para><b>The press's path, not a second one.</b> Each repository is tidied by <see cref="SessionTrees.TidyEmptyAsync"/>,
/// which judges each session branch the line is kept holding (FABRIC-END4a) as the press's list does and runs the clean-up's own path over those the look takes
/// (<see cref="SessionTrees.GoesByItself"/>). Every guard is the press's: judged again right before it goes, deleted only
/// while it is the commit judged, never forced. The look adds its last guards, and never deletes a tree's folder: it moves it
/// aside, its package folders go an hour later, and a later look deletes it only once it has waited the policy's days with nothing
/// written in it since (KEEP1a, <see cref="KeepPolicy"/>).</para>
///
/// <para><b>Said once.</b> A branch kept by a last guard, because git could not read one, or because the removal did not
/// happen, is said in the report and written to the log the first look it is met, and not again while it lasts: a branch that
/// stays for weeks is one line. What holds a repository (a start in one of its trees, a service that did not answer), and a
/// folder moved aside that outlived its wait, are said the same way.</para>
///
/// <para><b>Once each pace</b>, from the first look: a pass reads every checkout's branches with git, which the loop's own pace
/// would repeat every few seconds for nothing.</para>
/// </remarks>
public sealed class BranchTidying(string home)
{
    /// <summary>How often a pass looks, at most.</summary>
    public static readonly TimeSpan DefaultPace = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();

    // What was said and is still so, by repository, branch and code: said again only after a pass where it was not.
    private HashSet<string> _said = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _began;

    /// <summary>How often a pass looks, at most; a test's is zero.</summary>
    public TimeSpan Pace { get; init; } = DefaultPace;

    /// <summary>What time it is; the system's by default.</summary>
    internal Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Whether a pass is due: at the first look, and then once each <see cref="Pace"/>.</summary>
    public bool Due
    {
        get
        {
            lock (_gate) return _began is not { } began || Clock() - began >= Pace;
        }
    }

    /// <summary>A pass begins now: the next is due a pace from here.</summary>
    public void Began()
    {
        lock (_gate) _began = Clock();
    }

    /// <summary>
    /// One pass over every repository with a checkout here: what it removed and what it says once, as the report's lines, and
    /// the same written to <paramref name="log"/>. Never throws for a service or git that does not answer: it says so, once.
    /// </summary>
    /// <param name="reviewing">
    /// The folders a review step in progress shows from (<see cref="ReviewingAsync"/>), asked with the sessions once a
    /// repository is held: the tree each lies in is kept as a session's in use is. Null where nothing is reviewed here.
    /// </param>
    public async Task<IReadOnlyList<string>> LookAsync(
        ServiceClient service, MachineLog? log, CancellationToken ct,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>>? reviewing = null)
    {
        Began();
        var lines = new List<string>();
        var met = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var whole = false;
        try
        {
            IReadOnlyList<RepoView> registry;
            try
            {
                registry = await service.RegistryAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
            {
                lines.AddRange(Say([new TidyPass([], TidyKept.Sessions, error.Message)], log, met));
                return lines;
            }

            var trees = new SessionTrees(home);
            foreach (var row in registry.Where(row => !string.IsNullOrWhiteSpace(row.Root) && Directory.Exists(row.Root)))
            {
                TidyPass pass;
                IReadOnlyList<StrayResult> strays = [];
                try
                {
                    // TREELOCK1: a session lock whose session no longer holds its tree goes first, since the tidy refuses a locked tree.
                    await trees.LetGoUnheldLocksAsync(row.Root!, row.Repository, row.Workspace, InUseAsync, ct).ConfigureAwait(false);
                    // AUTOTIDY2c, OPENCUT1: the folders under its trees that are no tree first, so a branch whose folder one was
                    // goes in the same look rather than being kept as unreadable.
                    strays = await trees.TidyStraysAsync(row.Root!, row.Repository, row.Workspace, InUseAsync, Clock(), ct).ConfigureAwait(false);
                    pass = await trees.TidyEmptyAsync(row.Root!, row.Repository, row.Workspace, InUseAsync, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
                {
                    pass = Threw(row.Repository, row.Workspace, error);
                }

                // Said as each repository is done: a removal is written though the look is closed before the next.
                lines.AddRange(Say([pass with { Strays = strays }], log, met));
            }

            lines.AddRange(await KeepAsync(trees, service, log, met, ct).ConfigureAwait(false));
            whole = true;
            return lines;
        }
        finally
        {
            // What a whole pass did not meet again has cleared: met again later, it is said again.
            lock (_gate)
            {
                if (whole) _said = met;
                else _said.UnionWith(met);
            }
        }

        Task<IReadOnlySet<string>> InUseAsync(CancellationToken token) => BranchTidying.InUseAsync(home, service, reviewing, token);
    }

    /// <summary>
    /// The trees the look's tidy keeps whatever they hold: a session running or waiting holds its tree; so does one that ended with the
    /// person's words waiting on its record, which the next loop goes on with there (AUTOTIDY2d, D137 §2.2); and a review step in
    /// progress, by the folder it shows from. The pass's and link 2's (FABRIC-END4a) alike.
    /// </summary>
    public static async Task<IReadOnlySet<string>> InUseAsync(
        string home, ServiceClient service, Func<CancellationToken, Task<IReadOnlyCollection<string>>>? reviewing, CancellationToken ct)
    {
        var held = (await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        held.UnionWith(await service.WaitingTreesAsync(ct).ConfigureAwait(false));
        if (reviewing is not null) held.UnionWith(TreesOf(new SessionTrees(home).TreesRoot, await reviewing(ct).ConfigureAwait(false)));
        return held;
    }

    /// <summary>
    /// The policy a pass keeps by (KEEP1a): the one effective policy every reader of the set-aside days reads (<see cref="KeepPolicy"/>); a
    /// test hands another, as it hands a clock.
    /// </summary>
    internal Func<string, KeepPolicy> Policy { get; init; } = KeepPolicy.Of;

    /// <summary>How long the Keeping pass removes for, at most (the cleanup design §3.1); a test's may be none.</summary>
    internal TimeSpan Budget { get; init; } = SetAsideKeeping.DefaultBudget;

    /// <summary>
    /// The person's choices as the loop resolves them, which the downloads' rule reads for a command naming a file inside a version and locks
    /// at its judgement (KEEP1b, the cleanup design §3.5); this home's own resolution unless the watch names the loop's.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>The running sessions, as the downloads' rule reads them; a test hands its own, as it hands a clock.</summary>
    internal Func<ServiceClient, CancellationToken, Task<LiveSessions>>? Sessions { get; init; }

    // KEEP1b: when the downloads' rule last ran its daily removal in this process, and under which days, so a changed rule runs at once.
    private DateTimeOffset? _downloadsDaily;
    private TimeSpan? _downloadsDays;

    /// <summary>How often the downloads' rule removes what is past its days (the cleanup design §3.1, step 5).</summary>
    public static readonly TimeSpan DownloadsPace = TimeSpan.FromDays(1);

    /// <summary>
    /// The Keeping pass (KEEP1a, D195 point 4; the cleanup design §3.1), in the tidy's task after its repositories, where the purge ran,
    /// so it holds no repository's hold and a start never waits on it: it tries the home's run lock, and held elsewhere removes nothing
    /// this look and names the holder, once; else the set-aside kind's removal, the purge at the policy's days then the slims due, within
    /// the pass's budget; then <c>keeping.json</c>'s <c>last</c> for the kind, where it removed something or newly kept a path. Then the
    /// downloads' rule (KEEP1b, <see cref="DownloadKeeping"/>): each version's unused stretch each pass, and once a day, or at once after
    /// the rule changed, the versions past their days.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepAsync(SessionTrees trees, ServiceClient service, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        var setAside = Directory.Exists(trees.SetAsideRoot);
        // Nothing set aside and nothing downloaded, nothing to keep: the run lock and its holder's file are not written every pace for nothing.
        if (!setAside && !Downloaded()) return lines;
        using var run = KeepingRun.TryTake(home, KeepingRun.Look);
        if (run is null)
        {
            var holder = KeepingRun.Holder(home);
            lock (_gate)
            {
                if (First(met, "", "keeping", "held"))
                {
                    lines.Add($"tidy  {(setAside ? "the folders moved aside" : "the downloads")} were not cleaned this look: {holder?.Said ?? "another run"} "
                        + "holds Daoris's keeping, and a later look tries again.");
                }
            }

            return lines;
        }

        // One budget for the pass's removals (§3.1): what the set-aside folders spent, the downloads do not spend again.
        var spent = System.Diagnostics.Stopwatch.StartNew();
        if (setAside) lines.AddRange(KeepSetAside(trees, run, log, met, ct));
        var left = Budget - spent.Elapsed;
        lines.AddRange(await KeepDownloadsAsync(service, run, left > TimeSpan.Zero ? left : TimeSpan.Zero, log, met, ct).ConfigureAwait(false));
        return lines;
    }

    /// <summary>Whether anything is downloaded here to keep: either folder of the downloads, or both places of the layout holding one.</summary>
    private bool Downloaded()
    {
        try
        {
            return Directory.Exists(DaorisHome.Entry(home, HomeEntry.Toolchain)) || Directory.Exists(DaorisHome.Entry(home, HomeEntry.ToolVersions));
        }
        catch (HomeEntryTwiceException)
        {
            return true;
        }
    }

    /// <summary>
    /// The downloads' rule, holding the home's run (KEEP1b): what it removed said in the report, and each version it kept past its days, and a
    /// record it could not write, said the first look it is met. A day's removal the budget cut short is taken up by the next pass.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepDownloadsAsync(
        ServiceClient service, KeepingRun run, TimeSpan budget, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        var policy = Policy(home).Downloads;
        var now = Clock();
        bool daily;
        lock (_gate) daily = _downloadsDaily is not { } last || now - last >= DownloadsPace || _downloadsDays != policy.Days;
        var markers = DaorisHome.Entry(home, HomeEntry.SessionPids);
        var judge = DownloadJudge.Of(
            home, HarnessSettings.ResolvePath(home), ConfigPath ?? DriverConfig.ResolvePath(home),
            token => Sessions is { } stand ? stand(service, token) : LiveSessions.ReadAsync(service, markers, token), Clock);
        var pass = await DownloadKeeping.PassAsync(home, policy, judge, daily, run.Door, run.Id, log, ct, budget).ConfigureAwait(false);
        lock (_gate)
        {
            if (daily && pass.Unwritten is null && !pass.Cut)
            {
                _downloadsDaily = now;
                _downloadsDays = policy.Days;
            }

            foreach (var each in pass.Cleared)
            {
                var row = each.Version;
                var named = $"{each.Asked.Name} {each.Asked.Version}";
                switch (each.Outcome)
                {
                    case "removed":
                        lines.Add($"tidy  removed the download {named}, which nothing had used for {policy.Days?.TotalDays:0} days ({Size(row?.Bytes ?? 0)}); "
                            + "its next pin or use downloads it again.");
                        break;
                    case "unfinished":
                        lines.Add($"tidy  moved the download {named} aside, unused for {policy.Days?.TotalDays:0} days, and part of it could not be removed yet; "
                            + "nothing runs it, and a later clearing takes the rest.");
                        break;
                    case "kept" when row?.Keeps is { } why && First(met, "", $"download/{named}", "kept-" + why):
                        lines.Add($"tidy  the download {named} stays past its days, since {DownloadKept(why, row.By)}.");
                        break;
                }
            }

            if (pass.Unwritten is { } unwritten && First(met, "", KeepingRecord.PathOf(home), "unwritten"))
            {
                lines.Add($"tidy  {Clause(unwritten)}.");
            }
        }

        return lines;
    }

    /// <summary>Why the downloads' rule kept a version past its days, in a clause (<see cref="DownloadedVersion.Keeps"/>).</summary>
    private static string DownloadKept(string why, IReadOnlyList<string> by) => why switch
    {
        "running" => $"a running session was handed it ({string.Join(", ", by.Select(each => each.Replace("session:", "", StringComparison.Ordinal)))})",
        "pinned" => "it was pinned since",
        "used" => "Tools runs it now",
        "named" => $"{by.FirstOrDefault()} names a file inside it",
        "in-use" => "a program is running from it",
        "unread" => $"what uses it cannot be read now ({by.FirstOrDefault()})",
        _ => why,
    };

    /// <summary>The set-aside kind's removal, holding the home's run (KEEP1a).</summary>
    private IReadOnlyList<string> KeepSetAside(SessionTrees trees, KeepingRun run, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        var read = KeepingRecord.Read(home);
        var done = trees.KeepSetAside(Policy(home), Clock(), Budget, ct);
        lines.AddRange(SayPurged(done.Purged, log, met));
        lines.AddRange(SaySlimmed(done.Slims, log, met));
        var removed = done.Purged.Count(folder => folder.Purged) + done.Slims.Sum(slim => slim.Removed.Count);
        if (removed == 0 && done.Slims.All(slim => slim.Kept.Count == 0)) return lines;
        var kept = done.Purged.Where(folder => !folder.Purged).Select(folder => folder.Code ?? SetAsideKept.Unread)
            .Concat(done.Slims.SelectMany(slim => slim.Kept).Select(path => path.Why ?? SlimKept.Gone))
            .GroupBy(why => why, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var bytes = done.Purged.Sum(folder => folder.Bytes) + done.Slims.Sum(slim => slim.Bytes);
        if (KeepingRecord.WriteLast(home, read, KeepKinds.SetAside.Id, new KeepingLast(Clock(), run.Door, run.Id, removed, bytes, kept)) is { } refused)
        {
            lock (_gate)
            {
                if (First(met, "", KeepingRecord.PathOf(home), "unwritten")) lines.Add($"tidy  {Clause(refused)}.");
            }
        }

        return lines;
    }

    /// <summary>
    /// What the slim says (KEEP1a, the cleanup design §5): each folder's package folders dropped, with <c>tidied.slimmed</c>; and each
    /// folder or path it kept, and each removal that failed, said the first look it is met. The log names the folder from the home, as
    /// <c>tidied.purged</c> does, and counts; never a path inside it.
    /// </summary>
    private IReadOnlyList<string> SaySlimmed(IReadOnlyList<SlimResult> slims, MachineLog? log, HashSet<string> met)
    {
        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var slim in slims)
            {
                if (slim.Removed.Count > 0)
                {
                    lines.Add($"tidy  dropped the package folders of the folder moved aside to {slim.Folder}: {string.Join(", ", slim.Removed)} "
                        + $"({Size(slim.Bytes)}); `npm ci` beside each `package.json` makes them again, and the rest of it waits its days.");
                }

                if (slim.Said is { } said && First(met, "", slim.Folder, "slim-" + SlimKept.Written))
                {
                    lines.Add($"tidy  the folder moved aside to {slim.Folder} keeps its package folders for its days, since {Clause(said)}.");
                }

                foreach (var path in slim.Kept.Where(path => slim.Said is null))
                {
                    if (!First(met, "", $"{slim.Folder}/{path.Path}", "slim-" + path.Why)) continue;
                    lines.Add($"tidy  `{path.Path}` in the folder moved aside to {slim.Folder} stays for its days, since {KeptWhy(path.Why)}.");
                }

                foreach (var path in slim.Failed)
                {
                    if (!First(met, "", $"{slim.Folder}/{path.Path}", "slim-failed")) continue;
                    lines.Add($"tidy  `{path.Path}` in the folder moved aside to {slim.Folder} could not be removed yet ({Clause(path.Why ?? "")}); "
                        + "a later look tries again.");
                }

                if (slim.Unread is { } unread && First(met, "", slim.Folder, "slim-" + SetAsideKept.Unread))
                {
                    lines.Add($"tidy  the folder moved aside to {slim.Folder} keeps its package folders this look, since {Clause(unread)}.");
                }

                if (slim.Removed.Count > 0 || slim.Kept.Count > 0)
                {
                    log?.Info("tidied.slimmed",
                        ("folder", Path.GetRelativePath(home, slim.Folder).Replace('\\', '/')), ("removed", slim.Removed.Count), ("bytes", slim.Bytes),
                        ("kept", slim.Kept.Count));
                }
            }
        }

        return lines;
    }

    /// <summary>Why the slim kept a path, in a clause (<see cref="SlimKept"/>).</summary>
    private static string KeptWhy(string? why) => why switch
    {
        SlimKept.Linked => "a link lies on the way to it, or it is one",
        SlimKept.Refused => "its path does not name a package folder inside the folder",
        SlimKept.Gone => "it is no longer there as a folder",
        _ => "something in the folder was written after it was moved aside",
    };

    /// <summary>A count of bytes as a person reads it.</summary>
    private static string Size(long bytes) => bytes >= 1 << 20
        ? (bytes / 1048576.0).ToString("0.0 MB", System.Globalization.CultureInfo.InvariantCulture)
        : (bytes / 1024.0).ToString("0.0 KB", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The folders a review step in progress shows from (REVIEWENV1d): the tree of the session that said each set-up waiting
    /// for the person's verdict, read from the service's records as the review's gate reads them, whether or not this loop has
    /// a desk, since <i>Show it again</i> serves from that tree; and, where the shell has a desk, each build it serves to a tab
    /// now.
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> ReviewingAsync(ReviewDesk? desk, ServiceClient service, CancellationToken ct)
    {
        var paths = desk?.Tabs.Serving.Select(serve => serve.Folder).ToList() ?? [];
        var waiting = (await service.EveryQuestAsync(ct).ConfigureAwait(false))
            .Where(quest => quest.SetUpIn is not null && quest.Status != "Declined")
            .Select(quest => (Quest: quest, Newest: ReviewServed.Newest(quest)))
            .Where(each => each.Newest is { Session.Length: > 0 }
                           && ReviewServed.Answered(each.Quest, ReviewServed.Reference(each.Newest)) is null)
            .Select(each => each.Newest!.Session!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (waiting.Count > 0)
        {
            paths.AddRange((await service.SessionRecordsAsync(ct).ConfigureAwait(false))
                .Where(record => waiting.Contains(record.Id) && record.Tree is { Length: > 0 })
                .Select(record => record.Tree!));
        }

        return paths;
    }

    /// <summary>
    /// The tree each path lies in, by the layout this home chose (<c>trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>): a
    /// folder deep in a tree names the tree. A path outside the trees home is kept as it is, and holds no tree of Daoris's.
    /// </summary>
    internal static IReadOnlyList<string> TreesOf(string treesRoot, IEnumerable<string> paths)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(treesRoot));
        var named = new List<string>();
        foreach (var path in paths)
        {
            var full = Path.GetFullPath(path);
            var under = Path.GetRelativePath(root, full);
            var parts = under.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            named.Add(Path.IsPathRooted(under) || parts[0] == ".." || parts.Length < 3
                ? full
                : Path.Combine(root, parts[0], parts[1], parts[2]));
        }

        return named;
    }

    /// <summary>
    /// What a pass says: each removal, and each branch or repository kept for a reason the look meets for the first time, in
    /// the report's words and the log's codes.
    /// </summary>
    /// <param name="whole">Whether the pass looked at every repository, so what it did not meet again is forgotten.</param>
    internal IReadOnlyList<string> Said(IReadOnlyList<TidyPass> passes, MachineLog? log, bool whole = true)
    {
        var met = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = Say(passes, log, met);
        lock (_gate)
        {
            if (whole) _said = met;
            else _said.UnionWith(met);
        }

        return lines;
    }

    /// <summary>A repository whose pass threw, held unread with the code <see cref="TidyCause.Pass"/> (AUTOTIDY1a3); the console keeps the message.</summary>
    internal static TidyPass Threw(string repository, string? workspace, Exception error) =>
        new([], TidyKept.Unread, error.Message) { Repository = repository, Workspace = RemoteTarget.Workspace(workspace), Cause = TidyCause.Pass };

    /// <summary><see cref="Said"/>'s lines and log for some passes, each reason met added to <paramref name="met"/>.</summary>
    private IReadOnlyList<string> Say(IReadOnlyList<TidyPass> passes, MachineLog? log, HashSet<string> met)
    {
        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var pass in passes)
            {
                SayStrays(pass, log, met, lines);
                // CLEANUP1: a recovery ref past its retention, gone. The report alone names it: a ref carries a branch's name, which may
                // carry a title's words, so the machine log is not told (the machine-log design §5).
                foreach (var gone in pass.Recovered)
                {
                    lines.Add($"tidy  {pass.Repository}: removed the recovery ref `{gone.Ref}`, past its {RecoveryRetention.Kept.TotalDays:0} days: "
                        + $"`{ContentHolds.Named(gone.Holder)}` holds its commit {TextBounds.ShortCommit(gone.Commit, 8)}.");
                }

                if (pass.Held is { } held)
                {
                    if (!First(met, pass.Repository, "", held)) continue;
                    lines.Add($"tidy  {Held(pass)}");
                    log?.Warn("branch.kept", Kept(Name(pass.Repository), Name(pass.Workspace), null, held, pass.Cause));
                    continue;
                }

                foreach (var result in pass.Results)
                {
                    var item = result.Item;
                    if (result.Removed)
                    {
                        lines.Add($"tidy  {pass.Repository}: removed `{item.Branch}`, {Why(item)}"
                            + (result.MovedTo is { } to
                                ? $"; its tree's folder was moved aside to {to}, and goes once it is left untouched for {Policy(home).SetAside.Days.TotalDays:0} days."
                                // TREEFOLLOW1: the tree stays, on the branch its agent made; this one only said where it started.
                                : item.TreeOn is { } on && on != item.Where ? $"; the session's work is on `{on}`." : ".")
                            + (result.KeptRef is { } kept ? $" {ContentHold.KeptAt(item.Branch, kept)}" : ""));
                        // AUTOTIDY2a: `kind` and `kept` are fields added after the rest, so what read the line before reads it the same.
                        log?.Info("branch.tidied",
                            ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", item.Branch), ("tree", item.Tree is not null),
                            ("folder", result.MovedTo is { } moved ? Path.GetRelativePath(home, moved).Replace('\\', '/') : null),
                            ("on", item.TreeOn), ("kind", item.Kind), ("kept", result.KeptRef));
                        continue;
                    }

                    // Kept for what it holds (dirty, in use, a build's output) is the press's, and says nothing; kept by a last guard,
                    // because git could not read one, or because the removal did not happen, is said the first look it is met.
                    var (why, clause) = result.Kept is { } code ? (code, result.Message)
                        : item.Unread ? (TidyKept.Unread, item.Detail ?? "git could not say")
                        : result.Message == "kept" ? (null, null) : (TidyKept.Refused, result.Message);
                    if (why is null || !First(met, pass.Repository, item.Branch, why)) continue;
                    lines.Add($"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(clause!)}.");
                    log?.Warn("branch.kept", Kept(pass.Repository, pass.Workspace, item.Branch, why, why == TidyKept.Unread ? result.Cause ?? (item.Unread ? TidyCause.Judged : null) : null));
                }

                SayLanded(pass, log, met, lines);
            }
        }

        return lines;
    }

    /// <summary>
    /// What the look did with the branches the repository's landings made (AUTOTIDY2b): each removal, and each the removal of
    /// which did not happen, said the first look it is met. The report names the branch; the machine log names it by its session
    /// alone, and never the ref its commits stay at, since both carry the title's words (the machine-log design §5).
    /// </summary>
    private void SayLanded(TidyPass pass, MachineLog? log, HashSet<string> met, List<string> lines)
    {
        foreach (var result in pass.Landed)
        {
            var item = result.Item;
            if (result.Removed)
            {
                lines.Add($"tidy  {pass.Repository}: removed `{item.Branch}`, a branch a landing made, {LandedWhy(item)}."
                    + (result.KeptRef is { } kept ? $" {ContentHold.KeptAt(item.Branch, kept)}" : ""));
                log?.Info("branch.tidied",
                    ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", null), ("tree", false), ("folder", null),
                    ("on", null), ("kind", item.Kind), ("kept", null), ("session", result.Session));
                continue;
            }

            // A branch the look did not name is the press's (its own judgement keeps it), and says nothing.
            if (result.Message == "kept" || !First(met, pass.Repository, item.Branch, TidyKept.Refused)) continue;
            lines.Add($"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(result.Message)}.");
            log?.Warn("branch.kept", [.. Kept(pass.Repository, pass.Workspace, null, TidyKept.Refused, null), ("session", result.Session)]);
        }
    }

    /// <summary>
    /// What the look did with the folders under the repository's trees that are no tree of its own (AUTOTIDY2c, OPENCUT1): each
    /// moved aside, and each kept, said the first look it is met. The machine log names a folder moved aside by where it went, from
    /// the home, as it names a tree's (<c>branch.tidied</c>'s <c>folder</c>), with no branch; a cut-off opening by its branch,
    /// Daoris's own name. Neither carries a sentence.
    /// </summary>
    private void SayStrays(TidyPass pass, MachineLog? log, HashSet<string> met, List<string> lines)
    {
        foreach (var stray in pass.Strays)
        {
            var name = Path.GetFileName(stray.Folder);
            if (stray.Moved)
            {
                var goes = $"was moved aside to {stray.MovedTo}, and goes once it is left untouched for {Policy(home).SetAside.Days.TotalDays:0} days.";
                lines.Add(stray.Kind != StrayKind.Opening
                    ? $"tidy  {pass.Repository}: `{name}`, a folder under its trees that git knows no tree in, {goes}"
                    : stray is { Branch: { } branch, BranchKept: null }
                        ? $"tidy  {pass.Repository}: removed `{branch}`, whose tree's opening was cut off: {stray.Message}; its tree's folder {goes}"
                        : $"tidy  {pass.Repository}: the tree `{name}`, whose opening was cut off ({stray.Message}), {goes}"
                          + (stray.BranchKept is { } why ? $" Its branch `{stray.Branch}` stays, since {Clause(why)}." : ""));
                log?.Info("branch.tidied",
                    ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", stray.BranchKept is null ? stray.Branch : null),
                    ("tree", true), ("folder", stray.MovedTo is { } to ? Path.GetRelativePath(home, to).Replace('\\', '/') : null),
                    ("on", null), ("kind", stray.Kind), ("kept", null));
                continue;
            }

            var code = stray.Kept ?? TidyKept.Refused;
            if (!First(met, pass.Repository, stray.Branch ?? stray.Folder, code)) continue;
            lines.Add(stray.Branch is { } kept
                ? $"tidy  {pass.Repository}: `{kept}` stays, since {Clause(stray.Message)}."
                : $"tidy  {pass.Repository}: the folder `{name}` under its trees stays, since {Clause(stray.Message)}.");
            log?.Warn("branch.kept", Kept(pass.Repository, pass.Workspace, stray.Branch, code, code == TidyKept.Unread ? stray.Cause : null));
        }
    }

    /// <summary>Why a landing's branch could go, by what proved its work is on the line, as the clean-up's list words it (AUTOTIDY2b).</summary>
    private static string LandedWhy(LandedItem item) => item.Kind switch
    {
        LandedKind.OnLine => $"since its work is on `{item.Where}` by content (each file it changed reads the same there)",
        LandedKind.Inside => $"since it is inside `{item.Where}`, whose work is on the line",
        LandedKind.PullRequest => $"since its pull request completed and its merge commit is on `{item.Where}`",
        _ => $"since its work is on `{item.Where}`",
    };

    /// <summary>
    /// <c>branch.kept</c>'s data: the cause (AUTOTIDY1a2) is a field only where there is one, so what was read before it reads the same.
    /// The code only, never the sentence's path (D47 §4).
    /// </summary>
    private static (string Key, object? Value)[] Kept(string repository, string workspace, string? branch, string why, string? cause)
    {
        (string Key, object? Value)[] data = [("repository", repository), ("workspace", workspace), ("branch", branch), ("why", why)];
        return cause is null ? data : [.. data, ("cause", cause)];
    }

    /// <summary>What the purge of the folders moved aside says: each deleted, and each kept for a reason met the first time.</summary>
    private IReadOnlyList<string> SayPurged(IReadOnlyList<SetAside> folders, MachineLog? log, HashSet<string> met)
    {
        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var folder in folders)
            {
                var named = Path.GetRelativePath(home, folder.Folder).Replace('\\', '/');
                var on = folder.Moved.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (folder.Purged)
                {
                    lines.Add($"tidy  deleted the folder moved aside to {folder.Folder} on {on}: nothing was written in it since.");
                    log?.Info("tidied.purged", ("folder", named), ("days", (int)(Clock() - folder.Moved).TotalDays));
                    continue;
                }

                if (!First(met, "", folder.Folder, folder.Code ?? SetAsideKept.Unread)) continue;
                lines.Add($"tidy  the folder moved aside to {folder.Folder} on {on} stays, since {Clause(folder.Kept ?? "")}. "
                    + "Look at it, and delete it once it holds nothing you need.");
                log?.Warn("tidied.kept", ("folder", named), ("why", folder.Code ?? SetAsideKept.Unread));
            }
        }

        return lines;
    }

    /// <summary>Whether this reason is met for the first time while it lasts; it is counted as met either way.</summary>
    private bool First(HashSet<string> met, string repository, string branch, string why)
    {
        var key = $"{repository}\n{branch}\n{why}";
        met.Add(key);
        return !_said.Contains(key);
    }

    /// <summary>What held a whole repository this look, in a sentence after its name.</summary>
    private static string Held(TidyPass pass) => pass.Held switch
    {
        TidyKept.Starting => $"{pass.Repository}: {TreeLock.Starting(pass.Repository)}.",
        TidyKept.Sessions when pass.Repository.Length == 0 =>
            $"the session branches were not looked at, since the service did not answer for its registry: {Clause(pass.Detail ?? "")}.",
        TidyKept.Sessions => $"{pass.Repository}: its session branches stay this look, since the service did not say which "
            + $"sessions run: {Clause(pass.Detail ?? "")}.",
        _ => $"{pass.Repository}: its session branches stay this look, since git could not list them: {Clause(pass.Detail ?? "")}.",
    };

    /// <summary>
    /// Why a removed branch could go, by its kind, as the clean-up's list words it (AUTOTIDY2a): nothing beyond the line, its work
    /// on a branch of the person's or on the line by content, or carried by a completed pull request.
    /// </summary>
    private static string Why(SweepItem item) => item.Kind switch
    {
        SweepKind.Landed when item.HeldBy is { } held => $"since {held.Said}",
        SweepKind.Landed => item.Where is { } where ? $"since its work is on `{where}`" : "since its work is on a branch of yours",
        SweepKind.Carried => $"since `{item.Where}`'s completed pull request carried its work",
        _ => $"which held nothing beyond `{item.Where}`",
    };

    /// <summary>The press's sentence as a clause: its own "and is kept" and its full stop are the line's to say.</summary>
    private static string Clause(string sentence) =>
        sentence.Replace(", and is kept", "", StringComparison.Ordinal).Replace(", so it is kept", "", StringComparison.Ordinal).Trim().TrimEnd('.');

    private static string? Name(string text) => text.Length == 0 ? null : text;
}
