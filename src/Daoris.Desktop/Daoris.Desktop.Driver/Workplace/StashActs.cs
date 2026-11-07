using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The act of this class, by the kind its record carries.</summary>
public static class StashVerbs
{
    public const string Save = "stash";
    public const string Apply = "stash-apply";
    public const string Drop = "stash-drop";
}

/// <summary>A stash Apply as the page pressed it: the stash's commit id, never its ordinal (a Save in between moves every ordinal), and the commit HEAD named when the page read.</summary>
public sealed record StashApplyPress(string Id, string Head);

/// <summary>One stash on the shared list: its commit id, its message, whether it holds untracked files, how many of its paths stand in the way in this checkout now, and whether a Save of Daoris's made it (the only kind a Drop is offered for).</summary>
public sealed record StashEntry(string Id, string Message, bool Untracked, int Collides, bool Mine = false);

/// <summary>A stash Save as the page pressed it: untracked files too or not, an optional message, the commit HEAD named and the tip of the shared stash when the page read.</summary>
public sealed record StashPress(bool Untracked, string? Message, string Head, string? Stash);

/// <summary>
/// What a Save rests on, read at the plan: the tip and the branch, the shared stash's tip, how many tracked files are changed and how many
/// untracked files stand, the other trees that share the stash, why a Save would be refused (null where not), the hooks it runs and who commits;
/// and for Apply the stashes on the shared list newest first and why none could be applied now (null where one could).
/// </summary>
public sealed record StashPlan(
    string? Head, string? Branch, string? Stash, int Tracked, int Untracked, int SharedWith, string? Why, IReadOnlyList<string> Hooks, string? Identity,
    IReadOnlyList<StashEntry> Entries, string? ApplyWhy);

/// <summary>
/// **Stash Save**. One <c>stash</c> step of the act journal, <c>git stash push</c>, tracked files only
/// unless the press asks for untracked ones too, never forced. The stash is <c>refs/stash</c>, shared by every linked tree, the sessions' included,
/// so the plan says so and the press is refused where the stash moved since the page read it. Apply and Drop are acts of their own
/// (<see cref="StashDrops"/>); the answer names the commit saved so the person can put it back by id.
/// </summary>
public sealed class StashActs(ActJournal journal)
{
    private static readonly string[] Hooks = ["reference-transaction"];

    internal ActRuns Runs { get; } = new(journal);

    /// <summary>The plan: what a Save would rest on, read fresh and changing nothing.</summary>
    public async Task<GitAnswer<StashPlan>> PlanAsync(string root, CancellationToken ct, string? repository = null)
    {
        IReadOnlyList<string> commands = ["git rev-parse HEAD", "git rev-parse refs/stash", "git status --porcelain", "git ls-files --others --exclude-standard", "git worktree list --porcelain"];
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = commands };
        }

        var state = await StateAsync(root, ct).ConfigureAwait(false);
        if (state is null) return new(null, GitReadRefusal.NotAnswered, "git could not read this checkout: it may be busy, or git did not start.") { Commands = commands };
        var author = await GitAuthor.OfAsync(journal.Home, root, ct).ConfigureAwait(false);
        var hooks = await ActWorkSteps.HooksAsync(root, Hooks, ct).ConfigureAwait(false);
        var why = state.Head is null ? "this checkout has no commit yet" : state.Tracked == 0 && state.Untracked == 0 ? "there is nothing to save" : null;
        return new(new StashPlan(state.Head, state.Branch, state.Stash, state.Tracked, state.Untracked, state.SharedWith, why, hooks,
            await CommitActs.IdentityAsync(root, author, ct).ConfigureAwait(false), await EntriesAsync(root, state.Stash, repository, ct).ConfigureAwait(false),
            state.Head is null ? "this checkout has no commit yet" : state.Unmerged ? "a file in conflict stands in the index, so it is resolved first"
            : state.Tracked > 0 ? "tracked files are changed here, so save or discard them first" : null), GitReadRefusal.None, null) { Commands = commands };
    }

    /// <summary>
    /// <b>Stash Apply</b>: <c>git stash apply &lt;id&gt;</c> by the stash commit's id, never <c>pop</c>, to a
    /// checkout with no tracked change and no untracked file in the way. A conflict stays in the index for Resolve; the stash is kept either way.
    /// </summary>
    public async Task<ActRun> ApplyAsync(StageContext context, StashApplyPress press, CancellationToken ct)
    {
        if (await StageActs.GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        if (await JudgeApplyAsync(context.Checkout, press, ct).ConfigureAwait(false) is { } stale) return Refused(stale);

        var plan = new JsonObject
        {
            ["verb"] = StashVerbs.Apply, ["id"] = press.Id, ["head"] = press.Head, ["switch"] = true,
            ["shared"] = "refs/stash is shared with every linked tree, sessions' included; an Apply leaves the stash where it stands",
        };
        var act = new ActPlan(StashVerbs.Apply, "window", context.Repository, context.Checkout, [ActWorkSteps.StashApply(press.Id, press.Head)])
        {
            Plan = plan.ToJsonString(),
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                return await JudgeApplyAsync(context.Checkout, press, token).ConfigureAwait(false);
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null) return run;
        var short12 = press.Id[..Math.Min(12, press.Id.Length)];
        if (run.Outcome == ActOutcomes.Done)
        {
            var (_, unmerged, _) = await WorkingTree.GitAsync(context.Checkout, ["ls-files", "--unmerged", "-z"], environment: null, GitBounds.Question, CancellationToken.None).ConfigureAwait(false);
            var said = unmerged.Length > 0
                ? $"Some files are in conflict: resolve them in the Changes tab. The stash is kept in `refs/stash` (`{short12}`)."
                : $"The stash is kept in `refs/stash` (`{short12}`), which every linked tree shares.";
            return run with { Notes = said, Said = $"{run.Said} {said}" };
        }

        return journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Stash)?.Said is { Length: > 0 } why
            ? run with { Notes = char.ToUpperInvariant(why[0]) + why[1..] + ".", Said = $"{run.Said} {char.ToUpperInvariant(why[0])}{why[1..]}." }
            : run;
    }

    // ——— Apply's reads.

    private const int Listed = 20;

    internal static async Task<IReadOnlyList<(string Id, string Message)>?> ListAsync(string root, CancellationToken ct)
    {
        var tip = await ActWorkSteps.StashTipAsync(root, ct).ConfigureAwait(false);
        return tip.Unread ? null : await ListAsync(root, tip.Id, ct).ConfigureAwait(false);
    }

    /// <summary>The list where the shared stash's tip is already read (null: no stash), so the plan asks <c>refs/stash</c> once.</summary>
    internal static async Task<IReadOnlyList<(string Id, string Message)>?> ListAsync(string root, string? tip, CancellationToken ct)
    {
        // The stash list is refs/stash's reflog; `log -g` reads it, where `stash list` is a name the census of writers counts.
        if (tip is null) return [];
        var (code, listed, _) = await WorkingTree.GitAsync(root, ["log", "-g", "-n", Listed.ToString(), "--format=%H%x1f%gs", "refs/stash"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code != 0) return null;
        return [.. listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\x1f')).Where(parts => parts.Length == 2 && WorkingTree.IsCommitId(parts[0])).Select(parts => (parts[0], parts[1]))];
    }

    private async Task<IReadOnlyList<StashEntry>> EntriesAsync(string root, string? stash, string? repository, CancellationToken ct)
    {
        if (await ListAsync(root, stash, ct).ConfigureAwait(false) is not { Count: > 0 } listed) return [];
        var tracked = await TrackedAsync(root, ct).ConfigureAwait(false);
        var mine = repository is null ? new HashSet<string>() : StashDrops.SavedHere(journal, repository, root);
        var entries = new List<StashEntry>();
        foreach (var (id, message) in listed)
        {
            var (untracked, collides) = await CollisionsAsync(root, id, tracked, ct).ConfigureAwait(false);
            entries.Add(new StashEntry(id, message, untracked, collides.Count, mine.Contains(id)));
        }

        return entries;
    }

    private static async Task<HashSet<string>> TrackedAsync(string root, CancellationToken ct)
    {
        var (_, files, _) = await WorkingTree.GitAsync(root, ["ls-files", "-z"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return [.. files.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>Whether the stash holds untracked files, and the paths of its that already stand here untracked: the ones <c>stash apply</c> would refuse to overwrite.</summary>
    private static async Task<(bool Untracked, IReadOnlyList<string> Collides)> CollisionsAsync(string root, string id, HashSet<string> tracked, CancellationToken ct)
    {
        var held = await ActWorkSteps.UntrackedOfAsync(root, id, ct).ConfigureAwait(false) ?? [];
        var (_, added, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "diff", "--name-only", "--diff-filter=A", "--no-renames", "-z", id + "^1", id], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        var adds = added.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => !tracked.Contains(path));
        return (held.Count > 0, [.. held.Concat(adds).Where(path => ActWorkSteps.Exists(root, path)).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>Why an Apply is refused now, or null: the stash not on the list, HEAD moved, tracked changes, a file in conflict or an untracked file in the way.</summary>
    private static async Task<string?> JudgeApplyAsync(string root, StashApplyPress press, CancellationToken ct)
    {
        if (!WorkingTree.IsCommitId(press.Id)) return Said("the stash is named by its commit id, and this is not one");
        if (await StateAsync(root, ct).ConfigureAwait(false) is not { } state) return Said("git could not read the checkout's HEAD, changes and stash");
        if (state.Head is null) return Said("this checkout has no commit yet");
        if (state.Head != press.Head) return Said("HEAD moved since the stash was read");
        if (state.Unmerged) return Said("a file in conflict stands in the index, so it is resolved first");
        if (state.Tracked > 0) return Said("tracked files are changed here; save or discard them first, since an Apply goes onto a clean checkout");
        if (await ListAsync(root, ct).ConfigureAwait(false) is not { } listed) return Said("git could not read the stash list");
        if (!listed.Any(each => each.Id == press.Id)) return Said("that commit is not in the stash list now; it may have been dropped");
        var (_, collides) = await CollisionsAsync(root, press.Id, await TrackedAsync(root, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return collides.Count == 0
            ? null
            : Said($"{(collides.Count == 1 ? "an untracked file" : $"{collides.Count} untracked files")} would be overwritten: {string.Join(", ", collides.Take(3).Select(path => $"`{path}`"))}{(collides.Count > 3 ? ", …" : "")}");
    }

    public async Task<ActRun> RunAsync(StageContext context, StashPress press, CancellationToken ct)
    {
        if (await StageActs.GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var (stale, state) = await JudgeAsync(context.Checkout, press, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        state = state!;

        var author = await GitAuthor.OfAsync(journal.Home, context.Checkout, ct).ConfigureAwait(false);
        if (await CommitActs.IdentityAsync(context.Checkout, author, ct).ConfigureAwait(false) is null)
        {
            return Refused(Said("git has no name and email to save a stash as; set `user.name` and `user.email`, or a git identity that commits as one"));
        }

        var message = OneLine(press.Message);
        var hooks = await ActWorkSteps.HooksAsync(context.Checkout, Hooks, ct).ConfigureAwait(false);
        var plan = new JsonObject
        {
            ["verb"] = StashVerbs.Save, ["head"] = state.Head, ["branch"] = state.Branch, ["stash"] = state.Stash, ["untracked"] = press.Untracked, ["message"] = message,
            ["tracked"] = state.Tracked, ["untrackedFiles"] = state.Untracked, ["switch"] = true, ["sharedWith"] = state.SharedWith,
            ["shared"] = "refs/stash is shared with every linked tree, sessions' included", ["hooks"] = new JsonArray([.. hooks.Select(each => (JsonNode)each)]),
        };
        var act = new ActPlan(StashVerbs.Save, "window", context.Repository, context.Checkout, [ActWorkSteps.Stash(GitAuthor.Arguments(author), press.Untracked, message, state.Head!, state.Stash)])
        {
            Plan = plan.ToJsonString(),
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                return (await JudgeAsync(context.Checkout, press, token).ConfigureAwait(false)).Refusal;
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null) return run;
        if (run.Outcome == ActOutcomes.Done)
        {
            var saved = (await ActWorkSteps.StashTipAsync(context.Checkout, CancellationToken.None).ConfigureAwait(false)).Id;
            var said = saved is null
                ? ""
                : $"Saved as stash `{saved[..Math.Min(12, saved.Length)]}` in `refs/stash`, which every linked tree shares; `git stash apply {saved}` puts it back.";
            return said.Length == 0 ? run : run with { Notes = said, Said = $"{run.Said} {said}" };
        }

        return journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Stash)?.Said is { Length: > 0 } why
            ? run with { Notes = char.ToUpperInvariant(why[0]) + why[1..] + ".", Said = $"{run.Said} {char.ToUpperInvariant(why[0])}{why[1..]}." }
            : run;
    }

    // ——— The reads an act rests on.

    private sealed record State(string? Head, string? Branch, string? Stash, int Tracked, int Untracked, int SharedWith, bool Unmerged);

    /// <summary>The checkout as it stands against a press: why the act is refused now, or null; with the state read.</summary>
    private static async Task<(string? Refusal, State? State)> JudgeAsync(string root, StashPress press, CancellationToken ct)
    {
        if (await StateAsync(root, ct).ConfigureAwait(false) is not { } state) return (Said("git could not read the checkout's HEAD, changes and stash"), null);
        if (state.Head is null) return (Said("this checkout has no commit yet"), null);
        if (state.Head != press.Head) return (Said("HEAD moved since the changes were read"), null);
        if (state.Stash != press.Stash) return (Said("the shared stash moved since it was read; another tree may have saved to it"), null);
        if (state.Unmerged) return (Said("a file in conflict stands in the index, so it is resolved, not stashed"), null);
        if (state.Tracked == 0 && (!press.Untracked || state.Untracked == 0)) return (Said(state.Untracked > 0 ? "no tracked file is changed; ask for the untracked ones to save them" : "there is nothing to save"), null);
        return (null, state);
    }

    private static async Task<State?> StateAsync(string root, CancellationToken ct)
    {
        // Head, branch, changed paths and files in conflict are one status; the stash tip is read after it, as the press compares it.
        if (await PlanStatus.ReadAsync(root, ct).ConfigureAwait(false) is not { } status) return null;
        var stash = await ActWorkSteps.StashTipAsync(root, ct).ConfigureAwait(false);
        if (stash.Unread) return null;
        var (others, untracked, _) = await WorkingTree.GitAsync(root, ["ls-files", "--others", "--exclude-standard", "-z"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        var (listed, trees, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (others != 0 || listed != 0) return null;

        var linked = trees.Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)) - 1;
        return new(status.Head, status.Branch, stash.Id, status.Tracked, Count(untracked), Math.Max(0, linked), status.Unmerged);
    }

    private static int Count(string zero) => zero.Split('\0', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>A message as one line git takes: its first non-blank line, trimmed and bounded; null where none is left.</summary>
    private static string? OneLine(string? message)
    {
        var line = (message ?? "").Replace("\r", "").Split('\n').Select(each => each.Trim()).FirstOrDefault(each => each.Length > 0);
        return line is null ? null : line.Length > 200 ? line[..200] : line;
    }

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);
}
