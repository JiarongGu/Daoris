namespace Daoris.Driver;

/// <summary>What a landed branch holds, for the clean-up's list (WSR5). Only the first three go.</summary>
public static class LandedKind
{
    /// <summary>Every file it changed reads on the line as it left it — a squash merge's pull request reached the line.</summary>
    public const string OnLine = "on-line";

    /// <summary>Every commit of it is on the line.</summary>
    public const string Merged = "merged";

    /// <summary>Its history is inside another landed branch whose work is on the line.</summary>
    public const string Inside = "inside";

    /// <summary>
    /// Its pull request completed, as the plugin that pushed it answered, and git confirms it here (PLUGHOOK1a, D148 point 4): the
    /// merge commit is on the line and the branch stands at or under the commit it merged. What a squash leaves once the line
    /// has moved on.
    /// </summary>
    public const string PullRequest = "pull-request";

    /// <summary>Some file it changed reads otherwise on the line — kept, and the files named.</summary>
    public const string Differs = "differs";

    /// <summary>A working tree has it checked out, the repository's own checkout included — kept.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>It holds commits its remote-tracking branch does not: pushed, then moved — kept.</summary>
    public const string AheadOfRemote = "ahead-of-remote";

    /// <summary>A session branch that stays holds commits only this branch holds, and D88's proof for it leans on this one — kept.</summary>
    public const string LeanedOn = "leaned-on";

    /// <summary>Git could not say — kept.</summary>
    public const string Unknown = "unknown";
}

/// <param name="Where">On the line: the form of the line it was proven on (`main`, `origin/main`). Inside: the branch it is inside. Leaned on: the session branch.</param>
/// <param name="Files">Differs: the files it changed that read otherwise on the line.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
/// <param name="PullRequest">The pull request a plugin answered with when it pushed the branch, where the record knows one.</param>
/// <param name="Commits">Ahead of its remote: the commits the remote lacks. Otherwise: the commits it holds that are not on the line.</param>
public sealed record LandedItem(
    string Repository, string Workspace, string Branch, string Kind, string? Where, IReadOnlyList<string> Files,
    string? Detail, string? PullRequest, int Commits)
{
    public bool Removable => Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest;

    /// <summary>
    /// The latest answer about its pull request, where one is kept (PLUGHOOK1a, design §2.5): what a row says the platform
    /// answered, and when.
    /// </summary>
    public PullRequestState? State { get; init; }

    /// <summary>
    /// Why a kept answer does not clear it, where one is kept and does not (design §2.3): the state (<c>open</c>, <c>abandoned</c>,
    /// <c>unknown</c>) or one of <see cref="PullRequestCodes"/>. Null where nothing is kept, or the answer cleared it.
    /// </summary>
    public string? StateCode { get; init; }

    /// <summary>The latest ask that failed since that answer, where one did (design §2.4).</summary>
    public PullRequestAskFailed? AskFailed { get; init; }

    /// <summary>
    /// Why this look did not ask about its pull request, where it would have (PLUGHOOK1c, design §2.1): no plugin to ask, the
    /// plugin not ready, or the occasion's bound reached first. Null where it was asked, was not due, or its work reads on the line.
    /// </summary>
    public PullRequestNotAsked? NotAsked { get; init; }
}

/// <summary>What the clean-up did with one landed branch, in the driver's words.</summary>
public sealed record LandedResult(LandedItem Item, bool Removed, string Message);

/// <summary>The clean-up's whole list (D88, WSR5): the session branches, then the branches landings made.</summary>
public sealed record SweepPlan(IReadOnlyList<SweepItem> Sessions, IReadOnlyList<LandedItem> Landed);

/// <summary>What one press of the clean-up did, both groups.</summary>
/// <param name="Folders">The empty folders trees left behind, each removed or still held, in a sentence.</param>
public sealed record SweepDone(IReadOnlyList<SweepResult> Sessions, IReadOnlyList<LandedResult> Landed, IReadOnlyList<string>? Folders = null);

/// <summary>A landed branch's row in words — the terminal's line, and the sentence the screen's row says in its own catalogue.</summary>
public static class LandedWords
{
    /// <summary>
    /// The row: what proved it or keeps it, and on a row that is kept what its pull request's answer was and why it does not
    /// clear the branch, or why nothing was asked (PLUGHOOK1c, design §2.5). A row that goes says what proved it, and no more.
    /// </summary>
    public static string Describe(LandedItem item) => Proof(item)
        + (item.Removable ? "" : PullRequestWords.Row(item.State, item.StateCode, item.AskFailed, item.NotAsked));

    private static string Proof(LandedItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        LandedKind.OnLine => $"its pull request reached `{item.Where}`: every file it changed reads there as it left it",
        LandedKind.Merged => $"merged into `{item.Where}`",
        LandedKind.Inside => $"inside `{item.Where}`, whose work is on the line",
        LandedKind.PullRequest => "its pull request completed" + (item.State?.How is { } how ? $" by {how}" : "")
            + (item.State?.Plugin is { } plugin ? $", as `{plugin}` answered," : "") + $" and its merge commit is on `{item.Where}`",
        LandedKind.Differs => (item.Files.Count == 1 ? "1 file still differs" : $"{item.Files.Count} files still differ")
            + $" on the line: {string.Join(", ", item.Files.Take(3))}" + (item.Files.Count > 3 ? $" and {item.Files.Count - 3} more" : ""),
        LandedKind.CheckedOut => "checked out in a working tree",
        LandedKind.AheadOfRemote => $"{item.Commits} commit(s) its remote does not have",
        LandedKind.LeanedOn => $"session branch `{item.Where}` still leans on it",
        _ => $"git could not tell: {item.Detail}",
    };
}

public sealed partial class SessionTrees
{
    /// <summary>
    /// The clean-up's list (D88, WSR5): every session branch with what it holds, then every branch a landing
    /// made and recorded, with whether its work reached the line. Nothing is changed.
    /// </summary>
    public async Task<SweepPlan> CleanPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default)
    {
        var known = repositories.ToList();
        // The look may remove, so it asks first where git cannot tell, as one occasion over every repository (PLUGHOOK1a, design
        // §2.1 occasion 2); the press asks nothing.
        var asked = await AskStatesAsync(
            known.Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root))
                .Select(each => (each.Root!, each.Repository, RemoteTarget.Workspace(each.Workspace))),
            landedMayGo: true, ct).ConfigureAwait(false);

        var sessions = await SweepPlanAsync(known, inUse, ct).ConfigureAwait(false);
        var record = Recorded.All();
        var config = Config();
        var landed = new List<LandedItem>();
        foreach (var (repository, workspace, root) in known)
        {
            var entries = EntriesOf(record, repository);
            if (entries.Count == 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            // What stays after this press: the session branches it keeps, whose proof may lean on a landed branch.
            var staying = sessions
                .Where(item => string.Equals(item.Repository, repository, StringComparison.OrdinalIgnoreCase) && !item.Removable)
                .Select(item => item.Branch).ToList();
            var judged = await JudgeLandedAsync(root, repository, RemoteTarget.Workspace(workspace), entries, staying, stale: null, ct)
                .ConfigureAwait(false);
            // Each row says why its pull request was not asked about, where it would have been (PLUGHOOK1c, design §2.1).
            landed.AddRange(judged.Select(each => each.Item with { NotAsked = NotAskedOf(each.Item, asked, config) }));
        }

        return new(sessions, landed);
    }

    /// <summary>
    /// Why an occasion did not ask about a landed row's pull request, where it would have (PLUGHOOK1c, design §2.1): what the
    /// occasion said of its entry (the plugin not ready, or its bound reached first); else, on a row kept for want of D102's proof
    /// with nothing final kept, that no plugin pushed it and the rule names none. A row asked about, or not due, says nothing.
    /// </summary>
    private PullRequestNotAsked? NotAskedOf(LandedItem item, IReadOnlyList<StateAnswer> asked, DriverConfig config)
    {
        var said = asked.LastOrDefault(answer => answer.Answer is null && answer.Failure is PullRequestCodes.Unready or PullRequestCodes.NotAsked
            && answer.Ask.Entry.GoneAt is null && string.Equals(answer.Ask.Entry.Branch, item.Branch, StringComparison.Ordinal)
            && string.Equals(answer.Ask.Entry.Repository, item.Repository, StringComparison.OrdinalIgnoreCase));
        if (said is not null) return new(said.Failure!, said.Sentence ?? said.Failure!);
        if (item.Kind is not (LandedKind.Differs or LandedKind.Unknown)) return null;
        if (Recorded.Of(item.Repository, item.Branch) is not { } entry || entry.PullRequestState?.State == PullRequestStates.Completed) return null;
        var rule = LandingRules.Choose(config, item.Repository, item.Workspace).Rule;
        return PullRequestAsking.PluginFor(entry, rule) is null ? new(PullRequestCodes.NoPlugin, PullRequestWords.NoPlugin) : null;
    }

    /// <summary>
    /// The clean-up's press (D88, WSR5): the session branches the proof clears go first — their proof may
    /// count a landed branch as holding their commits — then the landed branches whose work reached the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Judged over the whole set, then again right before each goes</b>, because the list is a fact about a
    /// moment and "inside another landed branch" is a fact about the set: the branches that are inside another
    /// go before the one they are inside, and each re-judgment proves that one again. Removal is
    /// <c>git branch -D</c> of exactly the branch judged, at the commit it was judged at, in the repository's
    /// own checkout — no working tree, no other ref, and never a remote branch.
    /// </remarks>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    public async Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default)
    {
        var known = repositories.ToList();
        var sessions = await SweepAsync(known, inUse, only, ct).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), this press's removals among them.
        foreach (var (repository, _, root) in known)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) await ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        }

        var landed = await CleanLandedAsync(known, only, ct).ConfigureAwait(false);
        // The empty folders trees left where something held them open (the first real post-merge run), tried again.
        return new(sessions, landed, EmptyFoldersGone());
    }

    /// <summary>
    /// The landed half of the clean-up's press (WSR5): the recorded branches whose work reached the line go, each
    /// judged over the whole set and again right before it goes. Bringing a repository up to date presses this
    /// half alone (WSR6), after its rebases.
    /// </summary>
    private async Task<List<LandedResult>> CleanLandedAsync(
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> known, IReadOnlySet<string>? only, CancellationToken ct)
    {
        var results = new List<LandedResult>();
        foreach (var (repository, space, root) in known)
        {
            if (EntriesOf(Recorded.All(), repository).Count == 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var workspace = RemoteTarget.Workspace(space);
            var stale = new List<string>();
            var removed = new List<string>();
            var proven = new List<LandedItem>();
            var first = await JudgeLandedAsync(
                root, repository, workspace, EntriesOf(Recorded.All(), repository),
                await SessionBranchesAsync(root, ct).ConfigureAwait(false), stale, ct).ConfigureAwait(false);

            // Inside first: each is proven through a branch still standing.
            foreach (var judged in first.OrderBy(each => each.Item.Kind == LandedKind.Inside ? 0 : 1))
            {
                var item = judged.Item;
                var key = $"{repository}:{item.Branch}";
                var named = only?.Contains(key) == true;
                if (only is not null && !named) continue;
                if (!item.Removable)
                {
                    results.Add(new(item, false, named ? ChangedSince : "kept"));
                    continue;
                }

                var again = await JudgeLandedAsync(
                    root, repository, workspace, EntriesOf(Recorded.All(), repository).Where(entry => !removed.Contains(entry.Branch)).ToList(),
                    await SessionBranchesAsync(root, ct).ConfigureAwait(false), stale: null, ct).ConfigureAwait(false);
                var now = again.FirstOrDefault(each => each.Item.Branch == item.Branch);
                if (now is null || !now.Item.Removable || now.Tip != judged.Tip)
                {
                    results.Add(new(now?.Item ?? item, false, ChangedSince));
                    continue;
                }

                // -D, because the proof was made in this call; git's own -d asks only whether HEAD holds the
                // commits, and a squash merge put none of them anywhere.
                var (code, _, err) = await WorkingTree.GitAsync(root, ["branch", "-D", item.Branch], ct).ConfigureAwait(false);
                if (code == 0)
                {
                    removed.Add(item.Branch);
                    proven.Add(now.Item);
                }

                results.Add(code == 0
                    ? new(now.Item, true, "removed")
                    : new(now.Item, false, $"git would not delete the branch: {FirstLine(err)}"));
            }

            // Gone, or no longer the landing's: never judged again — the name may be the person's now — and kept as a
            // trace with what the proof found, so the review of the session that landed it says where its work went (D113).
            foreach (var item in proven) Recorded.Removed(repository, item.Branch, item.Kind, item.Where);
            if (stale.Count > 0) Recorded.Gone(repository, stale);
        }

        return results;
    }

    private const string ChangedSince = "it changed since the list, and is kept";

    /// <summary>A landed branch as judged, and the commit it was judged at — the one a removal must still find.</summary>
    private sealed record Judged(LandedItem Item, string Tip);

    private static List<LandedBranch> EntriesOf(IReadOnlyList<LandedBranch> record, string repository) =>
        // A pattern under `daoris/` would make a session branch's name, which D88 already judges.
        [.. record.Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                  && !entry.Branch.StartsWith("daoris/", StringComparison.Ordinal))];

    private static async Task<IReadOnlyList<string>> SessionBranchesAsync(string root, CancellationToken ct)
    {
        var (code, refs, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        return code != 0 ? [] : refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Judge every recorded branch of one repository (WSR5): each by content against the line, then those
    /// inside another that passed, then what must keep them.
    /// </summary>
    /// <param name="staying">The session branches that stay: a landed branch one of them leans on stays too.</param>
    /// <param name="stale">Where a branch that is gone, or no longer the landing's, is named for the record to forget; null to only look.</param>
    private async Task<List<Judged>> JudgeLandedAsync(
        string root, string repository, string workspace, IReadOnlyList<LandedBranch> entries, IReadOnlyCollection<string> staying,
        List<string>? stale, CancellationToken ct)
    {
        var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LineFormsAsync(root, line, ct).ConfigureAwait(false);

        var judged = new List<(LandedBranch Entry, string Tip, Proof Proof, string? Keep, int Ahead, PullRequestVerdict? Verdict)>();
        foreach (var entry in entries)
        {
            var (tipCode, tipOut, _) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{entry.Branch}^{{commit}}"], ct).ConfigureAwait(false);
            if (tipCode != 0)
            {
                stale?.Add(entry.Branch);
                continue;
            }

            var tip = tipOut.Trim();
            // 🔴 A person's own branch is never judged: one that took the recorded name since does not hold the
            // commit the landing made it at, whatever its files say.
            var (ours, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
            if (ours != 0)
            {
                stale?.Add(entry.Branch);
                continue;
            }

            var proof = await ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
            // PLUGHOOK1a: a fourth proof, the platform's word confirmed by git, where D102's could not see the work on the line.
            var verdict = entry.PullRequestState is { } kept ? await VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false) : null;
            if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged) && verdict is { Clears: true })
            {
                proof = proof with { Kind = LandedKind.PullRequest, Where = verdict.Form, Files = [], Detail = null, Judgeable = false };
            }

            string? keep = null;
            var ahead = 0;
            if (worktrees.ContainsKey(entry.Branch))
            {
                keep = LandedKind.CheckedOut;
            }
            else if (proof.Kind != LandedKind.Merged && await AheadOfRemoteAsync(root, entry.Branch, tip, ct).ConfigureAwait(false) is { } lacking)
            {
                // Merged would lose nothing — every commit is on the line — so only that outranks a move since the push.
                ahead = lacking;
                keep = lacking > 0 ? LandedKind.AheadOfRemote : null;
            }

            judged.Add((entry, tip, proof, keep, ahead, verdict));
        }

        var proven = judged.Where(each => each.Proof.Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.PullRequest).ToList();
        var items = new List<Judged>();
        foreach (var (entry, tip, proof, keep, ahead, verdict) in judged)
        {
            var kind = proof.Kind;
            var where = proof.Where;
            // Inside another landed branch the proof clears — whether or not that one can go, its history
            // holds these commits and its files read on the line.
            if (proof.Judgeable)
            {
                foreach (var other in proven.Where(other => other.Entry.Branch != entry.Branch))
                {
                    var (inside, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, other.Tip], ct).ConfigureAwait(false);
                    if (inside != 0) continue;
                    kind = LandedKind.Inside;
                    where = other.Entry.Branch;
                    break;
                }
            }

            if (keep is not null)
            {
                kind = keep;
                where = null;
            }

            var removable = kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest;
            if (removable && await LeanerAsync(root, tip, staying, forms, ct).ConfigureAwait(false) is { } leaner)
            {
                kind = LandedKind.LeanedOn;
                where = leaner;
            }

            var files = kind == LandedKind.Differs ? proof.Files : [];
            items.Add(new(
                new LandedItem(repository, workspace, entry.Branch, kind, where, files, proof.Detail, entry.PullRequest,
                    kind == LandedKind.AheadOfRemote ? ahead : proof.Commits)
                {
                    // What the platform last said, and why it does not clear the branch where it does not (design §2.3, §2.5).
                    State = entry.PullRequestState,
                    StateCode = verdict is { Clears: false } ? verdict.Code : null,
                    AskFailed = entry.PullRequestAskFailed,
                },
                tip));
        }

        return items;
    }

    /// <summary>
    /// The line in both its forms (D86) that this checkout has: its own branch, and origin's copy — a
    /// platform's squash merge reaches the second before anyone pulls it into the first.
    /// </summary>
    private static async Task<IReadOnlyList<string>> LineFormsAsync(string root, string? line, CancellationToken ct)
    {
        var forms = new List<string>();
        if (line is null) return forms;
        foreach (var (reference, name) in new[] { ($"refs/heads/{line}", line), ($"refs/remotes/origin/{line}", $"origin/{line}") })
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (code == 0) forms.Add(name);
        }

        return forms;
    }

    /// <summary>What the proof found, before anything that keeps a branch is asked.</summary>
    /// <param name="Judgeable">Whether the branch may still be inside another: its line was read and its files compared.</param>
    private sealed record Proof(string Kind, string? Where, IReadOnlyList<string> Files, string? Detail, int Commits, bool Judgeable);

    /// <summary>
    /// The proof by content (WSR5): the files the branch changed since it left the line — both paths of a
    /// rename, and a deletion as a path the line must not hold — compared as blobs with the line, in each of
    /// its forms. A commit of it the line holds already is merged, which needs no content.
    /// </summary>
    private static async Task<Proof> ProveAsync(string root, string tip, string? line, IReadOnlyList<string> forms, CancellationToken ct)
    {
        if (forms.Count == 0)
        {
            return new(LandedKind.Unknown, null, [], line is null
                ? "no line is set and git names none"
                : $"there is no `{line}` here, nor `origin/{line}`, to compare it with", 0, false);
        }

        foreach (var form in forms)
        {
            var (merged, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, form], ct).ConfigureAwait(false);
            if (merged == 0) return new(LandedKind.Merged, form, [], null, 0, false);
        }

        var (_, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", tip, "--not", .. forms], ct).ConfigureAwait(false);
        var commits = int.TryParse(count.Trim(), out var n) ? n : 0;
        Proof? closest = null;
        string? failed = null;
        foreach (var form in forms)
        {
            var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["merge-base", tip, form], ct).ConfigureAwait(false);
            if (baseCode != 0)
            {
                failed = $"git found no point where it left `{form}`: {FirstLine(baseErr)}";
                continue;
            }

            var changed = await NamesAsync(root, baseOut.Trim(), tip, ct).ConfigureAwait(false);
            var differing = await NamesAsync(root, tip, form, ct).ConfigureAwait(false);
            if (changed is null || differing is null)
            {
                failed = $"git could not compare it with `{form}`";
                continue;
            }

            if (changed.Count == 0)
            {
                // Commits that change nothing leave no file to find on the line: not proven, and not a loss either.
                failed = "its commits change no file, so there is nothing to find on the line";
                closest ??= new(LandedKind.Unknown, null, [], failed, commits, true);
                continue;
            }

            var still = changed.Where(differing.Contains).ToList();
            if (still.Count == 0) return new(LandedKind.OnLine, form, [], null, commits, false);
            if (closest is null || closest.Kind == LandedKind.Unknown || still.Count < closest.Files.Count)
            {
                closest = new(LandedKind.Differs, form, still, null, commits, true);
            }
        }

        return closest ?? new(LandedKind.Unknown, null, [], failed, commits, false);
    }

    /// <summary>The paths that differ between two commits, as git names them with renames split into their two paths; null where git could not say.</summary>
    private static async Task<IReadOnlyList<string>?> NamesAsync(string root, string from, string to, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["diff", "--no-ext-diff", "--no-renames", "--name-only", "-z", from, to], ct).ConfigureAwait(false);
        return code != 0 ? null : output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// How many commits the branch holds that its remote-tracking branch does not, where it has one — its
    /// upstream, else <c>origin/&lt;branch&gt;</c> — or null where it has none: never pushed, or its remote
    /// branch deleted and pruned since.
    /// </summary>
    private static async Task<int?> AheadOfRemoteAsync(string root, string branch, string tip, CancellationToken ct)
    {
        string? remote = null;
        foreach (var candidate in new[] { $"{branch}@{{upstream}}", $"refs/remotes/origin/{branch}" })
        {
            var (code, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{candidate}^{{commit}}"], ct).ConfigureAwait(false);
            if (code != 0) continue;
            remote = sha.Trim();
            break;
        }

        if (remote is null) return null;
        var (countCode, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", tip, "--not", remote], ct).ConfigureAwait(false);
        // A count git could not make keeps the branch: an empty answer is not "nothing ahead".
        return countCode == 0 && int.TryParse(count.Trim(), out var ahead) ? ahead : 1;
    }

    /// <summary>
    /// A session branch that stays and shares commits with this one that the line does not hold: D88 counts
    /// this branch as holding them, so removing it would leave that session's work looking unlanded.
    /// </summary>
    private static async Task<string?> LeanerAsync(
        string root, string tip, IReadOnlyCollection<string> staying, IReadOnlyList<string> forms, CancellationToken ct)
    {
        foreach (var session in staying)
        {
            var (code, bases, _) = await WorkingTree.GitAsync(root, ["merge-base", "--all", session, tip], ct).ConfigureAwait(false);
            if (code != 0) continue;
            foreach (var shared in bases.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var onLine = false;
                foreach (var form in forms)
                {
                    var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", shared, form], ct).ConfigureAwait(false);
                    if (held != 0) continue;
                    onLine = true;
                    break;
                }

                if (!onLine) return session;
            }
        }

        return null;
    }

    /// <summary>
    /// A note the landing appends when the record could not be written: the branch stands, and only its
    /// clean-up and its hand-off are lost — the safe side, said rather than swallowed.
    /// </summary>
    private static TreeLanding Remember(TreeLanding landed, Action write)
    {
        try
        {
            write();
            return landed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return landed with
            {
                Message = landed.Message + $" Daoris could not record the branch as a landing's ({error.Message}), so its clean-up will not judge it.",
            };
        }
    }
}
