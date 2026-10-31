using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The acts of this class, by the kind their record carries.</summary>
public static class ResolveVerbs
{
    /// <summary>The conflicts of a file settled, its bytes replaced; the index is left for <see cref="Mark"/>.</summary>
    public const string Resolve = "resolve";

    /// <summary>The file added, its stages gone from the index.</summary>
    public const string Mark = "resolve-mark";

    /// <summary>The file's replaced bytes put back.</summary>
    public const string Undo = "resolve-undo";
}

/// <summary>What a conflict's pick takes.</summary>
public static class ResolveTakes
{
    public const string Ours = "ours";

    public const string Theirs = "theirs";

    public const string Both = "both";
}

/// <summary>The side or sides chosen for one conflict, by the conflict's id (<see cref="ConflictHunk.Id"/>).</summary>
public sealed record ResolvePick(string Id, string Take);

/// <summary>
/// A resolve or a mark as the page pressed it: the file, and what it read of it (HEAD, the three stages as <c>mode id</c>, the worktree file's
/// id); a resolve carries a pick for every conflict, a mark none.
/// </summary>
public sealed record ResolvePress(string Verb, string Path, string? Head, string Base, string Ours, string Theirs, string Worktree, IReadOnlyList<ResolvePick> Picks);

/// <summary>
/// **Resolve a conflicted file** (GITAPP2j, D204, the git-writes design §8). <i>Resolve</i> is one file step of the act journal: the file's
/// bytes become the stages' merge with each conflict taken ours, theirs or both, where the file still holds the bytes the page showed. The bytes
/// it replaces and the bytes it writes are files beside the record, and the three stages are held by the act's own ref, so a restart loses
/// neither. <i>Mark resolved</i> is a second act, one index step as stage's is, in which <c>git add</c> on a copy finds the entry that
/// replaces the stages. Undo of each puts back what it replaced, only where the file or the entries are still what the act left.
/// </summary>
public sealed partial class ResolveActs(ActJournal journal)
{
    internal ActRuns Runs { get; } = new(journal);

    /// <summary>A line git writes at the start or the end of a conflict it was asked to leave: a file that holds both was not resolved.</summary>
    [GeneratedRegex(@"^(?:<{7,}(?: |$)|>{7,}(?: |$))", RegexOptions.Multiline)]
    private static partial Regex Markers();

    public async Task<ActRun> RunAsync(StageContext context, ResolvePress press, CancellationToken ct)
    {
        if (press.Verb is not (ResolveVerbs.Resolve or ResolveVerbs.Mark)) return Refused($"`{press.Verb}` is no act of a file in conflict");
        if (press.Path.Length == 0 || Path.IsPathRooted(press.Path) || press.Path.Split('/', '\\').Contains("..")) return Refused(Said("that is not a path in the repository"));
        if (await StageActs.GuardAsync(context, "this act", ct, duringOperation: true).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var (stale, faces) = await StateAsync(context.Checkout, press, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        return press.Verb == ResolveVerbs.Resolve
            ? await ResolveAsync(context, press, faces!, ct).ConfigureAwait(false)
            : await MarkAsync(context, press, faces!, ct).ConfigureAwait(false);
    }

    private async Task<ActRun> ResolveAsync(StageContext context, ResolvePress press, GitConflicts.Faces faces, CancellationToken ct)
    {
        var (merged, why) = await GitConflicts.MergeAsync(context.Checkout, faces, ct).ConfigureAwait(false);
        if (merged is null) return Refused(Said($"`{press.Path}`: {why}"));
        var count = GitConflicts.Count(merged);
        if (press.Picks.Count != count || press.Picks.Select(pick => pick.Id).Distinct().Count() != count
            || press.Picks.Any(pick => pick.Take is not (ResolveTakes.Ours or ResolveTakes.Theirs or ResolveTakes.Both)))
        {
            return Refused(Said($"choose ours, theirs or both for every conflict of `{press.Path}`, once each ({count} {(count == 1 ? "conflict" : "conflicts")})"));
        }

        if (GitConflicts.Resolved(merged, press.Picks) is not { } text) return Refused(Said($"`{press.Path}`'s conflicts changed since they were read"));
        var file = Path.Combine(context.Checkout, press.Path.Replace('/', Path.DirectorySeparatorChar));
        if (new FileInfo(file).LinkTarget is not null) return Refused(Said($"`{press.Path}` is a link, which is resolved by hand"));
        byte[] replaced;
        try
        {
            replaced = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refused(Said($"`{press.Path}` could not be read ({error.Message})"));
        }

        var written = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        var id = Guid.NewGuid().ToString("N");
        var dir = ActFiles.Dir(journal.Home, id);
        var (replacedFile, writtenFile) = (Path.Combine(dir, "replaced"), Path.Combine(dir, "written"));
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(replacedFile, replaced, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(writtenFile, written, ct).ConfigureAwait(false);

        var plan = new JsonObject
        {
            ["verb"] = press.Verb, ["path"] = press.Path, ["head"] = press.Head, ["hooks"] = new JsonArray(), ["switch"] = true,
            ["stages"] = new JsonObject { ["base"] = press.Base, ["ours"] = press.Ours, ["theirs"] = press.Theirs }, ["worktree"] = press.Worktree,
            ["picks"] = new JsonArray([.. press.Picks.Select(pick => (JsonNode)new JsonObject { ["id"] = pick.Id, ["take"] = pick.Take })]),
        };
        var act = new ActPlan(
            ResolveVerbs.Resolve, "window", context.Repository, context.Checkout,
            [ActSteps.Bytes(file, press.Path, ActFiles.Digest(replaced), ActFiles.Digest(written), replacedFile, writtenFile)])
        {
            Id = id,
            Plan = plan.ToJsonString(),
            Keep = faces.Stages,
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token, duringOperation: true).ConfigureAwait(false) is { } guard) return guard;
                return (await StateAsync(context.Checkout, press, token).ConfigureAwait(false)).Refusal;
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        if (run.Refused) ActFiles.Remove(journal.Home, id);
        return run;
    }

    private async Task<ActRun> MarkAsync(StageContext context, ResolvePress press, GitConflicts.Faces faces, CancellationToken ct)
    {
        var file = Path.Combine(context.Checkout, press.Path.Replace('/', Path.DirectorySeparatorChar));
        var found = File.Exists(file) ? Markers().Matches(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false)).Select(each => each.Value[0]).ToList() : [];
        if (found.Contains('<') && found.Contains('>'))
        {
            return Refused(Said($"`{press.Path}` still holds conflict markers; resolve it, or remove them, and then mark it resolved"));
        }

        var layout = (await StageActs.LayoutAsync(context.Checkout).ConfigureAwait(false))!.Value;
        var (refusal, after) = await AfterAsync(context.Checkout, layout.GitDir, press.Path, ct).ConfigureAwait(false);
        if (refusal is not null) return Refused(refusal);

        var plan = new JsonObject
        {
            ["verb"] = press.Verb, ["path"] = press.Path, ["head"] = press.Head, ["hooks"] = new JsonArray(), ["switch"] = true,
            ["stages"] = new JsonObject { ["base"] = press.Base, ["ours"] = press.Ours, ["theirs"] = press.Theirs }, ["worktree"] = press.Worktree,
        };
        var act = new ActPlan(ResolveVerbs.Mark, "window", context.Repository, context.Checkout, [ActSteps.Index(layout.GitDir, faces.Stages, after!)])
        {
            Plan = plan.ToJsonString(),
            Keep = [.. faces.Stages.Concat(after!).DistinctBy(entry => (entry.Id, entry.Mode))],
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token, duringOperation: true).ConfigureAwait(false) is { } guard) return guard;
                return (await StateAsync(context.Checkout, press, token).ConfigureAwait(false)).Refusal;
            },
        };
        return await Runs.RunAsync(act, ct).ConfigureAwait(false);
    }

    /// <summary>What the index would hold for the file once added, found on a copy of the index and never on the index.</summary>
    private static async Task<(string? Refusal, IReadOnlyList<ActEntry>? After)> AfterAsync(string root, string gitDir, string path, CancellationToken ct)
    {
        var copy = Path.Combine(gitDir, $"index.daoris-plan-{Guid.NewGuid():N}");
        try
        {
            var index = Path.Combine(gitDir, "index");
            if (File.Exists(index)) File.Copy(index, copy, overwrite: true);
            var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = copy };
            var (code, _, err) = await WorkingTree.GitAsync(root, ["add", "--", ":(literal)" + path], environment, timeout: null, ct).ConfigureAwait(false);
            if (code != 0) return (Said($"git would not add `{path}` ({ContentHolds.Said(err)})"), null);
            var after = await ActIndex.EntriesAsync(root, [path], copy).ConfigureAwait(false);
            return after is null || after.Count == 0 || after.Any(entry => entry.Stage != 0)
                ? (Said($"git did not leave `{path}` as a single entry"), null)
                : (null, after);
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

    /// <summary>
    /// Put an act's replaced bytes back, or its stages: only where the file still holds the bytes the resolve wrote, or each entry is still what the
    /// mark left; otherwise the change is named and nothing is restored.
    /// </summary>
    public async Task<ActRun> UndoAsync(StageContext context, string act, CancellationToken ct)
    {
        if (journal.Act(act) is { Kind: ResolveVerbs.Mark }) return await new StageActs(journal).UndoIndexAsync(context, act, [ResolveVerbs.Mark], duringOperation: true, ct).ConfigureAwait(false);
        if (await StageActs.GuardAsync(context, "undoing it", ct, duringOperation: true).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        if (journal.Act(act) is not { } record) return Refused($"Daoris holds no act {act}");
        if (record.Kind != ResolveVerbs.Resolve) return Refused($"act {act} is no resolve, so it is not undone here");
        if (TreeLock.CommonDirOf(record.Checkout) != TreeLock.CommonDirOf(context.Checkout)) return Refused($"act {act} was made in another checkout");
        if (record.Open || record.Outcome != ActOutcomes.Done) return Refused($"act {act} did not finish as done ({record.Outcome ?? "still open"}), so it is not undone here");
        if (record.ExpiredAt is not null) return Refused($"act {act}'s record expired, with the bytes it kept");
        var step = journal.Steps(act).FirstOrDefault(each => each.Kind == ActStepKinds.Bytes && each.Rollback is not null);
        if (step?.Rollback is not { } rollback) return Refused($"act {act} kept no way back");

        var back = JsonNode.Parse(rollback)!;
        var (path, relative, before, kept) = (back["path"]!.GetValue<string>(), back["relative"]!.GetValue<string>(), back["before"]!.GetValue<string>(), back["written"]!.GetValue<string>());
        async Task<string?> JudgeAsync(CancellationToken token)
        {
            if (await StageActs.GuardAsync(context, "undoing it", token, duringOperation: true).ConfigureAwait(false) is { } guard) return guard;
            if (!File.Exists(kept)) return Said($"the bytes act {act} replaced are gone");
            try
            {
                return ActFiles.Digest(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)) == before
                    ? null
                    : Said($"`{relative}` changed since act {act} left it, so its bytes were not put back");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return Said($"`{relative}` could not be read ({error.Message})");
            }
        }

        if (await JudgeAsync(ct).ConfigureAwait(false) is { } refusal) return Refused(refusal);
        var plan = new ActPlan(ResolveVerbs.Undo, "window", context.Repository, context.Checkout, [new ActStepPlan(ActStepKinds.Bytes, rollback, rollback, step.Arguments)])
        {
            Plan = new JsonObject { ["verb"] = ResolveVerbs.Undo, ["undoes"] = act, ["hooks"] = new JsonArray(), ["switch"] = true }.ToJsonString(),
            Judge = JudgeAsync,
        };
        return await Runs.RunAsync(plan, ct).ConfigureAwait(false);
    }

    /// <summary>The stages, HEAD and the worktree file against what the press read; the file's stages as they stand.</summary>
    private static async Task<(string? Refusal, GitConflicts.Faces? Faces)> StateAsync(string root, ResolvePress press, CancellationToken ct)
    {
        if (await GitConflicts.FacesAsync(root, press.Path, ct).ConfigureAwait(false) is not { } faces) return (Said("git could not read the file's state"), null);
        if (faces.Stages.Count == 0) return (Said($"`{press.Path}` is not in conflict"), null);
        string Stage(int number) => faces.Stages.FirstOrDefault(entry => entry.Stage == number) is { } entry ? $"{entry.Mode} {entry.Id}" : "";
        if (Stage(1) != press.Base || Stage(2) != press.Ours || Stage(3) != press.Theirs) return (Said($"the stages of `{press.Path}` changed since they were read"), null);
        if (faces.Head != press.Head) return (Said("HEAD moved since the file was read"), null);
        if (faces.Worktree != press.Worktree) return (Said($"`{press.Path}`'s bytes changed since they were read"), null);
        return (null, faces);
    }

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);
}
