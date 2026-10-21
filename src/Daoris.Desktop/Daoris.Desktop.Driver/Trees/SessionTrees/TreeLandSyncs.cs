using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// What a landing judged before it lands (LANDSYNC1): the repository's checkout, the tree's branch and tip, the line and the commit
/// of it the tree was judged against, and what the line holds of the tree's commits (<see cref="LineHolds"/>).
/// </summary>
/// <param name="OntoName">How the line's commit is named in words: the line, or <c>origin/&lt;line&gt;</c> where origin's is newer.</param>
internal sealed record LandSync(string Root, string Branch, string Line, string Onto, string OntoName, string Tip, LineHeld Held);

/// <summary>What a landing's replay made: the new tip, the commits on it beyond what it was replayed onto, and what was left out.</summary>
/// <param name="LeftOut">What the line holds, with what the replay found empty among the kept, oldest first.</param>
internal sealed record LandReplay(string Now, IReadOnlyList<LandingCommit> Landed, IReadOnlyList<LeftOutCommit> LeftOut)
{
    /// <summary>
    /// The files the repository marks generated that the work and the line both changed, which the replay took at the line's copy
    /// (LANDREBASE1): written again where the work is merged, never the session's conflict. Empty where there were none.
    /// </summary>
    public IReadOnlyList<string> Generated { get; init; } = [];
}

/// <summary>
/// A landing in sync with its line (LANDSYNC1, D87's 2026-10-15 amendment): the press's one fetch and its words where it fails, the
/// judgement against the line, the replay of what the line lacks in a tree of Daoris's own and its proof, the conflict's refusal,
/// and the session's tree moved to what landed. Over the trees' context, and WSR6's replay.
/// </summary>
internal sealed class TreeLandSyncs(TreeContext context, BranchReplays replays)
{
    /// <summary>
    /// Read what a landing of <paramref name="full"/> is judged against, and judge it: the branch form against the line's latest by
    /// REVIEWBASE2-i's rule, the merge form against the line here (point 3). <paramref name="fetch"/> fetches the line first, once
    /// (point 6), and a fetch that fails is said and judged against the last one, never refused. Null where there is nothing to judge
    /// (not a tree of this home's, not on a branch, no line, or git cannot say), and the landing goes on as before.
    /// </summary>
    /// <param name="read">
    /// The tree's repository and branch as the landing read them a moment ago (LANDGIT1b), handed down rather than read again; null
    /// reads them here. The tip judged is read here all the same: it is what a replay is made from.
    /// </param>
    internal async Task<(LandSync? Sync, string? Fetched)> JudgeAsync(
        string full, string repository, string workspace, bool branchForm, bool fetch, CancellationToken ct, CommonDirAndBranch? read = null)
    {
        if (!context.Holds(full) || !Directory.Exists(full)) return (null, null);
        // Its repository and its branch, one start (PROC1d-iii).
        var tree = read ?? await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (tree.Code != 0) return (null, null);
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tree.CommonDir.Trim()))!;
        var branch = tree.Branch.Trim();
        if (branch is "" or "HEAD") return (null, null);
        if ((await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch is not { } line) return (null, null);

        var fetched = fetch ? await FetchedAsync(root, line, ct).ConfigureAwait(false) : null;
        // LANDGIT1b: the line here and origin's copy of it, one read after the fetch (PROC1d-ii), where each form was a start of its own
        // and the line here was asked twice. Read at the moment the forms were, and the judgement's alone: it ends with this method.
        var refs = RefSnapshot.Take(root);
        var here = await CommitOfAsync(root, $"refs/heads/{line}", ct, refs).ConfigureAwait(false);
        var onto = branchForm ? await LatestAsync(root, line, ct, refs).ConfigureAwait(false) : here;
        if (onto is null || await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false) is not { } tip) return (null, fetched);
        if (await LineHolds.JudgeAsync(root, tip, onto, line, ct).ConfigureAwait(false) is not { } held) return (null, fetched);
        return (new LandSync(root, branch, line, onto, onto == here ? line : $"origin/{line}", tip, held), fetched);
    }

    /// <summary>
    /// The press's one fetch of the line a tree's landing is judged against (point 6), for an entry's Accept, which fetches once before
    /// its parts and judges each against it (ACCEPTALL1d): the same fetch <see cref="JudgeAsync"/> makes. Its words where it failed,
    /// said once; null where it fetched, where there was nothing to fetch, or where the tree has no line to judge against.
    /// </summary>
    internal async Task<string?> FetchLineAsync(string full, string repository, string workspace, CancellationToken ct)
    {
        if (!context.Holds(full) || !Directory.Exists(full)) return null;
        var tree = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (tree.Code != 0) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tree.CommonDir.Trim()))!;
        if ((await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch is not { } line) return null;
        return await FetchedAsync(root, line, ct).ConfigureAwait(false);
    }

    /// <summary>D109's fetch of the line, and its words in WSR7's where it failed; null where it fetched or there is no `origin`.</summary>
    private static async Task<string?> FetchedAsync(string root, string line, CancellationToken ct)
    {
        var (failed, url) = await FetchAsync(root, line, ct).ConfigureAwait(false);
        // No `origin` at all is nothing to fetch, and nothing to say: the line here is all there is.
        if (failed is null || url is null) return null;
        return LandingSyncWords.NotFetched(failed, await LastFetchAsync(root, line, ct).ConfigureAwait(false), ReachOf(url), DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Replay <paramref name="kept"/> onto <paramref name="onto"/> aside, and prove it before anything is made (point 2): between the
    /// tree's tip and the replay's only files the line brought may differ, or, where <paramref name="onto"/> is a branch the landing
    /// advances, files that branch or the line brought. A conflict refuses with the rebase words; any other failure refuses in git's.
    /// </summary>
    /// <param name="joining">
    /// The entry's branch a part that ran alongside is joining (ACCEPTALL1), where nothing the line holds made it replay: a conflict
    /// is then with the entry's earlier work on that branch, and said so. Null for a replay onto the line, or past what it holds.
    /// </param>
    internal async Task<(LandReplay? Replay, TreeLanding? Refused)> ReplayAsync(
        LandSync sync, string repository, string workspace, string onto, IReadOnlyList<LandingCommit> kept, CancellationToken ct,
        string? joining = null)
    {
        var made = await replays.ReplayKeptAsideAsync(sync.Root, workspace, repository, onto, sync.Held.Commits, kept, ct).ConfigureAwait(false);
        if (made.Now is not { } now)
        {
            return (null, made.Conflict
                ? joining is not null ? EntryLandWords.Conflicted(joining, made.Words) : Conflicted(sync, made.Words)
                : new TreeLanding(false, $"Nothing was landed: replaying its own commits onto `{sync.OntoName}` failed: {made.Words} Nothing was made, "
                    + "and the tree and its branch are as they were.") { Refusal = AutoLandingCode.Refused, LeftOut = sync.Held.LeftOut });
        }

        if (await BeyondAsync(sync, now, onto, ct).ConfigureAwait(false) is { } beyond)
        {
            return (null, new TreeLanding(false, $"Nothing was landed: replaying its own commits onto `{sync.OntoName}` changed what the line did not bring "
                + $"({beyond}), so the replay was not kept. Nothing was made, and the tree and its branch are as they were.")
            { Refusal = AutoLandingCode.Refused, LeftOut = sync.Held.LeftOut });
        }

        var landed = await LineHolds.CommitsAsync(sync.Root, $"{onto}..{now}", ct).ConfigureAwait(false) ?? [];
        // What became empty: kept commits git dropped, matched in order by subject, since a replay keeps each one's message.
        var empty = new HashSet<string>(StringComparer.Ordinal);
        var next = 0;
        foreach (var commit in kept)
        {
            if (next < landed.Count && landed[next].Subject == commit.Subject) next++;
            else empty.Add(commit.Sha);
        }

        var left = sync.Held.LeftOut.ToDictionary(each => each.Sha, StringComparer.Ordinal);
        foreach (var commit in kept.Where(each => empty.Contains(each.Sha))) left[commit.Sha] = new LeftOutCommit(commit.Sha, commit.Subject, LeftOutWhy.Empty);
        return (new LandReplay(now, landed, [.. sync.Held.Commits.Where(each => left.ContainsKey(each.Sha)).Select(each => left[each.Sha])])
        {
            Generated = made.Generated,
        }, null);
    }

    /// <summary>The conflict's refusal (point 5): git's lines, nothing made, and the words the review offers to say to the session.</summary>
    internal static TreeLanding Conflicted(LandSync sync, string conflicts) =>
        new(false, $"Nothing was landed: replaying its own commits onto `{sync.OntoName}` conflicts with what the line changed since: {conflicts} "
            + "Nothing was made, and the tree and its branch are as they were." + LandingSyncWords.LeftOutSentence(sync.Held.LeftOut)
            + " Ask its session to replay its own commits onto the line, then accept again.")
        {
            Refusal = AutoLandingCode.Refused,
            LeftOut = sync.Held.LeftOut,
            Conflict = new LandingConflict(conflicts, LandingSyncWords.Rebase(sync.OntoName, sync.Held.LeftOut, conflicts)),
        };

    /// <summary>
    /// A branch of the tree's own (TREEFOLLOW1) that is on its remote, where the line holds some of its commits: never rewritten
    /// (D109), so refused as a conflict is, with the rebase words (point 3).
    /// </summary>
    internal static TreeLanding OwnOnRemote(LandSync sync)
    {
        var says = $"`{sync.Branch}` is on its remote, and Daoris does not rewrite a pushed branch.";
        return new(false, $"Nothing was landed: the line already holds some of this work, and {says}"
            + LandingSyncWords.LeftOutSentence(sync.Held.LeftOut) + " Ask its session to replay its own commits onto the line, then accept again.")
        {
            Refusal = AutoLandingCode.Refused,
            LeftOut = sync.Held.LeftOut,
            Conflict = new LandingConflict(says, LandingSyncWords.Rebase(sync.OntoName, sync.Held.LeftOut, null)),
        };
    }

    /// <summary>
    /// The session's tree moved to what landed (point 7), as WSR6 moves a session branch: detached where it stands, its branch moved
    /// by compare-and-swap from the commit the landing judged, switched back. Only while it is clean, on that branch at that commit,
    /// nothing runs in it and its branch is not on its remote. Null where it moved; else why it stays.
    /// </summary>
    internal async Task<string?> MoveTreeAsync(
        string full, LandSync sync, string now, string repository, string workspace, IReadOnlySet<string>? inUse, CancellationToken ct)
    {
        if (!Directory.Exists(full)) return "its tree is gone";
        if (inUse is not null && inUse.Any(tree => SamePath(tree, full))) return "a session still running or waiting holds it";
        var (_, onOut, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var (_, dirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (onOut.Trim() != sync.Branch || await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false) != sync.Tip) return "it moved since it was judged";
        if (!string.IsNullOrWhiteSpace(dirty)) return "it has uncommitted work";
        if (await OnRemoteAsync(sync.Root, sync.Branch, ct).ConfigureAwait(false)) return $"`{sync.Branch}` is on its remote, which Daoris does not rewrite";

        var (detached, _, detachErr) = await WorkingTree.GitAsync(full, ["switch", "--quiet", "--detach"], ct).ConfigureAwait(false);
        if (detached != 0) return $"git would not detach it: {Failure(detachErr)}";
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            sync.Root, ["update-ref", "-m", $"daoris: {sync.Branch} follows what landed", $"refs/heads/{sync.Branch}", now, sync.Tip], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(full, ["switch", "--quiet", sync.Branch], ct).ConfigureAwait(false);
        if (moved != 0) return $"git would not move `{sync.Branch}`: {Failure(moveErr)}";

        // Where it grows from now is the line (WSR6): bringing it up to date and the review cut here.
        try
        {
            var grown = context.Grown.Of(repository, sync.Branch);
            context.Grown.Record(new GrownBranch(repository, workspace, sync.Branch, sync.Line, sync.Onto, null, DateTimeOffset.UtcNow, grown?.Now));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The branch moved; only the record of where it grows from is lost, and the review reads the line instead.
        }

        return null;
    }

    /// <summary>Files that differ between the tree's tip and the replay's beyond what the line, or the branch replayed onto, brought.</summary>
    private static async Task<string?> BeyondAsync(LandSync sync, string now, string onto, CancellationToken ct)
    {
        var changed = await LandedProofs.NamesAsync(sync.Root, sync.Tip, now, ct).ConfigureAwait(false);
        var brought = await LandedProofs.NamesAsync(sync.Root, sync.Held.Leaves, sync.Onto, ct).ConfigureAwait(false);
        var branched = onto == sync.Onto ? [] : await LandedProofs.NamesAsync(sync.Root, sync.Held.Leaves, onto, ct).ConfigureAwait(false);
        if (changed is null || brought is null || branched is null) return "git could not compare the two";
        var beyond = changed.Where(path => !brought.Contains(path, StringComparer.Ordinal) && !branched.Contains(path, StringComparer.Ordinal)).ToList();
        return beyond.Count == 0 ? null : string.Join(", ", beyond.Take(3)) + (beyond.Count > 3 ? $" and {beyond.Count - 3} more" : "");
    }
}

/// <summary>A landing in sync with its line, in words (LANDSYNC1): what it left out and why, the fetch, the follow and the rebase words.</summary>
public static class LandingSyncWords
{
    /// <summary>
    /// What <c>trees land --plan</c> says of what the line holds (LANDSYNC1, D87's amendment point 4): what the press would leave out
    /// and why, what it would replay, and a conflict it would be refused for; where the line holds none of the tree's commits and only
    /// moved since the work began, what it would replay onto it first (LANDREBASE1); nothing where it lands the commits as made.
    /// </summary>
    public static IEnumerable<string> Plan(LandingPlan plan)
    {
        if (plan.LeftOut is not { } left || plan.Lands is not { } lands) yield break;
        var replays = string.Join(", ", lands.Select(commit => $"`{TextBounds.ShortCommit(commit.Sha, 8)}` “{commit.Subject}”")) + ".";
        if (left.Count == 0)
        {
            yield return $"trees: the line moved since its work began, so it would replay its {lands.Count} commit(s) onto the line first: {replays}";
        }
        else
        {
            yield return $"trees: it would leave out {left.Count} commit(s) the line holds: {string.Join(", ", left.Select(Each))}.";
            yield return $"trees: it would replay {lands.Count} commit(s) onto the line: {replays}";
        }

        if (plan.Conflict is { } conflict)
        {
            yield return $"trees: replaying them conflicts now, so a press would be refused: {conflict.Says} Its session can be asked: {conflict.Words}";
        }
    }

    /// <summary>One commit left out: its short id, subject and why (D87's amendment point 4).</summary>
    public static string Each(LeftOutCommit commit) => $"`{TextBounds.ShortCommit(commit.Sha, 8)}` “{commit.Subject}”" + commit.Why switch
    {
        LeftOutWhy.Content => " (on the line by content)",
        LeftOutWhy.Patch when commit.On is { } on => $" (on the line as `{TextBounds.ShortCommit(on, 8)}`, the same change)",
        LeftOutWhy.Patch => " (the same change is on the line)",
        _ => " (it became empty: its changes are already on the line)",
    };

    /// <summary>
    /// What a replayed landing carried: <c>N commit(s) replayed onto `line`, leaving out M the line holds: …</c>; where it left out
    /// nothing the line holds, the line only moved since the work began (LANDREBASE1), and that is said.
    /// </summary>
    public static string Replayed(int landed, string ontoName, IReadOnlyList<LeftOutCommit> leftOut)
    {
        var held = leftOut.Where(each => each.Why != LeftOutWhy.Empty).ToList();
        return held.Count == 0
            ? $"{landed} commit(s) replayed onto `{ontoName}`, which moved since the work began"
            : $"{landed} commit(s) replayed onto `{ontoName}`, leaving out {held.Count} the line holds: {string.Join(", ", held.Select(Each))}";
    }

    /// <summary>
    /// The generated files a replay took at the line's copy (LANDREBASE1), said after the sentence: up to three named, to be written again
    /// where the work is merged. Nothing where there were none.
    /// </summary>
    public static string Generated(IReadOnlyList<string> paths) => paths.Count == 0 ? ""
        : $" {paths.Count} generated file(s) the line changed too took the line's copy ({string.Join(", ", paths.Take(3))}"
          + (paths.Count > 3 ? $" and {paths.Count - 3} more" : "") + "), to be written again where the work is merged.";

    /// <summary>The commits the replay found empty, said after the sentence; nothing where none was.</summary>
    public static string Emptied(IReadOnlyList<LeftOutCommit> leftOut)
    {
        var empty = leftOut.Where(each => each.Why == LeftOutWhy.Empty).ToList();
        return empty.Count == 0 ? ""
            : " Became empty when replayed, their changes already on the line: "
              + string.Join(", ", empty.Select(each => $"`{TextBounds.ShortCommit(each.Sha, 8)}` “{each.Subject}”")) + ".";
    }

    /// <summary>What a landing that moved a branch on left out, said after it, with what became empty.</summary>
    public static string LeftOutLanded(IReadOnlyList<LeftOutCommit> leftOut)
    {
        var held = leftOut.Where(each => each.Why != LeftOutWhy.Empty).ToList();
        return (held.Count == 0 ? "" : $" It left out {held.Count} the line holds: {string.Join(", ", held.Select(Each))}.") + Emptied(leftOut);
    }

    /// <summary>What a refusal left out, said beside it; nothing where nothing was.</summary>
    public static string LeftOutSentence(IReadOnlyList<LeftOutCommit> leftOut) =>
        leftOut.Count == 0 ? "" : $" The line already holds {string.Join(", ", leftOut.Select(Each))}.";

    /// <summary>
    /// The words the session is asked to rebase with where its replay conflicts (point 5): the review offers them to the person's press,
    /// and an automatic landing says them itself (LANDREBASE1). That the line moved, and what of the work it holds where it holds some,
    /// the files, and the steps in the session's own tree: replay its own commits onto the line (keeping only what the line lacks where it
    /// holds some), resolve, rerun the tests its change reaches, and commit.
    /// </summary>
    public static string Rebase(string ontoName, IReadOnlyList<LeftOutCommit> leftOut, string? conflicts) =>
        $"Rebase your work onto `{ontoName}`: the line moved since your work began"
        + (leftOut.Count == 0
            ? conflicts is null ? "." : $", and landing it conflicts with what the line changed: {conflicts}"
            : $" and already holds some of it ({string.Join(", ", leftOut.Select(Each))})"
              + (conflicts is null ? "." : $", and landing the rest conflicts with it: {conflicts}"))
        + $" In your own tree, replay only your own commits onto `{ontoName}`"
        + (leftOut.Count == 0 ? "" : ", keeping only what the line lacks")
        + ", resolve each conflict, rerun the tests your change reaches, and commit. It can land again once your tree has changed.";

    /// <summary>
    /// A fetch that failed (point 6), in WSR7's words: git's reason, that the landing was judged against what origin said when it was
    /// last fetched here and when that was, and what the git Daoris runs needs to reach an origin over SSH or HTTPS.
    /// </summary>
    public static string NotFetched(string reason, DateTimeOffset? lastFetch, string? reach, DateTimeOffset now) =>
        $" `origin` was not fetched ({reason}), so this was judged against what it said when it was last fetched here "
        + $"({SyncWords.LastFetched(lastFetch, now)})." + (SyncWords.Reaching(reach) is { } needs ? $" {needs}" : "");

    /// <summary>The follow's words (point 7): the tree moved to what landed, or why it stays.</summary>
    public static string Followed(string? stays) => stays is null
        ? " The session's tree now stands on what landed, so its next turn starts in sync with the line."
        : $" The session's tree stays where it was ({stays}), so its next turn starts from its old commits.";
}
