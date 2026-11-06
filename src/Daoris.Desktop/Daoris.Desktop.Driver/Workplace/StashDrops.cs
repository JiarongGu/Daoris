using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A stash Drop as the page pressed it: the stash's commit id, never its place on the list, which any other tree's Save moves.</summary>
public sealed record StashDropPress(string Id);

/// <summary>
/// **Stash Drop** (GITAPP2, D204, the git-writes design §8): the one act that removes an entry of the shared stash, and only one that a Save of
/// Daoris's own made, which its journal says by the commit saved; a message proves no ownership, so the stash of a person's terminal or a
/// session's tree is never dropped here. One <c>stash</c> step by the commit's id, the place found as it runs and the commit git names read
/// back (<see cref="StashDropSteps"/>). The commit it drops stays in the act's record and in its answer with the command that stores it again;
/// the object lasts until git's own clean-up, not longer, so the answer says that too.
/// </summary>
public sealed partial class StashDrops(ActJournal journal)
{
    internal ActRuns Runs { get; } = new(journal);

    /// <summary>The commits a Save of Daoris's, done in this repository's checkout, saved, read from the journal alone.</summary>
    internal static HashSet<string> SavedHere(ActJournal journal, string repository, string checkout)
    {
        var common = TreeLock.CommonDirOf(checkout);
        var saved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in journal.OfKind(StashVerbs.Save, repository, 500))
        {
            if (record.Outcome != ActOutcomes.Done || TreeLock.CommonDirOf(record.Checkout) != common) continue;
            foreach (var step in journal.Steps(record.Id).Where(each => each.Kind == ActStepKinds.Stash))
            {
                if (Saved().Match(step.Said ?? "") is { Success: true } made) saved.Add(made.Groups["id"].Value);
            }
        }

        return saved;
    }

    public async Task<ActRun> RunAsync(StageContext context, StashDropPress press, CancellationToken ct)
    {
        if (await StageActs.GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var (stale, message) = await JudgeAsync(context, press, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        var id = press.Id;

        var plan = new JsonObject
        {
            ["verb"] = StashVerbs.Drop, ["id"] = id, ["message"] = message, ["switch"] = true,
            ["shared"] = "refs/stash is shared with every linked tree, sessions' included; a Drop removes this one entry by its commit id",
            ["undo"] = $"git stash store -m \"{message.Replace("\"", "'", StringComparison.Ordinal)}\" {id}",
        };
        var act = new ActPlan(StashVerbs.Drop, "window", context.Repository, context.Checkout, [StashDropSteps.Step(id, message)])
        {
            Plan = plan.ToJsonString(),
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                return (await JudgeAsync(context, press, token).ConfigureAwait(false)).Refusal;
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null) return run;
        var short12 = id[..Math.Min(12, id.Length)];
        if (run.Outcome == ActOutcomes.Done)
        {
            var said = $"The stash `{short12}` is dropped; `git stash store -m \"{message.Replace("\"", "'", StringComparison.Ordinal)}\" {id}` puts it back until git clears unused objects.";
            return run with { Notes = said, Said = $"{run.Said} {said}" };
        }

        return journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Stash)?.Said is { Length: > 0 } why
            ? run with { Notes = char.ToUpperInvariant(why[0]) + why[1..] + ".", Said = $"{run.Said} {char.ToUpperInvariant(why[0])}{why[1..]}." }
            : run;
    }

    /// <summary>Why a Drop is refused now (null where it is not) and the message of the stash, read fresh.</summary>
    private async Task<(string? Refusal, string Message)> JudgeAsync(StageContext context, StashDropPress press, CancellationToken ct)
    {
        if (!WorkingTree.IsCommitId(press.Id)) return (Said("the stash is named by its commit id, and this is not one"), "");
        if (await StashActs.ListAsync(context.Checkout, ct).ConfigureAwait(false) is not { } listed) return (Said("git could not read the stash list"), "");
        if (!listed.Any(each => each.Id == press.Id)) return (Said("that commit is not in the stash list now; it may have been dropped"), "");
        if (!SavedHere(journal, context.Repository, context.Checkout).Contains(press.Id))
        {
            return (Said("Daoris did not save that stash, so it is not dropped here; a stash of a terminal or a session is dropped where it was made"), "");
        }

        return (null, listed.First(each => each.Id == press.Id).Message);
    }

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);

    [GeneratedRegex(@"saved the stash (?<id>[0-9a-f]{40,64})")]
    private static partial Regex Saved();
}
