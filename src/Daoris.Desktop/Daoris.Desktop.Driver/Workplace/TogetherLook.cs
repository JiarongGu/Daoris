namespace Daoris.Driver;

/// <summary>What the look reads of an operation's head from the records each time.</summary>
/// <param name="HeadRecord">The record the head's line ends at now.</param>
/// <param name="OpenedByOwnWord">That record was opened from the operation's head record by the operation's keyed word.</param>
/// <param name="MoveWhy">Why the line moved otherwise (<see cref="TogetherWhy"/>); null reads as another word.</param>
/// <param name="HeadRunning">The head's turn is running.</param>
/// <param name="LineTip">The line's tip, or its name: the start of the range a landing replays.</param>
internal sealed record TogetherFacts(string HeadRecord, bool OpenedByOwnWord, string? MoveWhy, bool HeadRunning, string LineTip);

/// <summary>
/// The look's step of one operation (LINESTOGETHER1d, the review-flow design §4): the head's line carried to a successor its own word
/// opened, else stale; a member's tip moving before <i>checked</i>, stale; the head's turn <i>replaying</i>, then <i>checking</i>; the
/// accounting at the head's tip, tried once per tip; a head moved after <i>checked</i>, back to <i>checking</i> and checked again.
/// </summary>
internal sealed class TogetherLook(string home, Func<DateTimeOffset> now)
{
    /// <summary>Step the operation by what is read now; the operation as it then stands, or null where it is not kept.</summary>
    public async Task<TogetherOperation?> StepAsync(
        string root, string id, TogetherFacts facts, IReadOnlyList<IntegrationMergeRule>? lineRules, CancellationToken ct)
    {
        var store = new TogetherOperations(home);
        if (store.Of(id) is not { IsOpen: true } operation) return store.Of(id);

        if (!string.Equals(facts.HeadRecord, operation.Head.Record, StringComparison.OrdinalIgnoreCase)
            && store.LineMoved(id, facts.HeadRecord, facts.OpenedByOwnWord, facts.MoveWhy ?? TogetherWhy.AnotherWord, now()) is { } moved)
        {
            if (!moved.IsOpen) return moved;
            operation = moved;
        }

        if (operation.Phase is TogetherPhase.Asked or TogetherPhase.Replaying or TogetherPhase.Checking)
        {
            foreach (var member in operation.Members)
            {
                if (await TipAsync(member.Tree, ct).ConfigureAwait(false) is { } tip
                    && store.MemberAt(id, member.Session, tip, now()) is { } stale)
                {
                    return stale;
                }
            }
        }

        if (operation.Phase == TogetherPhase.Asked && facts.HeadRunning) operation = store.Replaying(id, now()) ?? operation;
        if (operation.Phase == TogetherPhase.Replaying && !facts.HeadRunning) operation = store.TurnEnded(id, now()) ?? operation;
        if (operation.Phase is not (TogetherPhase.Checking or TogetherPhase.Ready) || facts.HeadRunning) return operation;
        if (await TipAsync(operation.Head.Tree, ct).ConfigureAwait(false) is not { } headTip) return operation;
        if (operation.Phase == TogetherPhase.Ready) operation = store.HeadAt(id, headTip, now()) ?? operation;
        if (operation.Phase != TogetherPhase.Checking
            || string.Equals(operation.Tried, headTip, StringComparison.OrdinalIgnoreCase))
        {
            return operation;
        }

        var check = await TogetherChecks.AccountAsync(root, operation, headTip, facts.LineTip, lineRules, ct).ConfigureAwait(false);
        return store.Checked(id, headTip, check.Paths, check.Failed, now()) ?? store.Of(id);
    }

    private static async Task<string?> TipAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } tip ? tip : null;
    }
}
