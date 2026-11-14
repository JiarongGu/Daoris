using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A history edit asked: <paramref name="Edit"/> is <c>pick</c>, <c>revert</c> or <c>rebase</c>; <paramref name="Target"/> the
/// commit or <c>A..B</c> range to pick or revert onto <paramref name="Branch"/>, or the commit to rebase it onto.
/// </summary>
public sealed record HistoryEditAsk(string Home, string Repository, string Checkout, string Edit, string Branch, string Target)
{
    /// <summary>The repository's line, which is never rewritten; null where none is known.</summary>
    public string? Line { get; init; }

    /// <summary>The ids a shown plan pinned, which the press must still find; null pins what the press's own plan reads.</summary>
    public HistoryEditPins? Pins { get; init; }

    /// <summary>Told after each commit is made: how many are made, of how many.</summary>
    public Action<int, int>? Progress { get; init; }
}

/// <summary>A commit an edit takes, by its whole id and its subject.</summary>
public sealed record HistoryEditCommit(string Id, string Subject);

/// <summary>What a plan showed: the branch's tip, the commit the edit builds on, and the commits it takes, in order.</summary>
public sealed record HistoryEditPins(string Tip, string Onto, IReadOnlyList<string> Commits);

/// <summary>What an edit would do, read fresh: the branch at its tip, the commit it builds on, the commits in the order they are taken.</summary>
public sealed record HistoryEditPlan(string Edit, string Branch, string Tip, string Onto, IReadOnlyList<HistoryEditCommit> Commits)
{
    /// <summary>The refs read and left, verified at these ids in the edit's transaction: a rebase's remote-tracking refs.</summary>
    public IReadOnlyList<(string Ref, string Id)> Proofs { get; init; } = [];

    /// <summary>The commands a person types in their checkout for the same edit.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>The ids the plan pins.</summary>
    public HistoryEditPins Pins => new(Tip, Onto, [.. Commits.Select(each => each.Id)]);

    internal string OntoTree { get; init; } = "";

    internal IReadOnlyList<HistoryEditRead> Read { get; init; } = [];
}

/// <summary>An edit's run: the act's, and where it moved the branch and kept its old tip, once done.</summary>
public sealed record HistoryEditRun(ActRun Run, string? Tip = null, string? Kept = null);

/// <summary>A commit as <c>cat-file</c> holds it: its parents, whether it is signed, its message's encoding, its author and message exactly.</summary>
internal sealed record HistoryEditRead(string Id, IReadOnlyList<string> Parents, bool Signed, string? Encoding, string Author, string Message)
{
    public string Subject => Message.Split('\n')[0].Trim();

    /// <summary>The author's name, email and date for <c>commit-tree</c>, so a picked commit keeps them.</summary>
    public IReadOnlyDictionary<string, string> AuthorEnvironment
    {
        get
        {
            var close = Author.LastIndexOf('>');
            var open = close < 0 ? -1 : Author.LastIndexOf('<', close);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GIT_AUTHOR_NAME"] = open < 0 ? Author : Author[..open].TrimEnd(),
                ["GIT_AUTHOR_EMAIL"] = open < 0 ? "" : Author[(open + 1)..close],
                ["GIT_AUTHOR_DATE"] = close < 0 ? "" : Author[(close + 1)..].Trim(),
            };
        }
    }
}

/// <summary>
/// **Pick, revert and rebase onto, without a working tree**. On a branch no
/// tree has out, never the line, a session's, a landing's or one bound to bring sessions together: each commit made from
/// <c>merge-tree --write-tree --merge-base</c> and <c>commit-tree</c>, then the branch's <c>update</c>, its old tip's recovery ref under
/// <c>refs/daoris/discarded/</c> and the proof refs' <c>verify</c>, one transaction run as an act of the journal (<see cref="ActRuns"/>).
/// </summary>
/// <remarks>
/// <para><b>Read fresh, pinned, read again under the lock.</b> The plan reads git's version (2.40, <c>merge-tree --merge-base</c>), the
/// configuration's programs, the branch, the trees, the commits; the act reads all of it again under the checkout lock and refuses where any
/// pinned id moved. The commits are made there, objects only, before the record; a conflict, an empty result or git's no refuses with
/// nothing journaled and no ref moved.</para>
/// <para><b>Occupancy, before and after.</b> A branch checked out between the reads and the update is put back by the rewrite's rule, under
/// the same lock (<see cref="ActPlan.After"/>): the branch set back to its old tip only from the tip the edit produced, the recovery ref
/// dropped at that tip.</para>
/// </remarks>
public static class GitHistoryEdits
{
    public const string Pick = "pick";

    public const string Revert = "revert";

    public const string Rebase = "rebase";

    public const string PickKind = "history.pick";

    public const string RevertKind = "history.revert";

    public const string RebaseKind = "history.rebase";

    /// <summary>The most commits one edit takes (provisional); past it the command to copy is said.</summary>
    public const int MostCommits = 50;

    /// <summary>The first git whose <c>merge-tree</c> takes <c>--merge-base</c>.</summary>
    public static Version Since { get; } = new(2, 40);

    private const string Heads = "refs/heads/";

    /// <summary>Why this git offers no history edit, read from what <c>git version</c> said; null where it does.</summary>
    public static string? Floor(string said)
    {
        if (ToolQuestions.VersionIn(said) is not { } text || !Version.TryParse(text.Count(each => each == '.') == 0 ? text + ".0" : text, out var version))
        {
            return "git did not say its version, so a history edit is not offered; nothing was changed";
        }

        return version < Since
            ? $"this machine's git is {text}, and a history edit needs {Since} or later (`merge-tree --merge-base`), so it is not offered; nothing was changed"
            : null;
    }

    /// <summary>The act's kind for an edit.</summary>
    public static string KindOf(string edit) => edit switch { Revert => RevertKind, Rebase => RebaseKind, _ => PickKind };

    /// <summary>The commands a person types in their checkout for the same edit.</summary>
    public static IReadOnlyList<string> Commands(string edit, string branch, string target) => edit switch
    {
        Rebase => [$"git rebase {target} {branch}"],
        Revert => [$"git switch {branch}", $"git revert {target}"],
        _ => [$"git switch {branch}", $"git cherry-pick {target}"],
    };

    /// <summary>What the edit would do now, read without the lock and written nowhere: its plan, or why it is refused.</summary>
    public static async Task<(HistoryEditPlan? Plan, string? Refusal)> PlanAsync(HistoryEditAsk ask, CancellationToken ct)
    {
        if (ask.Edit is not (Pick or Revert or Rebase)) return Refused($"`{ask.Edit}` is not pick, revert or rebase");
        var root = ask.Checkout;
        var (_, version, _) = await WorkingTree.GitAsync(root, ["version"], ct).ConfigureAwait(false);
        if (Floor(version) is { } below) return (null, below);
        if (ask.Branch.Length == 0 || ask.Branch.StartsWith('-') || ask.Branch.Any(char.IsWhiteSpace)) return Refused($"`{ask.Branch}` is not a branch's name");
        if (ask.Target.Length == 0 || ask.Target.StartsWith('-') || ask.Target.Any(char.IsWhiteSpace) || ask.Target.Contains("...", StringComparison.Ordinal))
        {
            return Refused($"`{ask.Target}` is not a commit or an `A..B` range");
        }

        if (Held(ask) is { } held) return Refused(held);
        if (await CommitAsync(root, Heads + ask.Branch, ct).ConfigureAwait(false) is not { } tip) return Refused($"there is no branch `{ask.Branch}` in {ask.Repository}");
        var (tree, unread) = await OccupantAsync(root, ask.Branch, ct).ConfigureAwait(false);
        if (unread) return Refused($"{TreeWorktrees.ListUnread}, so whether a tree has `{ask.Branch}` out is not known");
        if (tree is not null) return Refused($"`{ask.Branch}` is checked out in the tree `{Path.GetFileName(tree)}`, and a history edit moves only a branch no tree has out");
        if (await ProgramsAsync(ask, ct).ConfigureAwait(false) is { } program) return (null, program);

        // The commits, oldest first: a rebase's since the commit it goes onto, or the commit or range named.
        string onto;
        IReadOnlyList<string>? ids;
        if (ask.Edit == Rebase)
        {
            if (await CommitAsync(root, ask.Target, ct).ConfigureAwait(false) is not { } named) return Refused($"`{ask.Target}` names no commit");
            onto = named;
            ids = await RevListAsync(root, $"{onto}..{tip}", ct).ConfigureAwait(false);
            if (ids is null) return Refused($"git could not list `{ask.Branch}`'s commits since `{ask.Target}`");
            if (ids.Count == 0) return Refused($"`{ask.Branch}` holds no commit `{ask.Target}` does not, so there is nothing to rebase");
        }
        else
        {
            onto = tip;
            ids = ask.Target.Contains("..", StringComparison.Ordinal)
                ? await RevListAsync(root, ask.Target, ct).ConfigureAwait(false)
                : await CommitAsync(root, ask.Target, ct).ConfigureAwait(false) is { } one ? [one] : null;
            if (ids is null || ids.Count == 0) return Refused($"`{ask.Target}` names no commit to {ask.Edit}");
        }

        var commands = Commands(ask.Edit, ask.Branch, ask.Target);
        if (ids.Count > MostCommits)
        {
            return (null, $"it takes {ids.Count} commits, more than the {MostCommits} Daoris rewrites at once, so nothing was changed; by hand: `{string.Join("` then `", commands)}`");
        }

        var read = new List<HistoryEditRead>();
        if (await ParentsAsync(root, ids, ct).ConfigureAwait(false) is not { } parentsOf) return Refused($"git could not read the parents of {Short(ids[0])}");
        foreach (var id in ids)
        {
            if (!parentsOf.TryGetValue(id, out var parents) || await CommitReadAsync(root, id, parents, ct).ConfigureAwait(false) is not { } commit)
            {
                return Refused($"git could not read the commit {Short(id)}");
            }

            if (Refusal(commit) is { } refusal) return Refused(refusal);
            read.Add(commit);
        }

        if (read.Skip(1).Where((commit, at) => commit.Parents[0] != read[at].Id).Any()) return Refused($"the commits from {Short(ids[0])} are not one line of history");

        if (ask.Edit == Rebase && read[0].Parents[0] == onto) return Refused($"`{ask.Branch}` already stands on `{ask.Target}`, so there is nothing to rebase");
        IReadOnlyList<(string, string)> proofs = [];
        if (ask.Edit == Rebase)
        {
            var (published, remotes, why) = await PublishedAsync(root, read[0].Id, ct).ConfigureAwait(false);
            if (why is not null) return Refused(why);
            if (published is not null) return Refused($"{Short(read[0].Id)} is on `{published}`, so a rebase would rewrite published history");
            proofs = remotes;
        }

        if (ask.Edit == Revert) read.Reverse();
        var (treed, ontoTree, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", onto + "^{tree}"], ct).ConfigureAwait(false);
        if (treed != 0 || !Whole(ontoTree.Trim())) return Refused($"git could not read the tree of {Short(onto)}");
        return (new HistoryEditPlan(ask.Edit, ask.Branch, tip, onto, [.. read.Select(each => new HistoryEditCommit(each.Id, each.Subject))])
        {
            Proofs = proofs, Commands = commands, OntoTree = ontoTree.Trim(), Read = read,
        }, null);
    }

    /// <summary>
    /// Run the edit: planned fresh, refused where a pinned id moved, then under the lock read again, its commits made and the branch moved in
    /// one transaction; and put back where a tree checked it out as it moved.
    /// </summary>
    public static async Task<HistoryEditRun> EditAsync(ActRuns runs, HistoryEditAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        var (plan, refusal) = await PlanAsync(ask, ct).ConfigureAwait(false);
        if (plan is null) return new(Refusing(refusal!));
        if (ask.Pins is { } pins && Moved(ask, pins, plan) is { } moved) return new(Refusing(moved));

        var full = Heads + ask.Branch;
        string? made = null, kept = null;
        var run = await runs.RunAsync(new ActPlan(KindOf(ask.Edit), press, ask.Repository, ask.Checkout, [])
        {
            Plan = PlanJson(ask, plan).ToJsonString(), Confirmed = confirmed,
            Judge = async token =>
            {
                var (now, why) = await PlanAsync(ask, token).ConfigureAwait(false);
                return now is null ? why : Moved(ask, plan.Pins, now);
            },
            Make = async token =>
            {
                var (tip, unmade) = await MakeAsync(ask, plan, token).ConfigureAwait(false);
                if (tip is null) return (null, unmade);
                if (await KeptNameAsync(ask.Checkout, ask.Branch, token).ConfigureAwait(false) is not { } recovery)
                {
                    return (null, $"git could not list the recovery refs, so `{ask.Branch}` was not moved; only objects were written");
                }

                var transaction = new RefTransaction().Update(full, plan.Tip, tip).Create(recovery, plan.Tip).Verify(plan.Proofs);
                try
                {
                    var step = ActSteps.Ref(transaction, [(full, plan.Tip, tip), (recovery, null, plan.Tip)]);
                    (made, kept) = (tip, recovery);
                    return ([step], null);
                }
                catch (ArgumentException error)
                {
                    return (null, $"{error.Message}, so `{ask.Branch}` was not moved; only objects were written");
                }
            },
            After = ordinal => ordinal == 0 && made is not null && kept is not null ? OccupiedLateAsync(ask, plan, made, kept) : Task.FromResult<string?>(null),
        }, ct).ConfigureAwait(false);
        return run.Outcome == ActOutcomes.Done ? new(run, made, kept) : new(run);
    }

    /// <summary>
    /// The occupancy read again under the lock once the branch moved: a tree on it, or a list git could not give, puts it back by the rewrite's
    /// rule, the branch set to its old tip only from the tip the edit produced and the recovery ref dropped at that tip, in one transaction.
    /// Null where no tree has it out.
    /// </summary>
    private static async Task<string?> OccupiedLateAsync(HistoryEditAsk ask, HistoryEditPlan plan, string made, string kept)
    {
        var none = CancellationToken.None;
        var (tree, unread) = await OccupantAsync(ask.Checkout, ask.Branch, none).ConfigureAwait(false);
        if (tree is null && !unread) return null;
        var why = unread
            ? $"{TreeWorktrees.ListUnread} once it moved, so whether a tree checked it out as it moved is not known"
            : $"it was checked out in the tree `{Path.GetFileName(tree!.TrimEnd('/', '\\'))}` as it moved";

        var full = Heads + ask.Branch;
        var run = await RefTransaction.RestoreRewritten(full, made, plan.Tip).Delete(kept, plan.Tip).RunAsync(ask.Checkout, none).ConfigureAwait(false);
        if (run.Done) return $"{why}, so it was put back where it was";
        var (code, at, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", full], none).ConfigureAwait(false);
        return code == 0 && at.Trim() == plan.Tip
            ? $"{why}, so it was put back where it was"
            : $"{why}, and Daoris could not put it back at {Short(plan.Tip)} ({ContentHolds.Said(run.Said)}); its old commit is kept at `{kept}`";
    }

    /// <summary>Each commit made in order, objects only: the new tip, or why none is, a conflict's files named.</summary>
    private static async Task<(string? Tip, string? Refusal)> MakeAsync(HistoryEditAsk ask, HistoryEditPlan plan, CancellationToken ct)
    {
        var author = await GitAuthor.OfAsync(ask.Home, ask.Checkout, ct).ConfigureAwait(false);
        var (at, tree) = (plan.Onto, plan.OntoTree);
        var doing = ask.Edit == Revert ? "reverting" : "picking";
        for (var index = 0; index < plan.Read.Count; index++)
        {
            var commit = plan.Read[index];
            var parent = commit.Parents[0];
            var (bases, theirs) = ask.Edit == Revert ? (commit.Id, parent) : (parent, commit.Id);
            var (code, said, err) = await WorkingTree.GitAsync(
                ask.Checkout, ["merge-tree", "--write-tree", "--name-only", "--no-messages", $"--merge-base={bases}", at, theirs], ct).ConfigureAwait(false);
            var lines = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var what = $"{doing} {Short(commit.Id)} ({commit.Subject}) onto {Short(at)}";
            if (code == 1 && lines.Length > 0)
            {
                return (null, $"{what} conflicts in {string.Join(", ", lines.Skip(1).Distinct().Select(file => $"`{file}`"))}; only objects were written, so nothing was changed");
            }

            if (code != 0 || lines.Length == 0 || !Whole(lines[0])) return (null, $"git could not merge for {what} ({ContentHolds.Said(err)}); only objects were written, so nothing was changed");
            if (lines[0] == tree) return (null, $"{what} changes nothing, so nothing was changed");

            var message = ask.Edit == Revert ? $"Revert \"{commit.Subject}\"\n\nThis reverts commit {commit.Id}.\n" : commit.Message;
            IReadOnlyList<string> arguments = [.. GitAuthor.Arguments(author), "commit-tree", lines[0], "-p", at, "--no-gpg-sign", "-F", "-"];
            var (made, id, madeErr) = ask.Edit == Revert
                ? await WorkingTree.GitAsync(ask.Checkout, arguments, message, ct).ConfigureAwait(false)
                : await WorkingTree.GitAsync(ask.Checkout, arguments, commit.AuthorEnvironment, message, ct).ConfigureAwait(false);
            if (made != 0 || !Whole(id.Trim())) return (null, $"git could not make the commit for {what} ({ContentHolds.Said(madeErr)}); only objects were written, so nothing was changed");
            (at, tree) = (id.Trim(), lines[0]);
            ask.Progress?.Invoke(index + 1, plan.Read.Count);
        }

        return (at, null);
    }

    /// <summary>Why the plan shown no longer stands, or null where every pinned id does.</summary>
    private static string? Moved(HistoryEditAsk ask, HistoryEditPins pinned, HistoryEditPlan now)
    {
        if (now.Tip != pinned.Tip) return $"`{ask.Branch}` moved since its plan was shown (from {Short(pinned.Tip)} to {Short(now.Tip)}), so nothing was changed";
        if (now.Onto != pinned.Onto) return $"`{ask.Target}` moved since the plan was shown (from {Short(pinned.Onto)} to {Short(now.Onto)}), so nothing was changed";
        return now.Pins.Commits.SequenceEqual(pinned.Commits)
            ? null
            : $"the commits to {ask.Edit} changed since the plan was shown, so nothing was changed";
    }

    /// <summary>The branches a history edit never moves: Daoris's own, the line, one a session's tree went onto, a landing's, one bound together.</summary>
    private static string? Held(HistoryEditAsk ask)
    {
        var branch = ask.Branch;
        if (branch == "daoris" || branch.StartsWith("daoris/", StringComparison.Ordinal)) return $"`{branch}` is a session's branch, Daoris's own";
        if (ask.Line is { } line && string.Equals(line, branch, StringComparison.Ordinal)) return $"`{branch}` is {ask.Repository}'s line, which a history edit never rewrites";
        if (new SessionBranches(ask.Home).Entries().Any(each => Same(each.Repository, ask.Repository) && each.Now == branch && each.Branch.StartsWith("daoris/", StringComparison.Ordinal)))
        {
            return $"a session's tree works on `{branch}`";
        }

        if (RecoveryRetention.Made(new LandedBranches(ask.Home).All(), ask.Repository).Contains(branch)) return $"`{branch}` is a branch a landing made, kept as it landed";
        return TogetherGuard.BranchRefusal(ask.Home, ask.Repository, branch) is { } bound ? $"`{branch}` is left as it stands: {bound}" : null;
    }

    /// <summary>
    /// The configured programs a history edit would run: a merge driver in any scope, <c>merge.renormalize</c> beside a filter, and the
    /// signing <c>commit.gpgSign</c> asks; each refused, named with its scope. Null where none is set.
    /// </summary>
    private static async Task<string?> ProgramsAsync(HistoryEditAsk ask, CancellationToken ct)
    {
        var root = ask.Checkout;
        var by = $"by hand: `{string.Join("` then `", Commands(ask.Edit, ask.Branch, ask.Target))}`";
        var (driven, drivers, driverErr) = await ConfigAsync(root, ["--get-regexp", @"^merge\..+\.driver$"], ct).ConfigureAwait(false);
        if (driverErr is not null) return driverErr;
        if (driven is { } driver)
        {
            return $"the {driver} configuration defines `{drivers}`, a merge driver git would run as a program, so no history edit is made here; nothing was changed; {by}";
        }

        var (renormalizes, renormalized, renormalizeErr) = await ConfigAsync(root, ["--type=bool", "--get", "merge.renormalize"], ct).ConfigureAwait(false);
        if (renormalizeErr is not null) return renormalizeErr;
        if (renormalizes is not null && renormalized == "true")
        {
            var (filters, filter, filterErr) = await ConfigAsync(root, ["--get-regexp", @"^filter\..+\.(clean|smudge|process)$"], ct).ConfigureAwait(false);
            if (filterErr is not null) return filterErr;
            if (filters is not null)
            {
                return $"the {renormalizes} configuration sets `merge.renormalize` beside the {filters} configuration's filter `{filter}`, which git would run "
                       + $"as a program while merging, so no history edit is made here; nothing was changed; {by}";
            }
        }

        var (signs, signed, signErr) = await ConfigAsync(root, ["--type=bool", "--get", "commit.gpgSign"], ct).ConfigureAwait(false);
        if (signErr is not null) return signErr;
        return signs is not null && signed == "true"
            ? $"the {signs} configuration sets `commit.gpgSign`, and Daoris does not sign the commits a history edit makes; nothing was changed; {by}"
            : null;
    }

    /// <summary>The first setting <c>git config --show-scope</c> answers: its scope and its key (or value), none, or why git could not say.</summary>
    private static async Task<(string? Scope, string? Value, string? Unread)> ConfigAsync(string root, IReadOnlyList<string> question, CancellationToken ct)
    {
        var (code, said, err) = await WorkingTree.GitAsync(root, ["config", "--show-scope", .. question], ct).ConfigureAwait(false);
        if (code == 1) return (null, null, null);
        if (code != 0) return (null, null, $"git could not read its configuration ({ContentHolds.Said(err)}), so nothing was changed");
        var first = said.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r')).FirstOrDefault();
        if (first is null) return (null, null, null);
        var parts = first.Split('\t', 2);
        var value = parts.Length < 2 ? "" : question[0] == "--get-regexp" ? parts[1].Split(' ')[0] : parts[1].Trim();
        return (parts[0], value, null);
    }

    /// <summary>The tree holding <paramref name="branch"/> (checked out, or rebased or bisected on with a detached HEAD), or that git could not list them.</summary>
    private static async Task<(string? Tree, bool Unread)> OccupantAsync(string root, string branch, CancellationToken ct)
    {
        if (await TreeWorktrees.EntriesAsync(root, ct).ConfigureAwait(false) is not { } entries) return (null, true);
        if (entries.FirstOrDefault(each => each.Branch == branch) is { } entry) return (entry.Path, false);

        var common = TreeLock.CommonDirOf(root);
        var linked = Path.Combine(common, "worktrees");
        string[] folders = Directory.Exists(linked) ? [common, .. Directory.GetDirectories(linked)] : [common];
        foreach (var folder in folders)
        {
            foreach (var (file, named) in new[] { ("rebase-merge/head-name", Heads + branch), ("rebase-apply/head-name", Heads + branch), ("BISECT_START", branch) })
            {
                var path = Path.Combine(folder, file);
                string text;
                try
                {
                    if (!File.Exists(path)) continue;
                    text = File.ReadAllText(path).Trim();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return (null, true);
                }

                if (text == named) return (folder == common ? entries.FirstOrDefault()?.Path ?? root : Path.GetFileName(folder), false);
            }
        }

        return (null, false);
    }

    /// <summary>
    /// Whether a rebase's oldest commit is on any remote-tracking ref (so every later one is too), and every remote-tracking ref at the id read,
    /// to verify in the transaction.
    /// </summary>
    private static async Task<(string? Published, IReadOnlyList<(string, string)> Remotes, string? Unread)> PublishedAsync(string root, string oldest, CancellationToken ct)
    {
        var (holds, holding, holdErr) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", "--contains", oldest, "refs/remotes"], ct).ConfigureAwait(false);
        if (holds != 0) return (null, [], $"git could not read whether its commits are published ({ContentHolds.Said(holdErr)})");
        var holders = holding.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (holders.Count > 0)
        {
            var named = holders.FirstOrDefault(each => !each.EndsWith("/HEAD", StringComparison.Ordinal)) ?? holders[0];
            return (named["refs/remotes/".Length..], [], null);
        }

        var (listed, rows, listErr) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)%09%(objectname)%09%(symref)", "refs/remotes"], ct).ConfigureAwait(false);
        if (listed != 0) return (null, [], $"git could not list the remote-tracking refs ({ContentHolds.Said(listErr)})");
        var remotes = rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r').Split('\t'))
            .Where(parts => parts.Length == 3 && parts[2].Length == 0 && Whole(parts[1])).Select(parts => (parts[0], parts[1])).ToList();
        return (null, remotes, null);
    }

    /// <summary>Why a commit is not taken: a merge, a root, a signature, a message in another encoding; null where it is taken.</summary>
    private static string? Refusal(HistoryEditRead commit) => commit switch
    {
        { Parents.Count: 0 } => $"{Short(commit.Id)} ({commit.Subject}) is a root commit, which has no parent to take its change from",
        { Parents.Count: > 1 } => $"{Short(commit.Id)} ({commit.Subject}) is a merge, and a history edit takes one line of non-merge commits",
        { Signed: true } => $"{Short(commit.Id)} ({commit.Subject}) is signed, and the commit made from it would carry no signature",
        { Encoding: { } encoding } when !encoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase) && !encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase) =>
            $"{Short(commit.Id)}'s message is in {encoding}, which Daoris does not carry over",
        _ => null,
    };

    /// <summary>A commit's stored headers and message, with <paramref name="parents"/> as git's history reads them; null where git
    /// could not read it.</summary>
    private static async Task<HistoryEditRead?> CommitReadAsync(string root, string id, IReadOnlyList<string> parents, CancellationToken ct)
    {
        if (await GitObjectReads.ReadAsync(root, id, GitObjectReads.Whole, ct).ConfigureAwait(false) is not ({ Missing: false, Type: "commit" } answer, var bytes)
            || answer.Count != answer.Size)
        {
            return null;
        }

        var said = GitObjectReads.Text(bytes, answer);
        var cut = said.IndexOf("\n\n", StringComparison.Ordinal);
        var (head, message) = cut < 0 ? (said, "") : (said[..cut], said[(cut + 2)..]);
        var (signed, encoding, author) = (false, (string?)null, "");
        foreach (var line in head.Split('\n'))
        {
            if (line.StartsWith("author ", StringComparison.Ordinal)) author = line["author ".Length..];
            else if (line.StartsWith("encoding ", StringComparison.Ordinal)) encoding = line["encoding ".Length..].Trim();
            else if (line.StartsWith("gpgsig ", StringComparison.Ordinal) || line.StartsWith("gpgsig-sha256 ", StringComparison.Ordinal)) signed = true;
        }

        return new HistoryEditRead(id, parents, signed, encoding, author, message);
    }

    /// <summary>Each commit's parents as git's history reads them, a shallow boundary's none and a graft's its own, which no stored
    /// object's bytes say; null where git could not list them.</summary>
    private static async Task<Dictionary<string, IReadOnlyList<string>>?> ParentsAsync(string root, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-list", "--no-walk=unsorted", "--parents", .. ids, "--"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var parents = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var line in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = line.Split(' ');
            parents[words[0]] = words[1..];
        }

        return parents;
    }

    /// <summary>The commits of <paramref name="range"/>, oldest first; null where git could not list them.</summary>
    private static async Task<IReadOnlyList<string>?> RevListAsync(string root, string range, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-list", "--reverse", "--topo-order", range, "--"], ct).ConfigureAwait(false);
        return code == 0 ? [.. said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Whole)] : null;
    }

    /// <summary>The whole commit id <paramref name="name"/> names, or null.</summary>
    private static async Task<string?> CommitAsync(string root, string name, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], ct).ConfigureAwait(false);
        return code == 0 && Whole(said.Trim()) ? said.Trim() : null;
    }

    /// <summary>The first free recovery name for the branch's old tip, as the content holds name theirs; null where git could not list them.</summary>
    private static async Task<string?> KeptNameAsync(string root, string branch, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", ContentHolds.DiscardedPrefix], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var taken = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 1; ; n++)
        {
            var name = ContentHolds.DiscardedPrefix + branch + (n == 1 ? "" : $"-{n}");
            if (!taken.Contains(name)) return name;
        }
    }

    private static JsonObject PlanJson(HistoryEditAsk ask, HistoryEditPlan plan) => new()
    {
        ["edit"] = ask.Edit, ["branch"] = ask.Branch, ["target"] = ask.Target, ["tip"] = plan.Tip, ["onto"] = plan.Onto,
        ["commits"] = new JsonArray([.. plan.Commits.Select(each => (JsonNode)each.Id)]),
        ["proofs"] = new JsonArray([.. plan.Proofs.Select(each => (JsonNode)new JsonObject { ["ref"] = each.Ref, ["id"] = each.Id })]),
        ["commands"] = new JsonArray([.. plan.Commands.Select(each => (JsonNode)each)]),
    };

    private static bool Whole(string text) => text.Length is 40 or 64 && text.All(char.IsAsciiHexDigit);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Short(string id) => id.Length > 12 ? id[..12] : id;

    private static (HistoryEditPlan?, string?) Refused(string why) => (null, $"{why}, so nothing was changed");

    private static ActRun Refusing(string said) => new(null, null, said, true);
}
