using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A branch or a lightweight tag to create: <paramref name="Kind"/> is <c>branch</c> or <c>tag</c>, <paramref name="At"/> the commit it names (HEAD where null).</summary>
public sealed record RefAsk(string Repository, string Checkout, string Kind, string Name, string? At);

/// <summary>
/// A branch or a lightweight tag to delete: <paramref name="At"/> the commit the page showed it at (refused where it stands elsewhere),
/// <paramref name="Line"/> the repository's line, which is never deleted.
/// </summary>
public sealed record RefDeleteAsk(string Repository, string Checkout, string Kind, string Name, string? At = null, string? Line = null);

/// <summary>A branch or a lightweight tag to rename to <paramref name="To"/>; <paramref name="At"/> and <paramref name="Line"/> as a delete's.</summary>
public sealed record RefRenameAsk(string Repository, string Checkout, string Kind, string Name, string To, string? At = null, string? Line = null);

/// <summary>
/// **Create or delete a branch or a lightweight tag, and undo either**. One
/// <c>create</c>, or one transaction of the <c>delete</c>, the recovery ref's <c>create</c> and each proof ref's <c>verify</c>, through
/// <see cref="RefTransaction"/> run as an act of the journal (<see cref="ActRuns"/>), so it holds the checkout alone, is journaled, and is
/// reconciled by its postconditions where its run died. The undo is an act of its own: a create's <c>delete</c> at the id the create made,
/// refused where the ref stands anywhere else; a delete's ref made again where the name is free, its section put back under git's
/// <c>config.lock</c>. A ref act writes no working tree, so it needs no checkout switch.
/// </summary>
public static class GitRefActs
{
    public const string CreateKind = "ref.create";

    public const string UndoKind = "ref.undo";

    public const string DeleteKind = "ref.delete";

    public const string RenameKind = "ref.rename";

    public const string FinishKind = "ref.finish";

    /// <summary>How many holders of the deleted ref's commit are verified with it: the ones the page would name.</summary>
    private const int HoldersVerified = 20;

    /// <summary>The commands a person types in the checkout for the same act.</summary>
    public static IReadOnlyList<string> Commands(RefAsk ask) =>
        [ask.Kind == "tag" ? $"git tag {ask.Name} {ask.At ?? "HEAD"}" : $"git branch {ask.Name} {ask.At ?? "HEAD"}"];

    /// <summary>The ref's full name, or null where the kind is neither.</summary>
    public static string? FullName(string kind, string name) => kind switch { "branch" => "refs/heads/" + name, "tag" => "refs/tags/" + name, _ => null };

    /// <summary>What the create would do now, read without the lock and written nowhere: the whole commit id it names, or why it is refused.</summary>
    public static async Task<(string? Id, string? Refusal)> PlanAsync(RefAsk ask, CancellationToken ct)
    {
        if (await JudgeAsync(ask, null, ct).ConfigureAwait(false) is { } refusal) return (null, refusal);
        return (await CommitAsync(ask.Checkout, ask.At ?? "HEAD", ct).ConfigureAwait(false), null);
    }

    /// <summary>Create the ref: refused before anything is written where the name or the commit is not as asked, else run to its close.</summary>
    public static async Task<ActRun> CreateAsync(ActRuns runs, RefAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        if (await JudgeAsync(ask, null, ct).ConfigureAwait(false) is { } refusal) return new ActRun(null, null, refusal, true);
        var id = await CommitAsync(ask.Checkout, ask.At ?? "HEAD", ct).ConfigureAwait(false);
        var full = FullName(ask.Kind, ask.Name)!;
        var step = ActSteps.Ref(new RefTransaction().Create(full, id!), [(full, null, id)]);
        var plan = new JsonObject { ["kind"] = ask.Kind, ["name"] = ask.Name, ["ref"] = full, ["at"] = id, ["commands"] = new JsonArray([.. Commands(ask).Select(each => (JsonNode)each)]) };
        return await runs.RunAsync(new ActPlan(CreateKind, press, ask.Repository, ask.Checkout, [step])
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed, Judge = token => JudgeAsync(ask, id, token),
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Undo the create <paramref name="act"/> made: its ref deleted only at the id it was made at, refused where that act is no closed create
    /// of this repository, or the ref has moved or gone.
    /// </summary>
    public static async Task<ActRun> UndoAsync(ActRuns runs, string repository, string checkout, string act, string press, string? confirmed, CancellationToken ct)
    {
        var journal = runs.Journal;
        if (journal.Act(act) is not { } record) return Refused($"no act `{act}` is recorded, so nothing was undone");
        if (record.Kind is not (CreateKind or DeleteKind or RenameKind or FinishKind)) return Refused($"act `{act}` is not a create, a delete or a rename, so nothing was undone");
        if (!string.Equals(record.Repository, repository, StringComparison.OrdinalIgnoreCase)) return Refused($"act `{act}` was made in another repository, so nothing was undone");
        // A rename stopped between its ref and its section steps is undone too: its ref moves back alone.
        var partial = record.Kind == RenameKind && record.Outcome == ActOutcomes.Partial;
        if (record.Open || (record.Outcome != ActOutcomes.Done && !partial)) return Refused($"act `{act}` did not finish as done ({record.Outcome ?? "still open"}), so there is nothing to undo");
        if (record.Kind == DeleteKind) return await UndoDeleteAsync(runs, record, checkout, press, confirmed, ct).ConfigureAwait(false);
        if (record.Kind is RenameKind or FinishKind) return await UndoRenameAsync(runs, record, checkout, press, confirmed, ct).ConfigureAwait(false);
        var moved = JsonNode.Parse(journal.Steps(act).Single().Postconditions)!["refs"]![0]!;
        var (reference, id) = (moved["ref"]!.GetValue<string>(), moved["after"]!.GetValue<string>());

        var step = ActSteps.Ref(new RefTransaction().Delete(reference, id), [(reference, id, null)]);
        var plan = new JsonObject { ["undo"] = act, ["ref"] = reference, ["at"] = id };
        return await runs.RunAsync(new ActPlan(UndoKind, press, repository, checkout, [step])
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed,
            Judge = async token =>
            {
                var (code, stands, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", reference], token).ConfigureAwait(false);
                return code == 0 && stands.Trim() == id
                    ? null
                    : $"`{ShortName(reference)}` no longer stands at the commit act `{act}` made it at ({(code == 0 ? "it was moved" : "it is gone")}), so it was not deleted";
            },
        }, ct).ConfigureAwait(false);
    }

    // ——— Delete.

    /// <summary>The commands a person types in the checkout for the same act.</summary>
    public static IReadOnlyList<string> Commands(RefDeleteAsk ask) => [ask.Kind == "tag" ? $"git tag -d {ask.Name}" : $"git branch -D {ask.Name}"];

    /// <summary>What the delete would do now, read without the lock and written nowhere: the whole commit id it deletes, or why it is refused.</summary>
    public static async Task<(string? Id, string? Refusal)> PlanDeleteAsync(string home, RefDeleteAsk ask, CancellationToken ct)
    {
        var (read, refusal) = await ReadDeleteAsync(home, ask, null, ct).ConfigureAwait(false);
        return (read?.Id, refusal);
    }

    /// <summary>
    /// Delete the ref: refused before anything is written where it is a held branch, an annotated tag, occupied, or not at the commit shown;
    /// else the delete, the recovery ref and each holder's <c>verify</c> in one transaction, then the branch's section removed under
    /// <c>config.lock</c>. A branch a tree checked out meanwhile is made again, and the recovery ref taken back with it.
    /// </summary>
    public static async Task<ActRun> DeleteAsync(ActRuns runs, RefDeleteAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        var home = runs.Journal.Home;
        var (read, refusal) = await ReadDeleteAsync(home, ask, null, ct).ConfigureAwait(false);
        if (read is null) return Refused(refusal!);

        var (recovery, made, taken) = await RecoveryNameAsync(ask.Checkout, ask.Name, read.Id, ct).ConfigureAwait(false);
        if (recovery is null) return Refused(taken!);

        var transaction = new RefTransaction().Verify(read.Holders).Delete(read.Full, read.Id);
        var moves = new List<(string Ref, string? Before, string? After)> { (read.Full, read.Id, null) };
        if (made)
        {
            transaction.Create(recovery, read.Id);
            moves.Add((recovery, null, read.Id));
        }
        else
        {
            transaction.Verify(recovery, read.Id);
        }

        var steps = new List<ActStepPlan>();
        try
        {
            steps.Add(ActSteps.Ref(transaction, moves));
        }
        catch (ArgumentException error)
        {
            return Refused($"{error.Message}, so nothing was deleted");
        }

        JsonObject? section = null;
        if (ask.Kind == "branch" && await SectionAsync(ask.Checkout, ask.Name).ConfigureAwait(false) is { } kept)
        {
            section = kept;
            steps.Add(ActSteps.Config(kept["file"]!.GetValue<string>(), "local", kept["section"]!.GetValue<string>(), null, kept["text"]!.GetValue<string>()));
        }

        var plan = new JsonObject
        {
            ["kind"] = ask.Kind, ["name"] = ask.Name, ["ref"] = read.Full, ["at"] = read.Id, ["recovery"] = recovery, ["recoveryMade"] = made,
            ["holders"] = new JsonArray([.. read.Holders.Select(each => (JsonNode)each.Ref)]), ["section"] = section,
            ["commands"] = new JsonArray([.. Commands(ask).Select(each => (JsonNode)each)]),
        };
        return await runs.RunAsync(new ActPlan(DeleteKind, press, ask.Repository, ask.Checkout, steps)
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed,
            Judge = async token => (await ReadDeleteAsync(home, ask, read.Id, token).ConfigureAwait(false)).Refusal,
            After = ordinal => ordinal == 0 && ask.Kind == "branch" ? OccupiedLateAsync(ask, read, recovery, made) : Task.FromResult<string?>(null),
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The occupancy read again once the branch went: a tree on it, or a list git could not give, puts it back where nothing stands at its name
    /// (the delete's rule), and the recovery ref it made goes in the same transaction. Null where the branch is clear or never went.
    /// </summary>
    private static async Task<string?> OccupiedLateAsync(RefDeleteAsk ask, DeleteRead read, string recovery, bool recoveryMade)
    {
        var none = CancellationToken.None;
        var (code, _, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", read.Full], none).ConfigureAwait(false);
        if (code == 0) return null;
        var trees = await TreeWorktrees.EntriesAsync(ask.Checkout, none).ConfigureAwait(false);
        string? why = null;
        if (trees is null) why = $"{TreeWorktrees.ListUnread} once it went, so whether a tree checked it out as it went is not known";
        else if (TreeWorktrees.ByBranch(trees).TryGetValue(ask.Name, out var tree)) why = $"it was checked out in `{Path.GetFileName(tree.TrimEnd('/', '\\'))}` as it went";
        if (why is null) return null;

        var back = RefTransaction.RestoreDeleted(read.Full, read.Id);
        if (recoveryMade) back.Delete(recovery, read.Id);
        var run = await back.RunAsync(ask.Checkout, none).ConfigureAwait(false);
        if (run.Done) return $"{why}, so it was put back where it was";
        var (now, at, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", read.Full], none).ConfigureAwait(false);
        return now == 0 && at.Trim() == read.Id
            ? $"{why}, so it was put back where it was"
            : $"{why}, and Daoris could not put it back at {read.Id[..12]} ({ContentHolds.Said(run.Said)}); its recovery ref `{ShortName(recovery)}` keeps the commits";
    }

    /// <summary>What a delete rests on, read fresh: the ref, the commit it stands at, and the refs that hold that commit too.</summary>
    private sealed record DeleteRead(string Full, string Id, IReadOnlyList<(string Ref, string Commit)> Holders);

    private static async Task<(DeleteRead? Read, string? Refusal)> ReadDeleteAsync(string home, RefDeleteAsk ask, string? planned, CancellationToken ct)
    {
        if (FullName(ask.Kind, ask.Name) is not { } full) return (null, $"`{ask.Kind}` is neither a branch nor a tag, so nothing was deleted");
        var noun = ask.Kind == "tag" ? "tag" : "branch";
        if (ask.Name.Length == 0 || ask.Name.StartsWith('-') || ask.Name.Any(char.IsWhiteSpace)) return (null, $"`{ask.Name}` is not a name git takes for a {noun}, so nothing was deleted");
        if (ask.Kind == "branch")
        {
            if (ask.Name == "daoris" || ask.Name.StartsWith("daoris/", StringComparison.Ordinal))
            {
                return (null, $"`{ask.Name}` is under `daoris/`, which is Daoris's own for its sessions' branches, so nothing was deleted");
            }

            if (RecoveryRetention.Made(new LandedBranches(home).Entries(), ask.Repository).Contains(ask.Name))
            {
                return (null, $"`{ask.Name}` is a branch a landing made, which Daoris keeps for its pull request, so nothing was deleted");
            }

            if (string.Equals(ask.Name, ask.Line, StringComparison.Ordinal)) return (null, $"`{ask.Name}` is the repository's line, so nothing was deleted");
            if (TogetherGuard.BranchRefusal(home, ask.Repository, ask.Name) is { } together) return (null, $"`{ask.Name}`: {together}, so nothing was deleted");
        }

        var (code, listed, err) = await WorkingTree.GitAsync(
            ask.Checkout, ["for-each-ref", "--format=%(refname)%09%(objectname)%09%(objecttype)%09%(symref)", "--", full], ct).ConfigureAwait(false);
        if (code != 0) return (null, $"git could not read `{ask.Name}` ({ContentHolds.Said(err)}), so nothing was deleted");
        var row = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(each => each.TrimEnd('\r').Split('\t')).FirstOrDefault(parts => parts.Length >= 3 && parts[0] == full);
        if (row is null) return (null, $"no {noun} named `{ask.Name}` stands, so nothing was deleted");
        if (row.Length > 3 && row[3].Length > 0) return (null, $"`{ask.Name}` is a symbolic ref, so nothing was deleted");
        if (ask.Kind == "tag" && row[2] == "tag") return (null, $"`{ask.Name}` is an annotated or signed tag, which is listed and not deleted here, so nothing was deleted");
        if (row[2] != "commit" || !WorkingTree.IsCommitId(row[1])) return (null, $"`{ask.Name}` names no commit, so nothing was deleted");
        var id = row[1];
        if (ask.At is not null && await CommitAsync(ask.Checkout, ask.At, ct).ConfigureAwait(false) != id) return (null, $"`{ask.Name}` no longer stands at the commit shown, so nothing was deleted");
        if (planned is not null && id != planned) return (null, $"`{ask.Name}` no longer stands at the commit shown, so nothing was deleted");

        if (ask.Kind == "branch")
        {
            if (await TreeWorktrees.EntriesAsync(ask.Checkout, ct).ConfigureAwait(false) is not { } trees)
            {
                return (null, $"{TreeWorktrees.ListUnread}, so whether a tree has `{ask.Name}` checked out is not known and nothing was deleted");
            }

            if (TreeWorktrees.ByBranch(trees).TryGetValue(ask.Name, out var tree))
            {
                return (null, $"`{ask.Name}` is checked out in `{Path.GetFileName(tree.TrimEnd('/', '\\'))}`, so nothing was deleted");
            }
        }

        var (held, holding, heldErr) = await WorkingTree.GitAsync(
            ask.Checkout, ["for-each-ref", "--contains", id, $"--format={ContentHolds.UnaliasedFormat}", "refs/heads/", "refs/tags/", "refs/remotes/"], ct).ConfigureAwait(false);
        if (held != 0) return (null, $"git could not say which refs hold `{ask.Name}`'s commit ({ContentHolds.Said(heldErr)}), so nothing was deleted");
        var holders = ContentHolds.Unaliased(holding)
            .Where(each => each.Ref != full && (ContentHolds.PersonsRef(each.Ref) || each.Ref.StartsWith("refs/tags/", StringComparison.Ordinal)) && WorkingTree.IsCommitId(each.Commit))
            .Take(HoldersVerified).ToList();
        return (new DeleteRead(full, id, holders), null);
    }

    /// <summary>The recovery ref's name under <see cref="ContentHolds.DiscardedPrefix"/>: a free one to make, or one that already stands at the commit (verified, not made).</summary>
    private static async Task<(string? Ref, bool Made, string? Refusal)> RecoveryNameAsync(string checkout, string name, string id, CancellationToken ct)
    {
        var (named, why) = await RecoveryKeeps.NameAsync(checkout, name, id, ct).ConfigureAwait(false);
        return named is null ? (null, false, $"{why}, so nothing was deleted") : (named.Ref, named.Made, null);
    }

    /// <summary>The branch's section of the local configuration as the plan keeps it (file, scope, section, text), or null where it has none or the file does not read.</summary>
    internal static async Task<JsonObject?> SectionAsync(string checkout, string name)
    {
        var file = Path.Combine(TreeLock.CommonDirOf(checkout), "config");
        var section = $"branch.{name}";
        try
        {
            return await ActConfig.SectionAsync(file, section).ConfigureAwait(false) is { } text
                ? new JsonObject { ["file"] = file, ["scope"] = "local", ["section"] = section, ["text"] = text }
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Undo the delete <paramref name="record"/> made: the ref made again at its id only where the name is free, the recovery ref it made let go
    /// where it still stands at that id, the branch's section put back under <c>config.lock</c> where none stands.
    /// </summary>
    private static async Task<ActRun> UndoDeleteAsync(ActRuns runs, ActRecord record, string checkout, string press, string? confirmed, CancellationToken ct)
    {
        var plan = JsonNode.Parse(record.Plan)!;
        // A delete Daoris judged (JournaledDeletes) names the recovery ref its door's rule kept, made by the act only where `recoveryMade` says so.
        var (full, id, recovery) = (plan["ref"]!.GetValue<string>(), plan["at"]!.GetValue<string>(), (string?)plan["recovery"]);
        var name = ShortName(full);
        var noun = full.StartsWith("refs/tags/", StringComparison.Ordinal) ? "tag" : "branch";
        var section = plan["section"] as JsonObject;

        var transaction = RefTransaction.RestoreDeleted(full, id);
        var moves = new List<(string Ref, string? Before, string? After)> { (full, null, id) };
        var (code, at, _) = recovery is null ? (1, "", "") : await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", recovery], ct).ConfigureAwait(false);
        if (recovery is not null && plan["recoveryMade"]?.GetValue<bool>() == true && code == 0 && at.Trim() == id)
        {
            transaction.Delete(recovery, id);
            moves.Add((recovery, id, null));
        }

        var steps = new List<ActStepPlan>();
        try
        {
            steps.Add(ActSteps.Ref(transaction, moves));
        }
        catch (ArgumentException error)
        {
            return Refused($"{error.Message}, so nothing was undone");
        }

        if (section is not null)
        {
            steps.Add(ActSteps.Restore(section["file"]!.GetValue<string>(), section["scope"]!.GetValue<string>(), section["section"]!.GetValue<string>(), section["text"]!.GetValue<string>()));
        }

        var shown = new JsonObject { ["undo"] = record.Id, ["ref"] = full, ["at"] = id };
        return await runs.RunAsync(new ActPlan(UndoKind, press, record.Repository, checkout, steps)
        {
            Plan = shown.ToJsonString(), Confirmed = confirmed,
            Judge = async token =>
            {
                var (stands, _, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", full], token).ConfigureAwait(false);
                if (stands == 0) return $"a {noun} named `{name}` already exists, so act `{record.Id}` was not undone";
                var (known, _, _) = await WorkingTree.GitAsync(checkout, ["cat-file", "-e", id + "^{commit}"], token).ConfigureAwait(false);
                if (known != 0) return $"the commit act `{record.Id}` deleted `{name}` at is no longer in the repository, so it was not undone";
                if (section is not null && await SectionAsync(checkout, name).ConfigureAwait(false) is not null)
                {
                    return $"a `branch.{name}` section stands already, so act `{record.Id}` was not undone";
                }

                return null;
            },
        }, ct).ConfigureAwait(false);
    }

    // ——— Rename.

    /// <summary>The commands a person types in the checkout for the same act.</summary>
    public static IReadOnlyList<string> Commands(RefRenameAsk ask) =>
        ask.Kind == "tag" ? [$"git tag {ask.To} {ask.Name}", $"git tag -d {ask.Name}"] : [$"git branch -m {ask.Name} {ask.To}"];

    private static RefDeleteAsk Source(RefRenameAsk ask) => new(ask.Repository, ask.Checkout, ask.Kind, ask.Name, ask.At, ask.Line);

    /// <summary>The delete's and the create's refusals say what they would have left undone; here that is the rename.</summary>
    private static string? Renamed(string? refusal) => refusal?.Replace("so nothing was deleted", "so nothing was renamed").Replace("so nothing was created", "so nothing was renamed");

    /// <summary>What a rename rests on, read fresh: the source as a delete reads it (held branches, annotated tags, occupancy, the commit shown) and a target that is free.</summary>
    private static async Task<(DeleteRead? Read, string? Refusal)> ReadRenameAsync(string home, RefRenameAsk ask, string? planned, CancellationToken ct)
    {
        var (read, refusal) = await ReadDeleteAsync(home, Source(ask), planned, ct).ConfigureAwait(false);
        if (read is null) return (null, Renamed(refusal));
        if (await JudgeAsync(new RefAsk(ask.Repository, ask.Checkout, ask.Kind, ask.To, read.Id), read.Id, ct).ConfigureAwait(false) is { } target) return (null, Renamed(target));
        return ask.Kind == "branch" && await SectionAsync(ask.Checkout, ask.To).ConfigureAwait(false) is not null
            ? (null, $"a `branch.{ask.To}` section stands already, so nothing was renamed")
            : (read, null);
    }

    /// <summary>What the rename would do now, read without the lock and written nowhere: the whole commit id it moves, or why it is refused.</summary>
    public static async Task<(string? Id, string? Refusal)> PlanRenameAsync(string home, RefRenameAsk ask, CancellationToken ct)
    {
        var (read, refusal) = await ReadRenameAsync(home, ask, null, ct).ConfigureAwait(false);
        return (read?.Id, refusal);
    }

    /// <summary>
    /// Rename the ref: refused before anything is written where it is a held branch, an annotated tag, occupied, not at the commit shown, or
    /// the new name is taken; else one transaction (the old name deleted and the new one made at the tip, the old tip kept on a recovery ref),
    /// then the branch's section renamed under <c>config.lock</c>. A branch a tree checked out meanwhile is moved back, the recovery ref with it.
    /// </summary>
    public static async Task<ActRun> RenameAsync(ActRuns runs, RefRenameAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        var home = runs.Journal.Home;
        var (read, refusal) = await ReadRenameAsync(home, ask, null, ct).ConfigureAwait(false);
        if (read is null) return Refused(refusal!);

        var (recovery, made, taken) = await RecoveryNameAsync(ask.Checkout, ask.Name, read.Id, ct).ConfigureAwait(false);
        if (recovery is null) return Refused(Renamed(taken)!);

        var target = FullName(ask.Kind, ask.To)!;
        var transaction = RefTransaction.Rename(read.Full, target, read.Id);
        var moves = new List<(string Ref, string? Before, string? After)> { (read.Full, read.Id, null), (target, null, read.Id) };
        if (made)
        {
            transaction.Create(recovery, read.Id);
            moves.Add((recovery, null, read.Id));
        }
        else
        {
            transaction.Verify(recovery, read.Id);
        }

        var steps = new List<ActStepPlan>();
        try
        {
            steps.Add(ActSteps.Ref(transaction, moves));
        }
        catch (ArgumentException error)
        {
            return Refused($"{error.Message}, so nothing was renamed");
        }

        JsonObject? section = null;
        if (ask.Kind == "branch" && await SectionAsync(ask.Checkout, ask.Name).ConfigureAwait(false) is { } kept)
        {
            section = kept;
            steps.Add(ActSteps.Config(kept["file"]!.GetValue<string>(), "local", kept["section"]!.GetValue<string>(), $"branch.{ask.To}", kept["text"]!.GetValue<string>()));
        }

        var plan = new JsonObject
        {
            ["kind"] = ask.Kind, ["name"] = ask.Name, ["to"] = ask.To, ["ref"] = read.Full, ["toRef"] = target, ["at"] = read.Id,
            ["recovery"] = recovery, ["recoveryMade"] = made, ["section"] = section,
            ["commands"] = new JsonArray([.. Commands(ask).Select(each => (JsonNode)each)]),
        };
        return await runs.RunAsync(new ActPlan(RenameKind, press, ask.Repository, ask.Checkout, steps)
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed,
            Judge = async token => (await ReadRenameAsync(home, ask, read.Id, token).ConfigureAwait(false)).Refusal,
            After = ordinal => ordinal == 0 && ask.Kind == "branch" ? OccupiedLateRenamedAsync(ask, read, target, recovery, made) : Task.FromResult<string?>(null),
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The occupancy read again once the branch moved: a tree on the old name, or a list git could not give, moves it back by the rename's
    /// reversal, which checks both names, and the recovery ref it made goes in the same transaction. Null where the branch is clear or never moved.
    /// </summary>
    private static async Task<string?> OccupiedLateRenamedAsync(RefRenameAsk ask, DeleteRead read, string target, string recovery, bool recoveryMade)
    {
        var none = CancellationToken.None;
        var (code, at, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", target], none).ConfigureAwait(false);
        if (code != 0 || at.Trim() != read.Id) return null;
        var trees = await TreeWorktrees.EntriesAsync(ask.Checkout, none).ConfigureAwait(false);
        string? why = null;
        if (trees is null) why = $"{TreeWorktrees.ListUnread} once it moved, so whether a tree checked it out as it moved is not known";
        else if (TreeWorktrees.ByBranch(trees).TryGetValue(ask.Name, out var tree)) why = $"it was checked out in `{Path.GetFileName(tree.TrimEnd('/', '\\'))}` as it moved";
        if (why is null) return null;

        var back = RefTransaction.ReverseRename(read.Full, target, read.Id);
        if (recoveryMade) back.Delete(recovery, read.Id);
        var run = await back.RunAsync(ask.Checkout, none).ConfigureAwait(false);
        return run.Done
            ? $"{why}, so it was moved back to `{ask.Name}`"
            : $"{why}, and Daoris could not move it back to `{ask.Name}` ({ContentHolds.Said(run.Said)}); its recovery ref `{ShortName(recovery)}` keeps the commits";
    }

    /// <summary>
    /// Finish a rename stopped between its ref and its section steps: the section moved alone, only where the new name stands at the journaled
    /// commit, the old is gone, and the section still reads as journaled with none at the new name. Refused otherwise, nothing written.
    /// </summary>
    public static async Task<ActRun> FinishAsync(ActRuns runs, string repository, string checkout, string act, string press, string? confirmed, CancellationToken ct)
    {
        var journal = runs.Journal;
        if (journal.Act(act) is not { } record) return Refused($"no act `{act}` is recorded, so nothing was finished");
        if (record.Kind != RenameKind) return Refused($"act `{act}` is not a rename, so nothing was finished");
        if (!string.Equals(record.Repository, repository, StringComparison.OrdinalIgnoreCase)) return Refused($"act `{act}` was made in another repository, so nothing was finished");
        if (record.Open || record.Outcome != ActOutcomes.Partial) return Refused($"act `{act}` did not stop between its ref and its section ({record.Outcome ?? "still open"}), so there is nothing to finish");
        var plan = JsonNode.Parse(record.Plan)!.AsObject();
        if (plan["section"] is not JsonObject section) return Refused($"act `{act}` moved no section, so there is nothing to finish");
        var (full, target, id) = (plan["ref"]!.GetValue<string>(), plan["toRef"]!.GetValue<string>(), plan["at"]!.GetValue<string>());
        var (name, to) = (ShortName(full), ShortName(target));
        var text = section["text"]!.GetValue<string>();

        var step = ActSteps.Config(section["file"]!.GetValue<string>(), "local", section["section"]!.GetValue<string>(), $"branch.{to}", text);
        plan["finishes"] = act;
        return await runs.RunAsync(new ActPlan(FinishKind, press, repository, checkout, [step])
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed,
            Judge = async token =>
            {
                if (await RenamedStandsAsync(checkout, full, target, id, token).ConfigureAwait(false) is { } moved) return $"{moved}, so act `{act}` was not finished";
                if (await SectionAsync(checkout, name).ConfigureAwait(false) is not { } old || old["text"]!.GetValue<string>() != text)
                {
                    return $"the `branch.{name}` section is gone or changed since act `{act}` read it, so it was not moved";
                }

                return await SectionAsync(checkout, to).ConfigureAwait(false) is not null ? $"a `branch.{to}` section stands already, so act `{act}` was not finished" : null;
            },
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Why a rename's ref step does not stand as journaled (the new name at the commit, the old one absent), else null; reads both names.</summary>
    private static async Task<string?> RenamedStandsAsync(string checkout, string full, string target, string id, CancellationToken ct)
    {
        var (code, at, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", target], ct).ConfigureAwait(false);
        if (code != 0 || at.Trim() != id) return $"`{ShortName(target)}` no longer stands at the commit the rename moved it to ({(code == 0 ? "it was moved" : "it is gone")})";
        var (old, _, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", full], ct).ConfigureAwait(false);
        return old == 0 ? $"`{ShortName(full)}` stands again" : null;
    }

    /// <summary>
    /// Undo the rename (or its Finish) <paramref name="record"/> made: the new name deleted and the old made at the tip in one transaction that
    /// checks both, the recovery ref it made let go, and the section moved back where it had moved. A stopped rename moves only its ref.
    /// </summary>
    private static async Task<ActRun> UndoRenameAsync(ActRuns runs, ActRecord record, string checkout, string press, string? confirmed, CancellationToken ct)
    {
        var plan = JsonNode.Parse(record.Plan)!;
        var (full, target, id, recovery) = (plan["ref"]!.GetValue<string>(), plan["toRef"]!.GetValue<string>(), plan["at"]!.GetValue<string>(), plan["recovery"]!.GetValue<string>());
        var (name, to) = (ShortName(full), ShortName(target));
        var section = plan["section"] as JsonObject;
        var text = section?["text"]!.GetValue<string>();
        var moved = section is not null
            && (record.Kind == FinishKind || runs.Journal.Steps(record.Id).Any(step => step.Kind == ActStepKinds.Config && step.Outcome == ActOutcomes.Done));
        var renamedText = moved ? ActConfig.Renamed(text!, $"branch.{to}") : null;

        var transaction = RefTransaction.ReverseRename(full, target, id);
        var moves = new List<(string Ref, string? Before, string? After)> { (target, id, null), (full, null, id) };
        var (code, at, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", recovery], ct).ConfigureAwait(false);
        if (plan["recoveryMade"]?.GetValue<bool>() == true && code == 0 && at.Trim() == id)
        {
            transaction.Delete(recovery, id);
            moves.Add((recovery, id, null));
        }

        var steps = new List<ActStepPlan>();
        try
        {
            steps.Add(ActSteps.Ref(transaction, moves));
        }
        catch (ArgumentException error)
        {
            return Refused($"{error.Message}, so nothing was undone");
        }

        if (moved) steps.Add(ActSteps.Config(section!["file"]!.GetValue<string>(), "local", $"branch.{to}", $"branch.{name}", renamedText!));
        var shown = new JsonObject { ["undo"] = record.Id, ["ref"] = full, ["toRef"] = target, ["at"] = id };
        return await runs.RunAsync(new ActPlan(UndoKind, press, record.Repository, checkout, steps)
        {
            Plan = shown.ToJsonString(), Confirmed = confirmed,
            Judge = async token =>
            {
                if (await RenamedStandsAsync(checkout, full, target, id, token).ConfigureAwait(false) is { } gone) return $"{gone}, so act `{record.Id}` was not undone";
                if (section is null) return null;
                var (newer, older) = (await SectionAsync(checkout, to).ConfigureAwait(false), await SectionAsync(checkout, name).ConfigureAwait(false));
                if (moved)
                {
                    return newer?["text"]!.GetValue<string>() == renamedText && older is null
                        ? null
                        : $"the `branch.{to}` section changed, or a `branch.{name}` section stands, since act `{record.Id}`, so it was not undone";
                }

                return newer is null && older?["text"]!.GetValue<string>() == text
                    ? null
                    : $"the `branch.{name}` section is not as act `{record.Id}` left it (it was moved or changed since; undo that act instead), so it was not undone";
            },
        }, ct).ConfigureAwait(false);
    }

    private static ActRun Refused(string said) => new(null, null, said, true);

    /// <summary>The ref's short name for a sentence.</summary>
    public static string ShortName(string reference) =>
        reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..]
        : reference.StartsWith("refs/tags/", StringComparison.Ordinal) ? reference["refs/tags/".Length..] : reference;

    /// <summary>The whole commit id <paramref name="name"/> names in the checkout, or null where it names no commit.</summary>
    private static async Task<string?> CommitAsync(string checkout, string name, CancellationToken ct)
    {
        if (name.StartsWith('-')) return null;
        var (code, said, _) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], ct).ConfigureAwait(false);
        return code == 0 && WorkingTree.IsCommitId(said.Trim()) ? said.Trim() : null;
    }

    /// <summary>
    /// The fresh reads: why the ref is not to be created, or null. With <paramref name="planned"/> (under the lock) the commit must still be
    /// the one the plan showed.
    /// </summary>
    private static async Task<string?> JudgeAsync(RefAsk ask, string? planned, CancellationToken ct)
    {
        if (FullName(ask.Kind, ask.Name) is not { } full) return $"`{ask.Kind}` is neither a branch nor a tag, so nothing was created";
        var noun = ask.Kind == "tag" ? "tag" : "branch";
        if (ask.Name.Length == 0 || ask.Name.StartsWith('-') || ask.Name.Any(char.IsWhiteSpace)) return $"`{ask.Name}` is not a name git takes for a {noun}, so nothing was created";
        if (ask.Kind == "branch" && (ask.Name == "daoris" || ask.Name.StartsWith("daoris/", StringComparison.Ordinal)))
        {
            return $"`{ask.Name}` is under `daoris/`, which is Daoris's own for its sessions' branches, so nothing was created";
        }

        var (valid, _, _) = await WorkingTree.GitAsync(ask.Checkout, ["check-ref-format", full], ct).ConfigureAwait(false);
        if (valid != 0) return $"`{ask.Name}` is not a name git takes for a {noun}, so nothing was created";

        var (read, standing, readErr) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", full], ct).ConfigureAwait(false);
        if (read == 0 && standing.Trim().Length > 0) return $"a {noun} named `{ask.Name}` already exists, so nothing was created";
        if (read != 0 && read != 1) return $"git could not read whether `{ask.Name}` exists ({ContentHolds.Said(readErr)}), so nothing was created";
        var segments = ask.Name.Split('/');
        for (var depth = 1; depth < segments.Length; depth++)
        {
            var parent = full[..(full.Length - ask.Name.Length)] + string.Join('/', segments.Take(depth));
            var (above, _, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", parent], ct).ConfigureAwait(false);
            if (above == 0) return $"`{ShortName(parent)}` exists as a {noun}, so `{ask.Name}` cannot be made under it";
        }

        var (below, listed, _) = await WorkingTree.GitAsync(ask.Checkout, ["for-each-ref", "--count=1", "--format=%(refname)", "--", full + "/"], ct).ConfigureAwait(false);
        if (below == 0 && listed.Trim().Length > 0) return $"`{ShortName(listed.Trim())}` exists, so `{ask.Name}` cannot be made as a {noun} above it";

        var id = await CommitAsync(ask.Checkout, ask.At ?? "HEAD", ct).ConfigureAwait(false);
        if (id is null) return $"`{ask.At ?? "HEAD"}` names no commit, so nothing was created";
        return planned is not null && id != planned ? $"`{ask.At ?? "HEAD"}` no longer names the commit shown, so nothing was created" : null;
    }
}
