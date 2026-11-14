using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a delete Daoris judged came to: its act, whether the branch went, and the clause that keeps it where it did not.</summary>
/// <param name="Act">The journal's record, which <c>GIT_REF_UNDO</c> undoes; null where the act is refused before anything is written.</param>
/// <param name="Kept">Why the branch stays, in a clause; null where it went.</param>
/// <param name="Said">git's or the act's own words where its ref step did not finish, which the caller reads for why.</param>
internal sealed record JournaledDelete(string? Act, string? Kept, string? Said);

/// <summary>
/// **Every branch delete through the journal**: a delete Daoris's own door judged (a session
/// branch discarded, a landing withdrawn, a sweep, the look's clean-up, a housekeeping discard) runs as the same <c>ref.delete</c> act the
/// person's <c>GIT_REF_DELETE</c> runs, so it is journaled, reconciled by its postconditions and undone by <c>GIT_REF_UNDO</c>. The door keeps
/// its judgement; this keeps the performer: one transaction verifying what the proof rested on and deleting the branch at the commit judged,
/// then the branch's own section under <c>config.lock</c>. Where the door's rule keeps the commits (<see cref="RecoveryKeeps"/>), the recovery ref
/// is made in that same transaction, so a refused delete makes none; the record names it, and the Undo takes back one the act made. The Undo
/// makes the branch again at its commit wherever git still holds it.
/// </summary>
internal static class JournaledDeletes
{
    /// <summary>Who pressed a delete Daoris judged, as the journal records it beside the person's <c>window</c> and <c>terminal</c>.</summary>
    public const string Press = "daoris";

    /// <summary>How long a judged delete waits behind a start or a writer, whose holds are short, before it is kept and said.</summary>
    internal static TimeSpan Waits => WaitSeam.Value ?? TimeSpan.FromSeconds(30);

    internal static readonly AsyncLocal<TimeSpan?> WaitSeam = new();

    /// <summary>
    /// Delete <paramref name="branch"/> at <paramref name="judged"/>, verifying each of <paramref name="held"/> at its commit in the same
    /// transaction, and keeping its commits on <paramref name="keep"/> where given. <paramref name="late"/> is read once the ref went, with the
    /// recovery ref the act made: words that put both back and stop the act there, or null to go on. Run under no cancellation, as a judged
    /// delete always is, and under the checkout's hold where this flow holds it already.
    /// </summary>
    public static async Task<JournaledDelete> DeleteAsync(
        string home, string root, string repository, string branch, string judged, IReadOnlyList<(string Ref, string Commit)> held,
        Func<string?, Task<string?>> late, RecoveryName? keep = null)
    {
        var none = CancellationToken.None;
        var full = $"refs/heads/{branch}";
        var steps = new List<ActStepPlan>();
        var transaction = new RefTransaction().Verify(held).Delete(full, judged);
        var moves = new List<(string Ref, string? Before, string? After)> { (full, judged, null) };
        if (keep is { Made: true })
        {
            transaction.Create(keep.Ref, judged);
            moves.Add((keep.Ref, null, judged));
        }
        else if (keep is not null)
        {
            transaction.Verify(keep.Ref, judged);
        }

        try
        {
            steps.Add(ActSteps.Ref(transaction, moves));
        }
        catch (ArgumentException error)
        {
            return new(null, $"{error.Message}, so it was not deleted", null);
        }

        var section = await GitRefActs.SectionAsync(root, branch).ConfigureAwait(false);
        if (section is not null)
        {
            steps.Add(ActSteps.Config(section["file"]!.GetValue<string>(), "local", section["section"]!.GetValue<string>(), null, section["text"]!.GetValue<string>()));
        }

        var plan = new JsonObject
        {
            ["kind"] = "branch", ["name"] = branch, ["ref"] = full, ["at"] = judged, ["recovery"] = keep?.Ref, ["recoveryMade"] = keep?.Made == true,
            ["holders"] = new JsonArray([.. held.Select(each => (JsonNode)each.Ref)]), ["section"] = section?.DeepClone(),
            ["commands"] = new JsonArray((JsonNode)$"git branch -D {branch}"),
        };
        string? stopped = null;
        var runs = new ActRuns(new ActJournal(home));
        var run = await runs.RunAsync(new ActPlan(GitRefActs.DeleteKind, Press, repository, root, steps)
        {
            Waits = Waits,
            Plan = plan.ToJsonString(),
            After = async ordinal => ordinal == 0 ? stopped = await late(keep is { Made: true } ? keep.Ref : null).ConfigureAwait(false) : null,
        }, none).ConfigureAwait(false);

        if (run.Refused) return new(null, Clause(run.Said), null);
        if (stopped is not null) return new(run.Act, stopped, null);
        if (run.Outcome is ActOutcomes.Done) return new(run.Act, null, null);
        var refStep = runs.Journal.Steps(run.Act!).FirstOrDefault(each => each.Kind == ActStepKinds.Ref);
        var said = refStep?.Said ?? run.Said;
        // A transaction git ended refusing, its branch standing neither at its judged commit nor gone: the caller reads on git's own word what
        // stands, the branch moved since or unread.
        return run.Outcome == ActOutcomes.NotDone || refStep is { ExitCode: not (null or 0), Outcome: ActOutcomes.Unknown }
            ? new(run.Act, null, said)
            : new(run.Act, $"Daoris could not tell whether git deleted it, and act {run.Act} keeps what it knows", null);
    }

    /// <summary>
    /// A delete its door forces, with no proof (the person's force, a tree already gone): the branch at the commit it stands at now, verified at
    /// it, through the same judged delete, so it is journaled with its Undo. Null where it went; otherwise why it stays, in a clause.
    /// </summary>
    public static async Task<string?> ForcedAsync(ContentHolds content, string root, string repository, string branch, CancellationToken ct)
    {
        var full = $"refs/heads/{branch}";
        var (stands, at, err) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", full], ct).ConfigureAwait(false);
        if (stands != 0 || !WorkingTree.IsCommitId(at.Trim())) return $"git would not delete it: {(stands == 1 ? "no such branch stands" : ContentHolds.Said(err))}";
        var tip = at.Trim();
        return await content.DeleteJudgedAsync(root, repository, branch, tip, [(full, tip)], ct).ConfigureAwait(false);
    }

    private static string Clause(string said)
    {
        var clause = said.TrimEnd('.', ' ');
        return clause.Length == 0 ? clause : char.ToLowerInvariant(clause[0]) + clause[1..];
    }
}
