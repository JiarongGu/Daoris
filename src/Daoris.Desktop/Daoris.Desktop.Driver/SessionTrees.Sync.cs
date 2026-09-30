namespace Daoris.Driver;

/// <summary>What pulling a repository's line would do, or found (WSR6). Only a fast-forward moves anything.</summary>
public static class PullKind
{
    /// <summary>The local line would move forward to origin's, and nothing of its own would be lost.</summary>
    public const string FastForward = "fast-forward";

    /// <summary>The local line already holds origin's.</summary>
    public const string UpToDate = "up-to-date";

    /// <summary>The local line holds commits origin lacks: nothing to pull, and Daoris never pushes.</summary>
    public const string Ahead = "ahead";

    /// <summary>Each holds commits the other lacks: a fast-forward cannot take it, and nothing else is Daoris's to do.</summary>
    public const string Diverged = "diverged";

    /// <summary>The repository's own checkout is on the line, with uncommitted work.</summary>
    public const string Dirty = "dirty";

    /// <summary>The line is checked out in a working tree that is not the repository's own checkout.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>There is no <c>origin/&lt;line&gt;</c> here to pull from.</summary>
    public const string NoRemote = "no-remote";

    /// <summary>There is no local branch of the line: nothing to move, and the line is read from origin.</summary>
    public const string NoLocal = "no-local";

    /// <summary>No line is set and git names none.</summary>
    public const string NoLine = "no-line";

    /// <summary>Git could not say.</summary>
    public const string Unknown = "unknown";
}

/// <param name="From">The local line's commit, where it has one.</param>
/// <param name="To">Origin's commit it would move to, where there is one.</param>
/// <param name="Commits">Fast-forward: the commits it would take. Ahead: those origin lacks. Diverged: those the local line lacks.</param>
/// <param name="Fetch">Why the fetch did not happen, in git's words, or null where it did or none was asked for.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
public sealed record LinePull(
    string Repository, string Workspace, string? Line, string Kind, string? From, string? To, int Commits, string? Fetch, string? Detail)
{
    public bool Moves => Kind == PullKind.FastForward;
}

/// <summary>What bringing one branch up to date would do, or found (WSR6). Only a replay moves anything.</summary>
public static class RebaseKind
{
    /// <summary>Its own commits would be replayed onto the line, cutting at the commit it grew from.</summary>
    public const string Replay = "replay";

    /// <summary>It already holds the line's tip.</summary>
    public const string UpToDate = "up-to-date";

    /// <summary>A session still running or waiting holds its tree.</summary>
    public const string InUse = "in-use";

    /// <summary>Its tree has uncommitted work.</summary>
    public const string Dirty = "dirty";

    /// <summary>It is checked out in a working tree that is not a session's own: the person's checkout, or a landing's branch anywhere.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>It is on its remote: replaying it would need a force push, which is never Daoris's.</summary>
    public const string Pushed = "pushed";

    /// <summary>It grew from a commit beyond the line — the step before's — whose work has not reached the line.</summary>
    public const string Waits = "grew-from-unlanded";

    /// <summary>Git could not say.</summary>
    public const string Unknown = "unknown";
}

/// <summary>How the commit a branch's own work starts after was found (WSR6).</summary>
public static class CutBy
{
    /// <summary>It grew from the line: where it leaves the line.</summary>
    public const string Line = "line";

    /// <summary>The commit its record says it started at, beyond the line, whose work reached the line.</summary>
    public const string Record = "record";

    /// <summary>No record: the newest commit of it whose work reads on the line.</summary>
    public const string Content = "content";
}

/// <param name="Landed">A branch a landing made and recorded (WSR5), rather than a session's own.</param>
/// <param name="Onto">The line it is brought up to.</param>
/// <param name="Cut">The commit its own commits start after, where it is replayed; null otherwise.</param>
/// <param name="CutBy">How that commit was found (<see cref="Driver.CutBy"/>).</param>
/// <param name="GrewFrom">The step before's branch it started on, where its record knows one.</param>
/// <param name="Commits">Replay: its own commits. Pushed and moved since: the commits its remote lacks.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
public sealed record RebaseItem(
    string Repository, string Workspace, string Branch, bool Landed, string Kind, string? Onto, string? Cut, string? CutBy,
    string? GrewFrom, int Commits, string? Detail)
{
    public bool Replays => Kind == RebaseKind.Replay;
}

/// <summary>A repository with a checkout here, and whether it holds a branch of Daoris's (WSR7, D112).</summary>
/// <param name="Holds">A session branch (`daoris/…`), or a branch a landing recorded that still stands.</param>
public sealed record SyncRepository(string Repository, string Workspace, bool Holds);

/// <summary>
/// Which repositories bringing up to date takes (WSR7, D112): by default those holding a branch of Daoris's, and every
/// other one only where the person includes it — all of them (`--all`), or by name (`--repository`, the screen's tick).
/// </summary>
public sealed record SyncScope
{
    /// <summary>The default: the repositories holding a branch of Daoris's, and no other.</summary>
    public static SyncScope Held { get; } = new();

    /// <summary>Every repository with a checkout here.</summary>
    public static SyncScope Everything { get; } = new() { All = true };

    /// <summary>The default and the repositories named, whatever their case.</summary>
    public static SyncScope Named(IEnumerable<string> repositories) =>
        new() { Also = repositories.ToHashSet(StringComparer.OrdinalIgnoreCase) };

    public bool All { get; init; }

    /// <summary>Repositories taken beyond the default, compared without case.</summary>
    public IReadOnlySet<string> Also { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool Includes(SyncRepository repository) => repository.Holds || All || Also.Contains(repository.Repository);

    /// <summary>
    /// This scope and each repository a row the person saw listed names (`repository:branch`, the key a press takes): the
    /// press acts on what it was shown, so a repository included at the look is included at the press.
    /// </summary>
    public SyncScope Listed(IReadOnlySet<string>? only) => only is null ? this : this with
    {
        Also = Also.Concat(only.Select(key => key.IndexOf(':') is var colon and > 0 ? key[..colon] : key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase),
    };
}

/// <summary>Bringing repositories up to date (WSR6), listed first: each line's pull, each branch's replay, and the landed branches that go.</summary>
public sealed record SyncPlan(IReadOnlyList<LinePull> Lines, IReadOnlyList<RebaseItem> Rebases, IReadOnlyList<LandedItem> Deletes)
{
    /// <summary>The repositories the look took (WSR7, D112), each with whether it holds a branch of Daoris's.</summary>
    public IReadOnlyList<SyncRepository> Looked { get; init; } = [];

    /// <summary>Every other repository with a checkout here: not fetched, not judged, listed for the person to include.</summary>
    public IReadOnlyList<SyncRepository> Apart { get; init; } = [];
}

/// <summary>What the press did with one repository's line.</summary>
public sealed record PullResult(LinePull Pull, bool Moved, string Message);

/// <summary>What the press did with one branch.</summary>
public sealed record RebaseResult(RebaseItem Item, bool Replayed, string Message);

/// <summary>What one press of bringing up to date did, all three steps.</summary>
public sealed record SyncDone(IReadOnlyList<PullResult> Lines, IReadOnlyList<RebaseResult> Rebases, IReadOnlyList<LandedResult> Deletes)
{
    /// <summary>The repositories with a checkout here the press did not take (WSR7, D112): nothing of them was touched.</summary>
    public IReadOnlyList<SyncRepository> Apart { get; init; } = [];
}

/// <summary>The rows in words — the terminal's lines, and the sentences the screen's rows say in their own catalogue.</summary>
public static class SyncWords
{
    public static string Describe(LinePull pull) => $"{pull.Repository}  {pull.Line ?? "(no line)"}  " + pull.Kind switch
    {
        PullKind.FastForward => $"fast-forwards {pull.Commits} commit(s) to `origin/{pull.Line}`",
        PullKind.UpToDate => $"up to date with `origin/{pull.Line}`",
        PullKind.Ahead => $"{pull.Commits} commit(s) `origin/{pull.Line}` does not have: nothing to pull, and Daoris never pushes",
        PullKind.Diverged => $"it and `origin/{pull.Line}` each hold commits the other lacks: only a fast-forward is Daoris's",
        PullKind.Dirty => "the repository's own checkout is on it with uncommitted work, so it stays",
        PullKind.CheckedOut => "checked out in another working tree, which Daoris does not move",
        PullKind.NoRemote => $"no `origin/{pull.Line}` here to pull from",
        PullKind.NoLocal => $"no local `{pull.Line}` to move: the line is read from `origin/{pull.Line}`",
        PullKind.NoLine => "no line is set and git names none",
        _ => $"git could not tell: {pull.Detail}",
    } + (pull.Fetch is { } fetch ? $" (not fetched: {fetch})" : "");

    public static string Describe(RebaseItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        RebaseKind.Replay when item.Commits == 0 => $"moves onto `{item.Onto}`: it holds nothing of its own beyond work that reached the line",
        RebaseKind.Replay => $"replays {item.Commits} commit(s) of its own onto `{item.Onto}`" + item.CutBy switch
        {
            CutBy.Record => item.GrewFrom is { } from ? $", after `{from}`'s work, which reached the line" : ", after the work it grew from, which reached the line",
            CutBy.Content => ", after its newest commit whose work reads on the line",
            _ => "",
        },
        RebaseKind.UpToDate => $"already on `{item.Onto}`",
        RebaseKind.InUse => "a session still running or waiting holds its tree",
        RebaseKind.Dirty => "its tree has uncommitted work",
        RebaseKind.CheckedOut => "checked out in a working tree that is not a session's own",
        RebaseKind.Pushed => "on its remote: replaying it would need a force push",
        RebaseKind.Waits => (item.GrewFrom is { } parent ? $"it grew from `{parent}`" : "it grew from a commit beyond the line")
            + ", whose work has not reached the line yet",
        _ => $"git could not tell: {item.Detail}",
    };

    /// <summary>The repositories a look or a press did not take (WSR7, D112), in one line, and how to include them; null for none.</summary>
    public static string? Apart(IReadOnlyList<SyncRepository> apart) => apart.Count == 0
        ? null
        : $"trees: not looked at, since they hold no branch of Daoris's ({apart.Count}): "
          + string.Join(", ", apart.Select(each => each.Repository))
          + ". `--all` includes them, and `--repository <name>` one of them.";
}

/// <summary>
/// How long bringing up to date may take over each step (WSR6, WSR7), and how many fetches run at once. Public because
/// the page spells them again, to wait as long as the host may work: `bridge/call.ts`'s `hostBounds`, held to these by
/// `SyncBoundsTests`. A page that waited its bridge's default 30 seconds gave up on a look that ran for minutes.
/// </summary>
public static class SyncBounds
{
    /// <summary>A fetch is a round trip a person is waiting on, as a plugin's push is (D100).</summary>
    public static readonly TimeSpan Fetch = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How many repositories are fetched at once (WSR7): a look waits about one fetch's time rather than one per
    /// repository, and a small number asks little of the machine's connection and of a remote that limits its callers.
    /// </summary>
    public const int FetchesAtOnce = 4;

    /// <summary>A replay is local, but a signing prompt nobody answers must not hold the press for ever.</summary>
    public static readonly TimeSpan Replay = TimeSpan.FromMinutes(5);
}

public sealed partial class SessionTrees
{
    /// <summary>What the press says of a row that is no longer what the person saw listed.</summary>
    private const string LeftSince = "it changed since the list, and is left as it was";

    /// <summary>
    /// A credential prompt with nobody at a terminal would wait for ever: git is told to fail instead. The person's
    /// credential helper still answers, since the fetch runs as them.
    /// </summary>
    private static readonly Dictionary<string, string> NoPrompt = new(StringComparer.Ordinal) { ["GIT_TERMINAL_PROMPT"] = "0" };

    /// <summary>
    /// Bringing repositories up to date after a pull request merged (WSR6), listed first: fetch each line from
    /// <c>origin</c> where asked, then say what the press would do — the line's fast-forward, each branch's replay
    /// onto it, and each landed branch whose work reached it. No branch of the person's moves.
    /// </summary>
    /// <param name="fetch">Whether to fetch each line first. A fetch moves only origin's own refs here.</param>
    /// <param name="scope">
    /// Which repositories to take (WSR7, D112); null for the default, those holding a branch of Daoris's. Every other one
    /// comes back in <see cref="SyncPlan.Apart"/>, not fetched and not judged.
    /// </param>
    public async Task<SyncPlan> SyncPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        bool fetch = true, CancellationToken ct = default, SyncScope? scope = null)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var lines = new List<LinePull>();
        var rebases = new List<RebaseItem>();
        var deletes = new List<LandedItem>();
        var (looked, apart) = await ScopedAsync(repositories, scope ?? SyncScope.Held, ct).ConfigureAwait(false);
        // The network first, a few repositories at a time (WSR7); then each judged in turn, from what it fetched.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);
        foreach (var ((repository, space, root, _), (line, fetched)) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);
            var (pull, _) = await JudgePullAsync(root, repository, workspace, line, fetched, ct).ConfigureAwait(false);
            lines.Add(pull);

            // What the line will be after the press: origin's tip where the pull moves it, else where it stands.
            var onto = pull.Moves ? pull.To : await OntoAsync(root, line, ct).ConfigureAwait(false);
            var (judged, gone) = await JudgeBranchesAsync(root, repository, workspace, line, onto, busy, ct).ConfigureAwait(false);
            rebases.AddRange(judged.Select(each => each.Item));
            deletes.AddRange(gone);
        }

        return new(lines, rebases, deletes) { Looked = [.. looked.Select(each => each.Known)], Apart = apart };
    }

    /// <summary>
    /// Every repository with a checkout here, and whether each holds a branch of Daoris's (WSR7, D112): what a look takes
    /// by default, read on the machine and never over the network.
    /// </summary>
    public async Task<IReadOnlyList<SyncRepository>> SyncScopeAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, CancellationToken ct = default) =>
        [.. (await CheckoutsAsync(repositories, ct).ConfigureAwait(false)).Select(each => each.Known)];

    /// <summary>A repository with a checkout here, as a look or a press takes it: its root, and whether it holds Daoris's branches.</summary>
    private sealed record Checkout(string Repository, string? Space, string Root, SyncRepository Known);

    private async Task<List<Checkout>> CheckoutsAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, CancellationToken ct)
    {
        var record = Recorded.All();
        var present = repositories
            .Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root))
            .ToList();
        var holds = await AtMostAsync(present, SyncBounds.FetchesAtOnce,
            (each, token) => HoldsAsync(each.Root!, each.Repository, record, token), ct).ConfigureAwait(false);
        return [.. present.Zip(holds, (each, held) =>
            new Checkout(each.Repository, each.Workspace, each.Root!, new SyncRepository(each.Repository, RemoteTarget.Workspace(each.Workspace), held)))];
    }

    /// <summary>
    /// Each checkout's line, and its fetch where asked (WSR7): the one network step, a few repositories at a time, so a
    /// look waits about one fetch's time rather than one per repository. A failed fetch is the reason, in git's words.
    /// </summary>
    private async Task<IReadOnlyList<(string? Line, string? Failed)>> FetchedAsync(List<Checkout> checkouts, bool fetch, CancellationToken ct) =>
        await AtMostAsync(checkouts, SyncBounds.FetchesAtOnce, async (each, token) =>
        {
            var line = (await LineAsync(each.Root, each.Repository, RemoteTarget.Workspace(each.Space), token).ConfigureAwait(false)).Branch;
            return (line, fetch ? await FetchAsync(each.Root, line, token).ConfigureAwait(false) : null);
        }, ct).ConfigureAwait(false);

    /// <summary><paramref name="work"/> over each item, at most <paramref name="atOnce"/> at a time, the answers in the items' order (WSR7).</summary>
    internal static async Task<IReadOnlyList<TResult>> AtMostAsync<T, TResult>(
        IReadOnlyList<T> items, int atOnce, Func<T, CancellationToken, Task<TResult>> work, CancellationToken ct)
    {
        var answers = new TResult[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = atOnce, CancellationToken = ct },
            async (index, token) => answers[index] = await work(items[index], token).ConfigureAwait(false)).ConfigureAwait(false);
        return answers;
    }

    /// <summary>The checkouts a scope takes, and every other one, listed apart (D112).</summary>
    private async Task<(List<Checkout> Looked, List<SyncRepository> Apart)> ScopedAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, SyncScope scope, CancellationToken ct)
    {
        var checkouts = await CheckoutsAsync(repositories, ct).ConfigureAwait(false);
        return ([.. checkouts.Where(each => scope.Includes(each.Known))],
            [.. checkouts.Where(each => !scope.Includes(each.Known)).Select(each => each.Known)]);
    }

    /// <summary>
    /// Whether a repository holds a branch of Daoris's (D112): a session branch, or a branch a landing recorded that still
    /// stands — one list of its local branches, beside the landings record.
    /// </summary>
    private static async Task<bool> HoldsAsync(string root, string repository, IReadOnlyList<LandedBranch> record, CancellationToken ct)
    {
        var (code, refs, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", "refs/heads/"], ct).ConfigureAwait(false);
        // 🔴 A list git could not give is not "nothing of Daoris's": the repository is looked at, and its row says git's words.
        if (code != 0) return true;
        var branches = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.StartsWith("refs/heads/", StringComparison.Ordinal))
            .Select(name => name["refs/heads/".Length..])
            .ToHashSet(StringComparer.Ordinal);
        return branches.Any(branch => branch.StartsWith("daoris/", StringComparison.Ordinal))
               || EntriesOf(record, repository).Any(entry => branches.Contains(entry.Branch));
    }

    /// <summary>
    /// The press (WSR6): each repository's line fast-forwarded, the branches still at work replayed onto it, and the
    /// landed branches whose work reached it deleted — in that order, each judged again right before it acts.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>This writes into repositories the person owns</b>, so every guard of <see cref="MergeAsync"/> and
    /// of the clean-up is here, and each refuses rather than repairs. The line moves only forward and only where
    /// nothing is lost: in the repository's own checkout only while it is clean and on the line, checked again
    /// immediately before; elsewhere by moving the ref, and only from the commit it was judged at. No merge
    /// commit, no force, no push. A replay happens in a tree of Daoris's own — the session's, or one made for
    /// it under the trees home and removed after — never the person's checkout; a conflict aborts it and names the
    /// files, leaving the branch as it was, and a replay is kept only once its own work is proven intact. A branch
    /// on its remote is left, since replaying it would need a force push.</para>
    ///
    /// <para><b>Replays before deletions.</b> A session branch that grew from a landed branch shares its commits,
    /// and the landed branch's proof keeps it while one does (WSR5); once replayed past them it shares only the
    /// line's, and the landed branch can go.</para>
    ///
    /// <para><b>Branches that shared commits keep sharing them.</b> A landing's branch made at a session's tip is
    /// the same commit replayed once, and one inside another is replayed onto the other's new commits, so the
    /// clean-up's proof reads them together afterwards as it did before.</para>
    ///
    /// <para>🔴 <b>No session starts in a tree while it is replayed</b> (LEFT2). A repository's replays run with its
    /// trees held alone (<see cref="TreeLock"/>), which a driver or a conversation takes, shared, from choosing its
    /// tree until its record is open; the sessions in use are asked again inside the hold. A repository a session is
    /// starting in has its replays left, each said, for another press.</para>
    /// </remarks>
    /// <param name="only">The rows the person saw listed, as <c>repository:branch</c> (the line's by its branch); null for everything the proofs clear now.</param>
    /// <param name="fetch">Whether to fetch first — the terminal's one command does; the screen's press acts on what its list fetched.</param>
    /// <param name="inUseNow">
    /// The trees sessions running or waiting hold, asked again while a repository's trees are held for its replays
    /// (<see cref="TreeLock"/>, LEFT2): a session a driver opened since <paramref name="inUse"/> was read is then seen.
    /// Every door with a service hands it; without it the replays judge by <paramref name="inUse"/> alone.
    /// </param>
    /// <param name="scope">
    /// Which repositories to take (WSR7, D112); null for the default, those holding a branch of Daoris's. Each repository
    /// a row in <paramref name="only"/> names is taken too: the press acts on what the person was shown.
    /// </param>
    public async Task<SyncDone> SyncAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, bool fetch = false, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUseNow = null, SyncScope? scope = null)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var pulls = new List<PullResult>();
        var rebases = new List<RebaseResult>();
        var deletes = new List<LandedResult>();
        bool Listed(string key) => only is null || only.Contains(key);
        var (looked, apart) = await ScopedAsync(repositories, (scope ?? SyncScope.Held).Listed(only), ct).ConfigureAwait(false);
        // The network first, a few repositories at a time (WSR7); each is then judged again right before it acts.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);

        foreach (var ((repository, space, root, _), (line, fetched)) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);

            // (a) Pull the line.
            var (pull, inRoot) = await JudgePullAsync(root, repository, workspace, line, fetched, ct).ConfigureAwait(false);
            if (Listed($"{repository}:{line}"))
            {
                pulls.Add(pull.Moves
                    ? await PullAsync(root, pull, inRoot, ct).ConfigureAwait(false)
                    : new PullResult(pull, false, only is null ? SyncWords.Describe(pull) : $"{SyncWords.Describe(pull)} — {LeftSince}"));
            }

            // (c) Replay what still works on it, onto the line as it now stands — with the repository's trees held
            // alone (LEFT2), so no session starts in one while it is rebased, and the sessions in use asked again
            // inside that hold, since one may have opened since the look `inUse` came from.
            var onto = await OntoAsync(root, line, ct).ConfigureAwait(false);
            using var held = TreeLock.TryReplaying(home, workspace, repository);
            var busyNow = held is not null && inUseNow is not null
                ? new HashSet<string>((await inUseNow(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
                : busy;
            var (judged, _) = await JudgeBranchesAsync(root, repository, workspace, line, onto, busyNow, ct).ConfigureAwait(false);
            var replayed = new List<Replayed>();
            foreach (var each in await DeepestLastAsync(root, judged, ct).ConfigureAwait(false))
            {
                var key = $"{repository}:{each.Item.Branch}";
                if (!Listed(key)) continue;
                if (!each.Item.Replays)
                {
                    rebases.Add(new(each.Item, false, only is null ? SyncWords.Describe(each.Item) : $"{SyncWords.Describe(each.Item)} — {LeftSince}"));
                    continue;
                }

                if (held is null)
                {
                    rebases.Add(new(each.Item, false, $"`{each.Item.Branch}`: {TreeLock.Starting(repository)}."));
                    continue;
                }

                rebases.Add(await ReplayAsync(root, repository, workspace, line!, onto!, each, replayed, ct).ConfigureAwait(false));
            }

            // Let go once the replays are done: the deletions below touch no session's tree. (The `using` is for a throw.)
            held?.Dispose();

            // (b) Delete what merged: the landed half of the clean-up, after the replays, and never a branch just replayed.
            var replayedNow = judged.Where(each => each.Item.Replays).Select(each => $"{repository}:{each.Item.Branch}").ToHashSet(StringComparer.Ordinal);
            var deleteOnly = only is null ? null : only.Where(key => !replayedNow.Contains(key)).ToHashSet(StringComparer.Ordinal);
            var cleaned = await CleanLandedAsync([(repository, space, root)], deleteOnly, ct).ConfigureAwait(false);
            deletes.AddRange(cleaned.Where(result => result.Removed || result.Message != "kept"
                || result.Item.Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.LeanedOn));

            await ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        }

        return new(pulls, rebases, deletes) { Apart = apart };
    }

    /// <summary>
    /// The review's base for a session tree (WSR6): the commit the session record says it began at, unless bringing
    /// the branch up to date replayed it since — then that commit is no longer in its history, and a diff from it
    /// would show the line's own changes as the session's. The branch's record then says where it now grows from.
    /// </summary>
    public async Task<string> ReviewBaseAsync(string tree, string recorded, CancellationToken ct = default)
    {
        // The record's word becomes a git argument, so it must be a commit id and nothing else.
        if (!Holds(tree) || !Directory.Exists(tree) || recorded.Length == 0 || !recorded.All(char.IsAsciiHexDigit)) return recorded;
        var (inHistory, _, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", recorded, "HEAD"], ct).ConfigureAwait(false);
        if (inHistory == 0) return recorded;

        var (_, branchOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var (_, repository) = OwnerOf(Path.GetFullPath(tree));
        if (Grown.Of(repository, branchOut.Trim()) is not { } grown) return recorded;
        var (fromInHistory, _, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", grown.From, "HEAD"], ct).ConfigureAwait(false);
        return fromInHistory == 0 ? grown.From : recorded;
    }

    /// <summary>A branch as judged for the replay, with what the press acts on: its tip, its tree, where its own commits start.</summary>
    private sealed record RebaseJudged(RebaseItem Item, string? Tip = null, string? Tree = null, string? Cut = null);

    /// <summary>A branch the press replayed: where it was, where it went, and where its own commits started.</summary>
    private sealed record Replayed(string OldTip, string NewTip, string Cut);

    /// <summary>
    /// Fetch the line from <c>origin</c> (WSR6), as the person, with their credentials: the one network step, and it
    /// moves only origin's own refs here. Null where it fetched; otherwise why not, in git's words.
    /// </summary>
    private static async Task<string?> FetchAsync(string root, string? line, CancellationToken ct)
    {
        if (line is null) return null;
        var (remote, _, _) = await WorkingTree.GitAsync(root, ["remote", "get-url", "origin"], ct).ConfigureAwait(false);
        if (remote != 0) return "there is no `origin` remote here";
        var (code, _, err) = await WorkingTree.GitAsync(root, ["fetch", "--quiet", "origin", line], NoPrompt, SyncBounds.Fetch, ct).ConfigureAwait(false);
        return code == 0 ? null : Failure(err);
    }

    /// <summary>Where the line stands as the rest of Daoris reads it — the local branch, else origin's — as a commit, or null.</summary>
    private static async Task<string?> OntoAsync(string root, string? line, CancellationToken ct)
    {
        if (line is null || await ComparableAsync(root, line, ct).ConfigureAwait(false) is not { } name) return null;
        return await CommitOfAsync(root, name, ct).ConfigureAwait(false);
    }

    private static async Task<string?> CommitOfAsync(string root, string revision, CancellationToken ct)
    {
        var (code, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], ct).ConfigureAwait(false);
        return code == 0 ? sha.Trim() : null;
    }

    private static async Task<bool> IsAncestorAsync(string root, string ancestor, string of, CancellationToken ct) =>
        (await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", ancestor, of], ct).ConfigureAwait(false)).Code == 0;

    private static async Task<int> CountAsync(string root, string range, CancellationToken ct)
    {
        var (_, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", range], ct).ConfigureAwait(false);
        return int.TryParse(count.Trim(), out var n) ? n : 0;
    }

    /// <summary>What pulling the line would do, and whether the line is checked out in the repository's own checkout.</summary>
    private static async Task<(LinePull Pull, bool InRoot)> JudgePullAsync(
        string root, string repository, string workspace, string? line, string? fetched, CancellationToken ct)
    {
        LinePull Pull(string kind, string? from = null, string? to = null, int commits = 0, string? detail = null) =>
            new(repository, workspace, line, kind, from, to, commits, fetched, detail);

        if (line is null) return (Pull(PullKind.NoLine), false);
        var remote = await CommitOfAsync(root, $"refs/remotes/origin/{line}", ct).ConfigureAwait(false);
        if (remote is null) return (Pull(PullKind.NoRemote), false);
        var local = await CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        if (local is null) return (Pull(PullKind.NoLocal, to: remote), false);
        if (local == remote) return (Pull(PullKind.UpToDate, local, remote), false);

        if (!await IsAncestorAsync(root, local, remote, ct).ConfigureAwait(false))
        {
            return await IsAncestorAsync(root, remote, local, ct).ConfigureAwait(false)
                ? (Pull(PullKind.Ahead, local, remote, await CountAsync(root, $"{remote}..{local}", ct).ConfigureAwait(false)), false)
                : (Pull(PullKind.Diverged, local, remote, await CountAsync(root, $"{local}..{remote}", ct).ConfigureAwait(false)), false);
        }

        var commits = await CountAsync(root, $"{local}..{remote}", ct).ConfigureAwait(false);
        var trees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        if (!trees.TryGetValue(line, out var tree)) return (Pull(PullKind.FastForward, local, remote, commits), false);
        if (!SamePath(tree, root)) return (Pull(PullKind.CheckedOut, local, remote, commits), false);

        var (clean, _) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
        return clean
            ? (Pull(PullKind.FastForward, local, remote, commits), true)
            : (Pull(PullKind.Dirty, local, remote, commits), true);
    }

    /// <summary>
    /// The fast-forward (WSR6): in the repository's own checkout, a <c>merge --ff-only</c> while it is clean and on the
    /// line, both read immediately before; where the line is checked out nowhere, the ref moved only from the commit
    /// it was judged at. Never a merge commit, never a force.
    /// </summary>
    private static async Task<PullResult> PullAsync(string root, LinePull pull, bool inRoot, CancellationToken ct)
    {
        var line = pull.Line!;
        var took = $"fast-forwarded `{line}` by {pull.Commits} commit(s) to `origin/{line}`.";
        if (inRoot)
        {
            // 🔴 The checkout's state, read as late as possible: somebody may be working in it right now.
            var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
            if (!clean)
            {
                return new(pull, false, $"the repository's own checkout is not clean ({detail}), so `{line}` stays where it is.");
            }

            var (_, onOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            if (!string.Equals(onOut.Trim(), line, StringComparison.Ordinal))
            {
                return new(pull, false, $"the repository's checkout moved to `{onOut.Trim()}` since the list, so `{line}` was left for another look.");
            }

            if (await CommitOfAsync(root, "HEAD", ct).ConfigureAwait(false) != pull.From)
            {
                return new(pull, false, $"`{line}` moved since the list, so it was left for another look.");
            }

            var (code, _, err) = await WorkingTree.GitAsync(root, ["merge", "--ff-only", "--quiet", pull.To!], ct).ConfigureAwait(false);
            return code == 0
                ? new(pull, true, took)
                : new(pull, false, $"git would not fast-forward `{line}`: {Failure(err)} The checkout is as it was.");
        }

        // The ref alone, and only from the commit it was judged at: git refuses the move if it is elsewhere now.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: fast-forward {line} to origin/{line}", $"refs/heads/{line}", pull.To!, pull.From!], ct)
            .ConfigureAwait(false);
        return moved == 0
            ? new(pull, true, took)
            : new(pull, false, $"`{line}` moved since the list, so it was left: {Failure(moveErr)}");
    }

    /// <summary>
    /// Every session branch and every recorded landed branch of one repository, judged for the replay onto
    /// <paramref name="onto"/> — and the landed branches whose work reached the line, which go instead.
    /// </summary>
    private async Task<(List<RebaseJudged> Branches, List<LandedItem> Deletes)> JudgeBranchesAsync(
        string root, string repository, string workspace, string? line, string? onto, HashSet<string> busy, CancellationToken ct)
    {
        var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        var judged = new List<RebaseJudged>();
        foreach (var branch in await SessionBranchesAsync(root, ct).ConfigureAwait(false))
        {
            var grown = Grown.Of(repository, branch);
            judged.Add(await JudgeReplayAsync(
                root, repository, workspace, branch, landed: false, worktrees.GetValueOrDefault(branch), grown?.From, grown?.GrewFrom,
                pushedOnRecord: false, line, onto, busy, ct).ConfigureAwait(false));
        }

        var deletes = new List<LandedItem>();
        var entries = EntriesOf(Recorded.All(), repository);
        if (entries.Count == 0) return (judged, deletes);

        // The session branches that stay as they are after the press: a replayed one shares only the line's commits.
        var staying = judged.Where(each => !each.Item.Replays).Select(each => each.Item.Branch).ToList();
        foreach (var landed in await JudgeLandedAsync(root, repository, workspace, entries, staying, stale: null, ct).ConfigureAwait(false))
        {
            var item = landed.Item;
            if (item.Removable || item.Kind == LandedKind.LeanedOn)
            {
                deletes.Add(item);
                continue;
            }

            var entry = entries.First(each => each.Branch == item.Branch);
            RebaseItem Left(string kind, int commits = 0, string? detail = null) =>
                new(repository, workspace, item.Branch, true, kind, line, null, null, null, commits, detail);
            judged.Add(item.Kind switch
            {
                LandedKind.Differs => await JudgeReplayAsync(
                    root, repository, workspace, item.Branch, landed: true, worktrees.GetValueOrDefault(item.Branch), entry.From, null,
                    entry.Pushed, line, onto, busy, ct).ConfigureAwait(false),
                LandedKind.CheckedOut => new RebaseJudged(Left(RebaseKind.CheckedOut)),
                LandedKind.AheadOfRemote => new RebaseJudged(Left(RebaseKind.Pushed, item.Commits)),
                _ => new RebaseJudged(Left(RebaseKind.Unknown, detail: item.Detail)),
            });
        }

        return (judged, deletes);
    }

    /// <summary>
    /// One branch, judged for the replay (WSR6): what keeps it as it is, and otherwise where its own commits start.
    /// </summary>
    /// <remarks>
    /// <para><b>Where its own commits start.</b> Where it grew from the line, where it leaves the line — a plain
    /// rebase. Where its record says it started beyond the line — a chain's step on the step before's tip — that
    /// commit, and only once its work reads on the line, by the clean-up's proof by content: then the step
    /// before's commits drop, since a squash merge put their content on the line and none of them. Until then it
    /// waits, since replaying only its own would lose the work it builds on. With no record (a tree opened before
    /// Daoris kept one), the newest commit of it whose work reads on the line, else where it leaves the line.</para>
    /// </remarks>
    private async Task<RebaseJudged> JudgeReplayAsync(
        string root, string repository, string workspace, string branch, bool landed, string? tree, string? grow, string? grewFrom,
        bool pushedOnRecord, string? line, string? onto, HashSet<string> busy, CancellationToken ct)
    {
        RebaseJudged Item(string kind, string? tip = null, string? cut = null, string? cutBy = null, int commits = 0, string? detail = null) =>
            new(new RebaseItem(repository, workspace, branch, landed, kind, line, cut is null ? null : cut[..Math.Min(8, cut.Length)], cutBy,
                grewFrom, commits, detail), tip, tree, cut);

        var tip = await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false);
        if (tip is null) return Item(RebaseKind.Unknown, detail: "the branch is gone");
        if (onto is null) return Item(RebaseKind.Unknown, tip, detail: line is null ? "no line is set and git names none" : $"there is no `{line}` here, nor `origin/{line}`");

        if (tree is not null)
        {
            // A landing's branch is never replayed where someone has it checked out; a session's only in its own tree.
            if (landed || !Holds(tree)) return Item(RebaseKind.CheckedOut, tip);
            if (busy.Contains(Normal(tree))) return Item(RebaseKind.InUse, tip);
            if (!Directory.Exists(tree)) return Item(RebaseKind.Unknown, tip, detail: "its tree is gone from disk, and git still names it (`git worktree prune` forgets it)");
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty)) return Item(RebaseKind.Dirty, tip, detail: $"{dirty.Trim().Split('\n').Length} path(s) uncommitted");
        }

        if (await IsAncestorAsync(root, onto, tip, ct).ConfigureAwait(false)) return Item(RebaseKind.UpToDate, tip);
        if (pushedOnRecord || await OnRemoteAsync(root, branch, ct).ConfigureAwait(false)) return Item(RebaseKind.Pushed, tip);

        var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["merge-base", tip, onto], ct).ConfigureAwait(false);
        if (baseCode != 0) return Item(RebaseKind.Unknown, tip, detail: $"git found no point where it left the line: {FirstLine(baseErr)}");
        var leaves = baseOut.Trim();

        string cut;
        string cutBy;
        if (grow is not null && await IsAncestorAsync(root, grow, tip, ct).ConfigureAwait(false))
        {
            if (await IsAncestorAsync(root, grow, leaves, ct).ConfigureAwait(false))
            {
                (cut, cutBy) = (leaves, CutBy.Line);
            }
            else
            {
                var proof = await ProveAsync(root, grow, line, [onto], ct).ConfigureAwait(false);
                if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged)) return Item(RebaseKind.Waits, tip);
                (cut, cutBy) = (grow, CutBy.Record);
            }
        }
        else
        {
            (cut, cutBy) = await CutByContentAsync(root, tip, leaves, line, onto, ct).ConfigureAwait(false);
        }

        return Item(RebaseKind.Replay, tip, cut, cutBy, await CountAsync(root, $"{cut}..{tip}", ct).ConfigureAwait(false));
    }

    /// <summary>How far back the proof by content looks for where a branch's own work starts, before taking where it leaves the line.</summary>
    private const int ContentWalk = 64;

    /// <summary>
    /// Where a branch with no record starts its own work (WSR6): walking back from its tip along its first parents,
    /// the newest commit whose work reads on the line by the clean-up's proof; none, and where it leaves the line.
    /// </summary>
    private static async Task<(string Cut, string CutBy)> CutByContentAsync(
        string root, string tip, string leaves, string? line, string onto, CancellationToken ct)
    {
        var (code, list, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--first-parent", $"--max-count={ContentWalk}", tip, "--not", leaves], ct).ConfigureAwait(false);
        if (code != 0) return (leaves, CutBy.Line);
        foreach (var commit in list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var proof = await ProveAsync(root, commit, line, [onto], ct).ConfigureAwait(false);
            if (proof.Kind is LandedKind.OnLine or LandedKind.Merged) return (commit, CutBy.Content);
        }

        return (leaves, CutBy.Line);
    }

    /// <summary>
    /// Whether a branch is on a remote: a remote-tracking branch of its own name, as its upstream or on
    /// <c>origin</c>. An upstream a new branch borrowed from its start point (the line's) is not its own.
    /// </summary>
    private static async Task<bool> OnRemoteAsync(string root, string branch, CancellationToken ct)
    {
        if (await CommitOfAsync(root, $"refs/remotes/origin/{branch}", ct).ConfigureAwait(false) is not null) return true;
        var (code, upstream, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", $"{branch}@{{upstream}}"], ct).ConfigureAwait(false);
        return code == 0 && upstream.Trim().EndsWith($"/{branch}", StringComparison.Ordinal);
    }

    /// <summary>
    /// The replays in the order that keeps shared commits shared: a branch inside another first, and a session's own
    /// branch before a landing's at the same commit, so the landing's takes the session's new commit.
    /// </summary>
    private static async Task<IReadOnlyList<RebaseJudged>> DeepestLastAsync(string root, List<RebaseJudged> judged, CancellationToken ct)
    {
        var depths = new Dictionary<RebaseJudged, int>();
        foreach (var each in judged) depths[each] = each.Item.Replays && each.Tip is not null ? await CountAsync(root, each.Tip, ct).ConfigureAwait(false) : 0;
        return [.. judged.OrderBy(each => depths[each]).ThenBy(each => each.Item.Landed ? 1 : 0)];
    }

    /// <summary>
    /// Replay one branch's own commits onto the line (WSR6), in a tree of Daoris's own, then prove its own work
    /// survived before the branch moves. A conflict aborts, and the branch is left as it was.
    /// </summary>
    /// <remarks>
    /// <para><b>The replay happens detached, and the branch moves last</b>, from the commit it was judged at: a session's
    /// tree is detached where it stands, a landing's branch is replayed in a tree made for it. Only once the new
    /// commit is proven does the ref move, so a refusal at any step leaves the branch where it was.</para>
    ///
    /// <para>🔴 <b>The proof</b> (the first real post-merge run): between the old tip and the new one, only files the
    /// line itself changed between the cut and where the branch now grows from may differ — everything else of the
    /// branch's must read exactly as before. Where the line brought nothing but the squash of the work the branch grew
    /// from, the two tips read the same.</para>
    /// </remarks>
    private async Task<RebaseResult> ReplayAsync(
        string root, string repository, string workspace, string line, string onto, RebaseJudged judged, List<Replayed> replayed,
        CancellationToken ct)
    {
        var item = judged.Item;
        var tip = judged.Tip!;
        var cut = judged.Cut!;
        var (baseCommit, upstream) = await WithinAsync(root, replayed, tip, cut, ct).ConfigureAwait(false) is { } within
            ? (within.NewTip, within.OldTip)
            : (onto, cut);

        string? now;
        string words;
        var tree = judged.Tree;
        if (tree is not null)
        {
            // 🔴 The session's own tree, read again immediately before: still on this branch, at the commit judged, clean.
            var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (onOut.Trim() != item.Branch || await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) != tip || !string.IsNullOrWhiteSpace(dirty))
            {
                return new(item, false, $"`{item.Branch}` {LeftSince}");
            }

            (now, words) = await ReplayDetachedAsync(tree, item.Branch, baseCommit, upstream, ct).ConfigureAwait(false);
            if (now is null)
            {
                return new(item, false, await BackOnAsync(tree, item.Branch, tip, ct).ConfigureAwait(false)
                    ? $"`{item.Branch}` would not replay onto `{line}`: {words} The replay was aborted, and the branch is as it was."
                    : $"`{item.Branch}` would not replay onto `{line}`: {words} Git could not put its tree back on it: look at the tree before "
                      + "anything else (`git rebase --abort`, then `git switch` to the branch).");
            }
        }
        else if (upstream == tip)
        {
            now = baseCommit;
        }
        else
        {
            (now, words) = await ReplayAsideAsync(root, workspace, repository, tip, baseCommit, upstream, ct).ConfigureAwait(false);
            if (now is null) return new(item, false, $"`{item.Branch}` would not replay onto `{line}`: {words} Nothing was moved.");
        }

        // 🔴 Proven before the branch moves: nothing of its own changed but what the line brought.
        if (await BeyondTheLineAsync(root, tip, now, upstream, baseCommit, ct).ConfigureAwait(false) is { } beyond)
        {
            if (tree is not null) await BackOnAsync(tree, item.Branch, tip, ct).ConfigureAwait(false);
            return new(item, false, $"`{item.Branch}`'s replay changed what the line did not bring ({beyond}), so it was not kept: the branch "
                + "is as it was.");
        }

        // Only from the commit it was judged at: git refuses the move if it is elsewhere now.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: bring {item.Branch} up to date onto {line}", $"refs/heads/{item.Branch}", now, tip], ct)
            .ConfigureAwait(false);
        if (tree is not null) await WorkingTree.GitAsync(tree, ["switch", "--quiet", item.Branch], ct).ConfigureAwait(false);
        if (moved != 0) return new(item, false, $"`{item.Branch}` moved since the list, so it was left: {Failure(moveErr)}");

        replayed.Add(new Replayed(tip, now, cut));
        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        var message = $"replayed `{item.Branch}` onto `{line}`: {kept} commit(s) of its own"
            + (kept < item.Commits ? $" ({item.Commits - kept} became empty, their changes already on the line)." : ".");

        // Where it grows from now is the line: the record says so, so the next press and the review cut here.
        try
        {
            if (item.Landed) Recorded.Moved(repository, item.Branch, now, onto);
            else Grown.Record(new GrownBranch(repository, workspace, item.Branch, line, onto, null, DateTimeOffset.UtcNow));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            message += $" Daoris could not record where it now grows from ({error.Message}).";
        }

        return new(item, true, message);
    }

    /// <summary>
    /// A branch this press already replayed whose old commits this one holds past its own start — the nearest,
    /// replayed last — so this one is replayed onto its new commits and the two stay shared; null for none.
    /// </summary>
    private static async Task<Replayed?> WithinAsync(string root, List<Replayed> replayed, string tip, string cut, CancellationToken ct)
    {
        for (var at = replayed.Count - 1; at >= 0; at--)
        {
            var done = replayed[at];
            if (done.OldTip == cut) continue;
            if (await IsAncestorAsync(root, done.OldTip, tip, ct).ConfigureAwait(false)
                && await IsAncestorAsync(root, cut, done.OldTip, ct).ConfigureAwait(false))
            {
                return done;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>git rebase --onto</c> in a tree whose HEAD is the branch (or a detached copy of it), aborted on any failure.
    /// </summary>
    /// <remarks>
    /// The person's configuration still speaks — their identity, their signing, their hooks — since the replay runs
    /// as them, except two settings that would reach beyond the one branch judged: <c>rebase.updateRefs</c> would
    /// move every other branch pointing into the range, and <c>rebase.autoStash</c> would carry uncommitted work
    /// the judgement found none of.
    /// </remarks>
    private static async Task<(bool Failed, string Words)> RebaseInAsync(string tree, string onto, string upstream, CancellationToken ct)
    {
        var (code, output, err) = await WorkingTree.GitAsync(
            tree, ["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "rebase", "--onto", onto, upstream],
            environment: null, SyncBounds.Replay, ct).ConfigureAwait(false);
        if (code == 0) return (false, "");

        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        var conflicts = (output + "\n" + err).Split('\n')
            .Select(each => each.Trim())
            .Where(each => each.StartsWith("CONFLICT", StringComparison.Ordinal))
            .ToList();
        return (true, conflicts.Count > 0
            ? string.Join("; ", conflicts.Take(3)) + (conflicts.Count > 3 ? $"; and {conflicts.Count - 3} more." : ".")
            : Failure(err.Length > 0 ? err : output));
    }

    /// <summary>
    /// A session's own tree, detached where it stands and replayed there (WSR6): the new commit, or null and git's
    /// words. The branch itself is not moved here, and on a failure the tree is left detached for
    /// <see cref="BackOnAsync"/> to put back.
    /// </summary>
    private static async Task<(string? Commit, string Words)> ReplayDetachedAsync(
        string tree, string branch, string onto, string upstream, CancellationToken ct)
    {
        var (detached, _, detachErr) = await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach"], ct).ConfigureAwait(false);
        if (detached != 0) return (null, $"git would not detach its tree to replay it: {Failure(detachErr)}");
        var (failed, words) = await RebaseInAsync(tree, onto, upstream, ct).ConfigureAwait(false);
        return failed ? (null, words) : (await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false), "");
    }

    /// <summary>A session's tree put back on its branch after a replay that was not kept — true where it is on it, at the commit judged.</summary>
    private static async Task<bool> BackOnAsync(string tree, string branch, string tip, CancellationToken ct)
    {
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(tree, ["switch", "--quiet", branch], ct).ConfigureAwait(false);
        var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return onOut.Trim() == branch && await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) == tip;
    }

    /// <summary>
    /// The files that differ between the branch's old tip and its new one beyond those the line changed between where
    /// its own commits started and what they were replayed onto (WSR6) — null where there are none; git unable to say
    /// is itself a reason not to keep the replay.
    /// </summary>
    private static async Task<string?> BeyondTheLineAsync(string root, string tip, string now, string upstream, string onto, CancellationToken ct)
    {
        var changed = await NamesAsync(root, tip, now, ct).ConfigureAwait(false);
        var brought = await NamesAsync(root, upstream, onto, ct).ConfigureAwait(false);
        if (changed is null || brought is null) return "git could not compare the two";
        var beyond = changed.Where(path => !brought.Contains(path, StringComparer.Ordinal)).ToList();
        return beyond.Count == 0 ? null : string.Join(", ", beyond.Take(3)) + (beyond.Count > 3 ? $" and {beyond.Count - 3} more" : "");
    }

    /// <summary>
    /// A branch nobody has checked out, replayed in a tree made for it under the trees home and removed after (WSR6):
    /// the new commit, or null and git's words. The branch itself is not moved here.
    /// </summary>
    /// <remarks>
    /// Under the trees home, where Daoris's trees live, and like every git call here with long paths on: the first real
    /// post-merge run, done by hand in a tree made elsewhere without them, could not check out the repository's
    /// deepest files.
    /// </remarks>
    private async Task<(string? Commit, string Words)> ReplayAsideAsync(
        string root, string workspace, string repository, string tip, string onto, string upstream, CancellationToken ct)
    {
        var aside = Path.Combine(TreesRoot, workspace, repository, $"r-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(Path.GetDirectoryName(aside)!);
        var (added, _, addErr) = await WorkingTree.GitAsync(root, ["worktree", "add", "--quiet", "--detach", aside, tip], ct).ConfigureAwait(false);
        if (added != 0) return (null, $"Daoris could not make a tree to replay it in: {Failure(addErr)}");
        try
        {
            var (failed, words) = await RebaseInAsync(aside, onto, upstream, ct).ConfigureAwait(false);
            return failed ? (null, words) : (await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false), "");
        }
        finally
        {
            // Daoris's own tree, made a moment ago for this one replay and holding nothing of anyone's: its commits
            // are in the repository, so removing it with --force loses nothing.
            await WorkingTree.GitAsync(root, ["worktree", "remove", "--force", aside], ct).ConfigureAwait(false);
        }
    }

    /// <summary>The record forgets session branches that are gone, so a name is never judged by a start it no longer has.</summary>
    private async Task ForgetGoneAsync(string root, string repository, CancellationToken ct)
    {
        // 🔴 A list git could not give is not "every branch is gone".
        var (code, refs, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        if (code != 0) return;
        var standing = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var gone = Grown.All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && !standing.Contains(entry.Branch))
            .Select(entry => entry.Branch)
            .ToList();
        if (gone.Count == 0) return;
        try
        {
            Grown.Forget(repository, gone);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record that could not be tidied only keeps a name no branch has; the next press tries again.
        }
    }
}
