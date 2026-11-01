using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The hunk a press chose, by its id (<see cref="Hunk.Id"/>), and, where it chose lines of it, their indexes into <see cref="Hunk.Lines"/>.</summary>
public sealed record HunkPick(string Id, IReadOnlyList<int>? Lines = null);

/// <summary>
/// A stage or an unstage as the page pressed it: the file, what it read of the file (HEAD, the index entry and the source of the hunks, as
/// <see cref="FileHunks"/> says them), and the hunks chosen; none chosen is the whole file.
/// </summary>
public sealed record StagePress(string Verb, string Path, string? Head, string Index, string Source, IReadOnlyList<HunkPick> Picks);

/// <summary>
/// What an act rests on, read at the press: the repository and its checkout, the switch <i>Daoris may change this checkout's files</i> (read again
/// under the lock), and the ledger's sessions with their trees. A ledger not given is a ledger not read.
/// </summary>
public sealed record StageContext(string Repository, string Checkout, Func<bool> Allowed, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? Beside);

/// <summary>The acts of this class, by the kind their record carries.</summary>
public static class StageVerbs
{
    public const string Stage = "stage";

    public const string Unstage = "unstage";

    public const string Undo = "stage-undo";
}

/// <summary>
/// **Stage, unstage and their Undo** (GITAPP2h, D204, the git-writes design §4, §8). One index step of the act journal over the file's index
/// entries. What the act leaves is found first on a copy of the index (<c>add</c>, <c>reset</c>, or <c>apply --cached --whitespace=nowarn</c> of a
/// patch from <see cref="GitHunks"/>), never on the index itself; under the checkout lock every fresh read is made again (the switch, the checkout,
/// the ledger, an operation in progress, HEAD, the entry and the source's bytes), and the step then replaces the entries under git's own
/// <c>index.lock</c> where they still equal the plan's. The entries before are kept by the act's own ref until its record expires, so Undo can
/// put them back where each still equals what the act left.
/// </summary>
public sealed class StageActs(ActJournal journal)
{
    private static readonly string[] Operations = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG", "rebase-merge", "rebase-apply"];

    internal ActRuns Runs { get; } = new(journal);

    public async Task<ActRun> RunAsync(StageContext context, StagePress press, CancellationToken ct)
    {
        var verb = press.Verb;
        if (verb is not (StageVerbs.Stage or StageVerbs.Unstage)) return Refused($"`{verb}` is no act of the Changes tab");
        var side = verb == StageVerbs.Stage ? GitHunks.Unstaged : GitHunks.Staged;
        if (PathRefusal(press.Path) is { } bad) return Refused(bad);
        if (await GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var (stale, before) = await StateAsync(context.Checkout, press, side, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        before = before!;
        var layout = (await LayoutAsync(context.Checkout).ConfigureAwait(false))!.Value;

        var made = await AfterAsync(context.Checkout, layout.GitDir, press, side, ct).ConfigureAwait(false);
        if (made.Refusal is not null) return Refused(made.Refusal);
        var after = made.After!;

        var plan = new JsonObject
        {
            ["verb"] = verb, ["path"] = press.Path, ["head"] = press.Head, ["hooks"] = new JsonArray(), ["switch"] = true,
            ["hunks"] = new JsonArray([.. press.Picks.Select(pick => (JsonNode)JsonValue.Create(pick.Id)!)]),
        };
        var act = new ActPlan(verb, "window", context.Repository, context.Checkout, [ActSteps.Index(layout.GitDir, before, after)])
        {
            Plan = plan.ToJsonString(),
            Keep = [.. before.Concat(after).DistinctBy(entry => (entry.Id, entry.Mode))],
            Judge = async token =>
            {
                if (await GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                var (refusal, now) = await StateAsync(context.Checkout, press, side, token).ConfigureAwait(false);
                if (refusal is not null) return refusal;
                return ActIndex.Same(now!, before) ? null : Said("the index's entries changed since they were read");
            },
        };
        return await Runs.RunAsync(act, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Put an act's index entries back, under the checkout lock, only where each still equals what the act left. An entry that changed is named
    /// and nothing is restored.
    /// </summary>
    public Task<ActRun> UndoAsync(StageContext context, string act, CancellationToken ct) =>
        UndoIndexAsync(context, act, [StageVerbs.Stage, StageVerbs.Unstage, StageVerbs.Undo], duringOperation: false, ct);

    /// <summary>
    /// <see cref="UndoAsync"/> for the acts of <paramref name="kinds"/>, which may stand in a merge or a rebase (Resolve's <i>Mark resolved</i>
    /// is made there) where <paramref name="duringOperation"/>.
    /// </summary>
    internal async Task<ActRun> UndoIndexAsync(StageContext context, string act, IReadOnlyList<string> kinds, bool duringOperation, CancellationToken ct)
    {
        if (await GuardAsync(context, "undoing it", ct, duringOperation).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        if (journal.Act(act) is not { } record) return Refused($"Daoris holds no act {act}");
        if (!kinds.Contains(record.Kind)) return Refused($"act {act} is no {(duringOperation ? "resolve" : "stage or unstage")}, so it is not undone here");
        if (TreeLock.CommonDirOf(record.Checkout) != TreeLock.CommonDirOf(context.Checkout)) return Refused($"act {act} was made in another checkout");
        if (record.Open || record.Outcome != ActOutcomes.Done) return Refused($"act {act} did not finish as done ({record.Outcome ?? "still open"}), so it is not undone here");
        if (record.ExpiredAt is not null) return Refused($"act {act}'s record expired, with the blobs it kept");
        var step = journal.Steps(act).FirstOrDefault(each => each.Kind == ActStepKinds.Index && each.Rollback is not null);
        if (step?.Rollback is not { } rollback) return Refused($"act {act} kept no way back");

        var json = JsonNode.Parse(rollback)!;
        var (result, original) = (ActSteps.EntriesOf(json["before"]), ActSteps.EntriesOf(json["after"]));
        var path = result.Concat(original).Select(entry => entry.Path).Distinct(StringComparer.Ordinal).ToList();
        async Task<string?> JudgeAsync(CancellationToken token)
        {
            if (await GuardAsync(context, "undoing it", token, duringOperation).ConfigureAwait(false) is { } guard) return guard;
            if (await ActIndex.EntriesAsync(context.Checkout, path, null).ConfigureAwait(false) is not { } now) return Said("git could not list the index's entries");
            var changed = path.Where(each => !ActIndex.Same([.. now.Where(entry => entry.Path == each)], [.. result.Where(entry => entry.Path == each)])).ToList();
            return changed.Count == 0
                ? null
                : Said($"{string.Join(", ", changed.Select(each => $"`{each}`"))} changed since act {act} left {(changed.Count == 1 ? "it" : "them")}, so none was restored");
        }

        if (await JudgeAsync(ct).ConfigureAwait(false) is { } refusal) return Refused(refusal);
        var layout = (await LayoutAsync(context.Checkout).ConfigureAwait(false))!.Value;
        var plan = new ActPlan(StageVerbs.Undo, "window", context.Repository, context.Checkout, [new ActStepPlan(ActStepKinds.Index, rollback, rollback, step.Arguments)])
        {
            Plan = new JsonObject { ["verb"] = StageVerbs.Undo, ["undoes"] = act, ["hooks"] = new JsonArray(), ["switch"] = true, ["gitDir"] = layout.GitDir }.ToJsonString(),
            Keep = [.. result.Concat(original).DistinctBy(entry => (entry.Id, entry.Mode))],
            Judge = JudgeAsync,
        };
        return await Runs.RunAsync(plan, ct).ConfigureAwait(false);
    }

    // ——— The reads an act rests on.

    /// <summary>The switch, the checkout's identity, the ledger and an operation in progress: why the act is refused now, or null.</summary>
    internal static async Task<string?> GuardAsync(StageContext context, string what, CancellationToken ct, bool duringOperation = false)
    {
        if (!context.Allowed())
        {
            return Said($"Daoris may not change `{context.Repository}`'s checkout's files; turn on *Daoris may change this checkout's files* in Setup → Reach to make this act");
        }

        if (!Directory.Exists(context.Checkout) || !await WorkingTree.IsTopLevelAsync(context.Checkout, ct).ConfigureAwait(false))
        {
            return Said($"`{context.Repository}`'s checkout is not the top of a repository of its own");
        }

        if (await LayoutAsync(context.Checkout).ConfigureAwait(false) is not { } layout) return Said($"git could not read `{context.Repository}`'s checkout");
        if (!string.Equals(layout.GitDir, layout.CommonDir, StringComparison.OrdinalIgnoreCase))
        {
            return Said($"`{context.Repository}`'s checkout is a session's tree, which a person's act never writes");
        }

        if (await CheckoutBeside.RefusesAsync(context.Beside, context.Checkout, context.Repository, what, "is refused; try again once it ends", ct).ConfigureAwait(false) is { } beside)
        {
            return Said(beside);
        }

        // Resolve's acts are made in a merge, a pick or a rebase, where the file is in conflict: they leave the operation to the person.
        if (duringOperation) return null;
        return Operations.FirstOrDefault(name => File.Exists(Path.Combine(layout.GitDir, name)) || Directory.Exists(Path.Combine(layout.GitDir, name))) is { } running
            ? Said($"a merge, rebase, pick, revert or bisect is in progress in `{context.Repository}`'s checkout (git's `{running}` stands), which this act does not meet")
            : null;
    }

    /// <summary>HEAD, the index entry and the source's bytes against what the press read, and the path's index entries as they stand.</summary>
    private static async Task<(string? Refusal, IReadOnlyList<ActEntry>? Before)> StateAsync(string root, StagePress press, string side, CancellationToken ct)
    {
        if (await GitHunks.FacesAsync(root, press.Path, side, ct).ConfigureAwait(false) is not { } faces) return (Said("git could not read the file's state"), null);
        if (faces.Index == "unmerged") return (Said($"`{press.Path}` is in conflict; it is resolved, not staged"), null);
        if (faces.Head != press.Head) return (Said("HEAD moved since the file was read"), null);
        if (faces.Index != press.Index) return (Said($"the index's entry for `{press.Path}` changed since it was read"), null);
        if (faces.Source != press.Source)
        {
            return (Said(side == GitHunks.Unstaged ? $"`{press.Path}`'s bytes changed since its hunks were read" : $"HEAD's `{press.Path}` is no longer the one the hunks were read against"), null);
        }

        var before = await ActIndex.EntriesAsync(root, [press.Path], null).ConfigureAwait(false);
        return before is null ? (Said("git could not list the index's entries"), null) : (null, before);
    }

    /// <summary>What the index would hold for the file after the act, found on a copy of the index; refused in words where it would change nothing.</summary>
    private static async Task<(string? Refusal, IReadOnlyList<ActEntry>? After)> AfterAsync(string root, string gitDir, StagePress press, string side, CancellationToken ct)
    {
        var copy = Path.Combine(gitDir, $"index.daoris-plan-{Guid.NewGuid():N}");
        try
        {
            var index = Path.Combine(gitDir, "index");
            if (File.Exists(index)) File.Copy(index, copy, overwrite: true);
            var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = copy };
            var literal = ":(literal)" + press.Path;
            if (press.Picks.Count == 0)
            {
                IReadOnlyList<string> whole = press.Verb == StageVerbs.Stage ? ["add", "-A", "--", literal]
                    : press.Head is null ? ["rm", "--cached", "-r", "-q", "--ignore-unmatch", "--", literal] : ["reset", "-q", "HEAD", "--", literal];
                var (code, _, err) = await WorkingTree.GitAsync(root, whole, environment, timeout: null, ct).ConfigureAwait(false);
                if (code != 0) return (Said($"git would not {press.Verb} `{press.Path}` ({ContentHolds.Said(err)})"), null);
            }
            else
            {
                if (await GitHunks.DiffAsync(root, press.Path, side, press.Head, ct).ConfigureAwait(false) is not { } diff) return (Said("git could not read the file's hunks"), null);
                if (diff.Binary) return (Said($"`{press.Path}` is binary, so it is {press.Verb}d whole"), null);
                var chosen = new List<(Hunk, IReadOnlySet<int>?)>();
                foreach (var pick in press.Picks)
                {
                    if (diff.Hunks.FirstOrDefault(each => each.Id == pick.Id) is not { } hunk) return (Said($"`{press.Path}`'s hunks changed since they were read"), null);
                    if (pick.Lines is { } lines && lines.Any(at => at < 0 || at >= hunk.Lines.Count || hunk.Lines[at].Kind is ' ' or '\\')) return (Said("a chosen line is no change of the hunk"), null);
                    chosen.Add((hunk, pick.Lines?.ToHashSet()));
                }

                if (GitHunks.Patch(diff.Header, chosen, reverse: press.Verb == StageVerbs.Unstage) is not { } patch) return (Said("the lines chosen change nothing"), null);
                if (patch.Contains('�')) return (Said($"`{press.Path}` is not UTF-8 text, so it is {press.Verb}d whole, never by hunk or line"), null);
                string[] arguments = press.Verb == StageVerbs.Stage
                    ? ["apply", "--cached", "--whitespace=nowarn", "-"]
                    : ["apply", "--cached", "--reverse", "--whitespace=nowarn", "-"];
                var (code, _, err) = await WorkingTree.GitAsync(root, arguments, environment, patch, ct).ConfigureAwait(false);
                if (code != 0) return (Said($"git would not apply the chosen hunks to the index ({ContentHolds.Said(err)})"), null);
            }

            var after = await ActIndex.EntriesAsync(root, [press.Path], copy).ConfigureAwait(false);
            if (after is null) return (Said("git could not list the replacement index's entries"), null);
            var before = await ActIndex.EntriesAsync(root, [press.Path], null).ConfigureAwait(false);
            return before is not null && ActIndex.Same(before, after) ? (Said($"the index already holds what {press.Verb} would leave"), null) : (null, after);
        }
        finally
        {
            try
            {
                File.Delete(copy);
            }
            catch (IOException)
            {
                // A copy left in the git directory is named for its act and harmless.
            }
        }
    }

    internal static async Task<(string GitDir, string CommonDir)?> LayoutAsync(string checkout)
    {
        var (code, said, _) = await WorkingTree.GitAsync(
            checkout, ["rev-parse", "--absolute-git-dir", "--git-common-dir"], environment: null, GitBounds.Question, CancellationToken.None).ConfigureAwait(false);
        var lines = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return code != 0 || lines.Length < 2 ? null : (System.IO.Path.GetFullPath(lines[0]), System.IO.Path.GetFullPath(System.IO.Path.Combine(checkout, lines[1])));
    }

    private static string? PathRefusal(string path) =>
        path.Length == 0 || System.IO.Path.IsPathRooted(path) || path.Split('/', '\\').Contains("..") ? Said("that is not a path in the repository") : null;

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);
}
