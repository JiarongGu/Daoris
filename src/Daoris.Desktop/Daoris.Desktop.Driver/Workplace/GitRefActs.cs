using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A branch or a lightweight tag to create (GITAPP2c): <paramref name="Kind"/> is <c>branch</c> or <c>tag</c>, <paramref name="At"/> the commit it names (HEAD where null).</summary>
public sealed record RefAsk(string Repository, string Checkout, string Kind, string Name, string? At);

/// <summary>
/// **Create a branch or a lightweight tag, and undo it** (GITAPP2c, D204; the git-writes design §6, §12 row c). One <c>create</c> through
/// <see cref="RefTransaction"/> run as an act of the journal (<see cref="ActRuns"/>), so it holds the checkout alone, is journaled, and is
/// reconciled by its postconditions where its run died. The undo is an act of its own: a <c>delete</c> at the id the create made, refused
/// where the ref stands anywhere else. A ref act writes no working tree, so it needs no checkout switch.
/// </summary>
public static class GitRefActs
{
    public const string CreateKind = "ref.create";

    public const string UndoKind = "ref.undo";

    /// <summary>The commands a person types in the checkout for the same act.</summary>
    public static IReadOnlyList<string> Commands(RefAsk ask) =>
        [ask.Kind == "tag" ? $"git tag {ask.Name} {ask.At ?? "HEAD"}" : $"git branch {ask.Name} {ask.At ?? "HEAD"}"];

    /// <summary>The ref's full name, or null where the kind is neither.</summary>
    public static string? FullName(string kind, string name) => kind switch { "branch" => "refs/heads/" + name, "tag" => "refs/tags/" + name, _ => null };

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
        if (record.Kind != CreateKind) return Refused($"act `{act}` is not a create, so nothing was undone");
        if (!string.Equals(record.Repository, repository, StringComparison.OrdinalIgnoreCase)) return Refused($"act `{act}` was made in another repository, so nothing was undone");
        if (record.Open || record.Outcome != ActOutcomes.Done) return Refused($"act `{act}` did not finish as done ({record.Outcome ?? "still open"}), so there is nothing to undo");
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
