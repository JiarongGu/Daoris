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

    /// <summary>
    /// How long after the person's last word to a session the look's tidy leaves its tree (TREEGONE2). No window of a session's fits: the
    /// watch's quiet is a running turn's silence, and a stray folder's quiet is writes. The pause seen was four minutes, under one pace;
    /// an hour covers a person stepping away, and past it the tree may go, since the next word goes on with Daoris's brief in a fresh one.
    /// </summary>
    public static readonly TimeSpan SpokenWithin = TimeSpan.FromHours(1);

    private readonly object _gate = new();

    // What was said and is still so, by repository, branch and code: said again only after a pass where it was not.
    private HashSet<string> _said = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _began;

    /// <summary>How often a pass looks, at most; a test's is zero.</summary>
    public TimeSpan Pace { get; init; } = DefaultPace;

    /// <summary>
    /// The look's classing (NOTIFY2h2), by which each line is classed as it is made: what the tidy removed or kept, which the session's
    /// page, its branch's line and This Daoris's cards say, is what moved; what it could not do now, a later look's. None by default.
    /// </summary>
    public LookSaid Look { get; init; } = LookSaid.None;

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
    /// The folders a review step in progress shows from (<see cref="ReviewServed.ReviewingAsync"/>), asked with the sessions once a
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

            lines.AddRange(await Keeping.KeepAsync(trees, service, log, met, ct).ConfigureAwait(false));
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

        Task<IReadOnlySet<string>> InUseAsync(CancellationToken token) => BranchTidying.InUseAsync(home, service, reviewing, token, Clock());
    }

    public static Task<IReadOnlySet<string>> InUseAsync(
        string home, ServiceClient service, Func<CancellationToken, Task<IReadOnlyCollection<string>>>? reviewing, CancellationToken ct,
        DateTimeOffset? now = null) => TidyHolders.InUseAsync(home, service, reviewing, ct, now);

    public static Task<HashSet<string>> HeldTreesAsync(ServiceClient service, CancellationToken ct) => TidyHolders.HeldTreesAsync(service, ct);

    public static Task<IReadOnlyList<TreeHolder>> HoldersAsync(ServiceClient service, CancellationToken ct) => TidyHolders.HoldersAsync(service, ct);

    public static Task<IReadOnlyList<TreeHolder>> BesideAsync(ServiceClient service, CancellationToken ct) => TidyHolders.BesideAsync(service, ct);

    public static IEnumerable<string> LineTrees(IEnumerable<PriorSession> records, IEnumerable<QuestView> quests) => TidyHolders.LineTrees(records, quests);

    public static IEnumerable<string> LineWaiters(IEnumerable<PriorSession> records, IEnumerable<QuestView> quests) => TidyHolders.LineWaiters(records, quests);

    public static Task<IEnumerable<string>> WaitingOnHelpersAsync(ServiceClient service, CancellationToken ct) => TidyHolders.WaitingOnHelpersAsync(service, ct);

    private TidyKeeping? _keeping;

    private TidyKeeping Keeping => _keeping ??= new(this, home);

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

    /// <summary>
    /// The card's sizes the Keeping pass counts by itself (KEEP1c): the watch names the process's own; null, a test's tidy, counts none.
    /// </summary>
    public SpaceSizes? Sizes { get; init; }

    /// <summary>The running sessions, as the downloads' rule reads them; a test hands its own, as it hands a clock.</summary>
    internal Func<ServiceClient, CancellationToken, Task<LiveSessions>>? Sessions { get; init; }

    /// <summary>
    /// This machine's session processes, which history's daily clear judges a live session by (KEEP1g); the watch names the loop's, and
    /// none reads the home's markers.
    /// </summary>
    public SessionProcesses? Processes { get; init; }

    /// <summary>The loop's record of conversations, so history's daily clear forgets a cleared one's numbering (KEEP1g); null removes the files alone.</summary>
    public ISessionEventRemoval? Events { get; init; }

    /// <summary>
    /// The shell's browser host, which says whether Daoris's own browser runs and holds its start through a clear of its caches (KEEP1f's
    /// third act); null with no shell, which keeps them.
    /// </summary>
    public IBrowserCaches? Caches { get; init; }

    /// <summary>The stretches below the floor this process keeps (KEEP1f); a test hands its own.</summary>
    internal SpaceFloors Floors { get; init; } = SpaceFloors.Shared;

    /// <summary>
    /// A folder's volume (KEEP1f): the watch names the system's read (<see cref="SpaceVolumes.Read"/>), a test its stand-in; null, a test's
    /// tidy that is about something else, runs no floor, so its lines never hang on the machine's own free space.
    /// </summary>
    public Func<string, VolumeReading?>? Volume { get; init; }

    /// <summary>How often the downloads' rule removes what is past its days (the cleanup design §3.1, step 5).</summary>
    public static readonly TimeSpan DownloadsPace = TimeSpan.FromDays(1);

    /// <summary>A count of bytes as a person reads it.</summary>
    internal static string Size(long bytes) => bytes >= 1 << 20
        ? (bytes / 1048576.0).ToString("0.0 MB", System.Globalization.CultureInfo.InvariantCulture)
        : (bytes / 1024.0).ToString("0.0 KB", System.Globalization.CultureInfo.InvariantCulture);

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
                    lines.Add(Look.Progress("tidy", $"tidy  {pass.Repository}: removed the recovery ref `{gone.Ref}`, past its {RecoveryRetention.Kept.TotalDays:0} days: "
                        + $"`{ContentHolds.Named(gone.Holder)}` holds its commit {TextBounds.ShortCommit(gone.Commit, 8)}."));
                }

                if (pass.Held is { } held)
                {
                    if (!First(met, pass.Repository, "", held)) continue;
                    // What held the whole repository this look (a start in a tree, a service not answering): a later look's.
                    lines.Add(Look.Retry("tidy", $"tidy  {Held(pass)}"));
                    log?.Warn("branch.kept", Kept(Name(pass.Repository), Name(pass.Workspace), null, held, pass.Cause));
                    continue;
                }

                foreach (var result in pass.Results)
                {
                    var item = result.Item;
                    if (result.Removed)
                    {
                        lines.Add(Look.Progress("tidy", $"tidy  {pass.Repository}: removed `{item.Branch}`, {Why(item)}"
                            + (result.MovedTo is { } to
                                ? $"; its tree's folder was moved aside to {to}, and goes once it is left untouched for {Policy(home).SetAside.Days.TotalDays:0} days."
                                // TREEFOLLOW1: the tree stays, on the branch its agent made; this one only said where it started.
                                : item.TreeOn is { } on && on != item.Where ? $"; the session's work is on `{on}`." : ".")
                            + (result.KeptRef is { } kept ? $" {ContentHold.KeptAt(item.Branch, kept)}" : "")));
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
                    // One git could not read is read again at a later look; one a guard kept, its session's page and its branch's line say.
                    lines.Add(Look.Of(why == TidyKept.Unread ? LookClass.Retry : LookClass.Progress, "tidy", $"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(clause!)}."));
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
                lines.Add(Look.Progress("tidy", $"tidy  {pass.Repository}: removed `{item.Branch}`, a branch a landing made, {LandedWhy(item)}."
                    + (result.KeptRef is { } kept ? $" {ContentHold.KeptAt(item.Branch, kept)}" : "")));
                log?.Info("branch.tidied",
                    ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", null), ("tree", false), ("folder", null),
                    ("on", null), ("kind", item.Kind), ("kept", null), ("session", result.Session));
                continue;
            }

            // A branch the look did not name is the press's (its own judgement keeps it), and says nothing.
            if (result.Message == "kept" || !First(met, pass.Repository, item.Branch, TidyKept.Refused)) continue;
            lines.Add(Look.Progress("tidy", $"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(result.Message)}."));
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
                lines.Add(Look.Progress("tidy", stray.Kind != StrayKind.Opening
                    ? $"tidy  {pass.Repository}: `{name}`, a folder under its trees that git knows no tree in, {goes}"
                    : stray is { Branch: { } branch, BranchKept: null }
                        ? $"tidy  {pass.Repository}: removed `{branch}`, whose tree's opening was cut off: {stray.Message}; its tree's folder {goes}"
                        : $"tidy  {pass.Repository}: the tree `{name}`, whose opening was cut off ({stray.Message}), {goes}"
                          + (stray.BranchKept is { } why ? $" Its branch `{stray.Branch}` stays, since {Clause(why)}." : "")));
                log?.Info("branch.tidied",
                    ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", stray.BranchKept is null ? stray.Branch : null),
                    ("tree", true), ("folder", stray.MovedTo is { } to ? Path.GetRelativePath(home, to).Replace('\\', '/') : null),
                    ("on", null), ("kind", stray.Kind), ("kept", null));
                continue;
            }

            var code = stray.Kept ?? TidyKept.Refused;
            if (!First(met, pass.Repository, stray.Branch ?? stray.Folder, code)) continue;
            lines.Add(Look.Of(code == TidyKept.Unread ? LookClass.Retry : LookClass.Progress, "tidy", stray.Branch is { } kept
                ? $"tidy  {pass.Repository}: `{kept}` stays, since {Clause(stray.Message)}."
                : $"tidy  {pass.Repository}: the folder `{name}` under its trees stays, since {Clause(stray.Message)}."));
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
    internal IReadOnlyList<string> SayPurged(IReadOnlyList<SetAside> folders, MachineLog? log, HashSet<string> met)
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
                    lines.Add(Look.Progress("tidy", $"tidy  deleted the folder moved aside to {folder.Folder} on {on}: nothing was written in it since."));
                    log?.Info("tidied.purged", ("folder", named), ("days", (int)(Clock() - folder.Moved).TotalDays));
                    continue;
                }

                if (!First(met, "", folder.Folder, folder.Code ?? SetAsideKept.Unread)) continue;
                lines.Add(Look.Progress("tidy", $"tidy  the folder moved aside to {folder.Folder} on {on} stays, since {Clause(folder.Kept ?? "")}. "
                    + "Look at it, and delete it once it holds nothing you need."));
                log?.Warn("tidied.kept", ("folder", named), ("why", folder.Code ?? SetAsideKept.Unread));
            }
        }

        return lines;
    }

    /// <summary>Whether this reason is met for the first time while it lasts; it is counted as met either way.</summary>
    internal bool First(HashSet<string> met, string repository, string branch, string why)
    {
        var key = $"{repository}\n{branch}\n{why}";
        met.Add(key);
        return !_said.Contains(key);
    }

    /// <summary>What held a whole repository this look, in a sentence after its name.</summary>
    private static string Held(TidyPass pass) => pass.Held switch
    {
        TidyKept.Starting => $"{pass.Repository}: {TreeLock.Starting(pass.Repository, pass.Detail)}.",
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
    internal static string Clause(string sentence) =>
        sentence.Replace(", and is kept", "", StringComparison.Ordinal).Replace(", so it is kept", "", StringComparison.Ordinal).Trim().TrimEnd('.');

    private static string? Name(string text) => text.Length == 0 ? null : text;
}
