using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's own tidy of empty session branches (AUTOTIDY1, D88's note): a session branch that holds nothing beyond the line,
/// with nothing in its tree, goes without a press, and the look says so in its report and the machine log. Held by the watch
/// across its looks, since each look's driver is built fresh: what it already said, and when it last looked.
/// </summary>
/// <remarks>
/// <para><b>The press's path, not a second one.</b> Each repository is tidied by <see cref="SessionTrees.TidyEmptyAsync"/>,
/// which runs the clean-up's own path over the few branches the line holds, taking only the empty kind
/// (<see cref="SessionTrees.GoesByItself"/>). Every guard is the press's: judged again right before it goes, deleted only
/// while it is the commit judged, never forced. The look adds its last guards, and never deletes a tree's folder: it moves it
/// aside, and a later look deletes it only once it has waited <see cref="SetAsideFor"/> with nothing written in it since.</para>
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
                try
                {
                    pass = await trees.TidyEmptyAsync(row.Root!, row.Repository, row.Workspace, InUseAsync, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
                {
                    pass = new TidyPass([], TidyKept.Unread, error.Message)
                    {
                        Repository = row.Repository, Workspace = RemoteTarget.Workspace(row.Workspace),
                    };
                }

                // Said as each repository is done: a removal is written though the look is closed before the next.
                lines.AddRange(Say([pass], log, met));
            }

            lines.AddRange(SayPurged(trees.PurgeSetAside(Clock(), SetAsideFor), log, met));
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

        // A session running or waiting holds its tree; so does a review step in progress, by the folder it shows from.
        async Task<IReadOnlySet<string>> InUseAsync(CancellationToken token)
        {
            var held = (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (reviewing is not null) held.UnionWith(TreesOf(new SessionTrees(home).TreesRoot, await reviewing(token).ConfigureAwait(false)));
            return held;
        }
    }

    /// <summary>How long a folder the look moved aside waits before a later look deletes it, where nothing was written in it since.</summary>
    public static readonly TimeSpan SetAsideFor = TimeSpan.FromDays(14);

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

    /// <summary><see cref="Said"/>'s lines and log for some passes, each reason met added to <paramref name="met"/>.</summary>
    private IReadOnlyList<string> Say(IReadOnlyList<TidyPass> passes, MachineLog? log, HashSet<string> met)
    {
        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var pass in passes)
            {
                if (pass.Held is { } held)
                {
                    if (!First(met, pass.Repository, "", held)) continue;
                    lines.Add($"tidy  {Held(pass)}");
                    log?.Warn("branch.kept", ("repository", Name(pass.Repository)), ("workspace", Name(pass.Workspace)), ("branch", null), ("why", held));
                    continue;
                }

                foreach (var result in pass.Results)
                {
                    var item = result.Item;
                    if (result.Removed)
                    {
                        lines.Add($"tidy  {pass.Repository}: removed `{item.Branch}`, which held nothing beyond `{item.Where}`"
                            + (result.MovedTo is { } to
                                ? $"; its tree's folder was moved aside to {to}, and goes once it is left untouched for {SetAsideFor.TotalDays:0} days."
                                : "."));
                        log?.Info("branch.tidied",
                            ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", item.Branch), ("tree", item.Tree is not null),
                            ("folder", result.MovedTo is { } moved ? Path.GetRelativePath(home, moved).Replace('\\', '/') : null));
                        continue;
                    }

                    // Kept for what it holds (dirty, in use, a build's output) is the press's, and says nothing; kept by a last guard,
                    // because git could not read one, or because the removal did not happen, is said the first look it is met.
                    var (why, clause) = result.Kept is { } code ? (code, result.Message)
                        : item.Unread ? (TidyKept.Unread, item.Detail ?? "git could not say")
                        : result.Message == "kept" ? (null, null) : (TidyKept.Refused, result.Message);
                    if (why is null || !First(met, pass.Repository, item.Branch, why)) continue;
                    lines.Add($"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(clause!)}.");
                    log?.Warn("branch.kept", ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", item.Branch), ("why", why));
                }
            }
        }

        return lines;
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
        TidyKept.Sessions => $"{pass.Repository}: its empty session branches stay this look, since the service did not say which "
            + $"sessions run: {Clause(pass.Detail ?? "")}.",
        _ => $"{pass.Repository}: its empty session branches stay this look, since git could not list them: {Clause(pass.Detail ?? "")}.",
    };

    /// <summary>The press's sentence as a clause: its own "and is kept" and its full stop are the line's to say.</summary>
    private static string Clause(string sentence) =>
        sentence.Replace(", and is kept", "", StringComparison.Ordinal).Replace(", so it is kept", "", StringComparison.Ordinal).Trim().TrimEnd('.');

    private static string? Name(string text) => text.Length == 0 ? null : text;
}
