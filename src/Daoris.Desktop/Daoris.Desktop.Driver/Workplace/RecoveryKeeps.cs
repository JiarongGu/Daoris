using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A recovery ref's name for one keep: free, which the act makes, or standing at the commit already, which the act verifies.</summary>
internal sealed record RecoveryName(string Ref, bool Made);

/// <summary>
/// **A recovery ref kept through the journal's act**: every ref under
/// <see cref="ContentHolds.DiscardedPrefix"/> a door of Daoris's keeps is made or verified in an act the journal records, so a record names it
/// and <see cref="RecoveryRetention"/> dates it from that act. A door that deletes the branch makes it in the delete's own transaction
/// (<see cref="JournaledDeletes"/>), which its Undo takes back; a door that keeps without deleting (a settled member's work, a tidied part, a
/// detached tree's commit, a missing landing's) runs <see cref="KeepAsync"/>, one <see cref="Kind"/> act with no Undo.
/// </summary>
internal static class RecoveryKeeps
{
    /// <summary>The act that keeps a recovery ref and deletes nothing.</summary>
    public const string Kind = "ref.keep";

    /// <summary>
    /// The recovery ref for <paramref name="branch"/> at <paramref name="commit"/>, named without making it: the first of its names (the branch's,
    /// then a number after it) that does not resolve, or one already standing at that commit. A name git cannot make is the act's refusal, said
    /// there; null with why where every name is taken.
    /// </summary>
    public static async Task<(RecoveryName? Name, string? Why)> NameAsync(string root, string branch, string commit, CancellationToken ct)
    {
        for (var n = 1; n <= ContentHolds.DiscardedNames; n++)
        {
            var reference = ContentHolds.DiscardedPrefix + branch + (n == 1 ? "" : $"-{n}");
            var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (code != 0) return (new(reference, true), null);
            if (at.Trim() == commit) return (new(reference, false), null);
        }

        return (null, "every name for its recovery ref is taken");
    }

    /// <summary>
    /// Keep <paramref name="commit"/> on a recovery ref of <paramref name="branch"/>'s name, as one act pressed by <c>daoris</c>, under no
    /// cancellation: made where a name is free, verified where one stands at it already, so a record names it either way.
    /// </summary>
    /// <param name="absent">A ref git must say is absent in the same transaction (a dropped landing's own branch), or null.</param>
    /// <returns>The ref, or null with why in a clause; then nothing that only it would keep may go.</returns>
    public static async Task<(string? Ref, string? Why)> KeepAsync(string home, string root, string repository, string branch, string commit, string? absent = null)
    {
        var none = CancellationToken.None;
        var (name, why) = await NameAsync(root, branch, commit, none).ConfigureAwait(false);
        if (name is null) return (null, why);
        var transaction = name.Made ? new RefTransaction().Create(name.Ref, commit) : new RefTransaction().Verify(name.Ref, commit);
        var moves = new List<(string Ref, string? Before, string? After)> { (name.Ref, name.Made ? null : commit, commit) };
        if (absent is not null)
        {
            transaction.Verify(absent, null);
            moves.Add((absent, null, null));
        }

        ActStepPlan step;
        try
        {
            step = ActSteps.Ref(transaction, moves);
        }
        catch (ArgumentException error)
        {
            return (null, error.Message);
        }

        var plan = new JsonObject
        {
            ["kind"] = "recovery", ["name"] = branch, ["ref"] = name.Ref, ["at"] = commit, ["recovery"] = name.Ref, ["recoveryMade"] = name.Made, ["absent"] = absent,
            ["commands"] = new JsonArray((JsonNode)$"git update-ref {name.Ref} {commit} \"\""),
        };
        var runs = new ActRuns(new ActJournal(home));
        var run = await runs.RunAsync(new ActPlan(Kind, JournaledDeletes.Press, repository, root, [step]) { Plan = plan.ToJsonString() }, none).ConfigureAwait(false);
        if (run.Outcome == ActOutcomes.Done) return (name.Ref, null);
        return (null, run.Refused || run.Act is null ? run.Said : runs.Journal.Steps(run.Act).FirstOrDefault()?.Said ?? run.Said);
    }

    /// <summary>
    /// Each recovery ref an act of <paramref name="repository"/> that did its work made or verified, with the newest such act's moment, which
    /// <see cref="RecoveryRetention"/> dates it from: the ref its plan names, and each ref under the prefix its ref steps leave standing.
    /// </summary>
    public static IReadOnlyDictionary<string, DateTimeOffset> Recorded(string home, string repository)
    {
        var dated = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var act in new ActJournal(home).Naming(repository, ContentHolds.DiscardedPrefix))
        {
            foreach (var named in Named(act))
            {
                if (!dated.TryGetValue(named, out var seen) || act.At > seen) dated[named] = act.At;
            }
        }

        return dated;
    }

    private static IEnumerable<string> Named(ActRecord act)
    {
        var named = new List<string>();
        try
        {
            if (JsonNode.Parse(act.Plan)?["recovery"] is JsonValue plan && plan.TryGetValue<string>(out var kept)) named.Add(kept);
            var steps = act.Recovery is null ? null : JsonNode.Parse(act.Recovery)?["steps"] as JsonArray;
            foreach (var refs in (steps ?? []).Select(step => step?["as"]?["refs"]).OfType<JsonArray>())
            {
                named.AddRange(refs.Where(move => move?["after"] is not null).Select(move => (string?)move!["ref"]).OfType<string>());
            }
        }
        catch (JsonException)
        {
            // A record this build cannot read names nothing, and its ref is counted as no record's.
        }

        return named.Where(each => each.StartsWith(ContentHolds.DiscardedPrefix, StringComparison.Ordinal));
    }
}
