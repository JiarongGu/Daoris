namespace Daoris.Driver;

/// <summary>What a combined Accept brought a member into: the branch, or the line a merge went into, and the tip it was bound at.</summary>
public sealed record TogetherBrought(string Operation, string Session, string Tree, string Tip, string Into, bool Line, DateTimeOffset At);

/// <summary>
/// The inclusion record of every form (LINESTOGETHER1d, the review-flow design §4): a landed operation, not a landing record, says what
/// a member was brought into, since a merge keeps none of its own.
/// </summary>
public static class TogetherInclusion
{
    /// <summary>The newest landed operation that brought <paramref name="session"/> in; null where none did, or the store does not read.</summary>
    public static TogetherBrought? Of(string home, string session) =>
        Brought(home).LastOrDefault(each => string.Equals(each.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>The newest landed operation that brought in the member whose tree is <paramref name="tree"/>.</summary>
    public static TogetherBrought? OfTree(string home, string tree) =>
        Brought(home).LastOrDefault(each => TogetherOperation.SameTree(each.Tree, tree));

    /// <summary>What a member's own landing says where its work was brought in already.</summary>
    public static string Words(TogetherBrought brought) =>
        $"its work at {TextBounds.ShortCommit(brought.Tip, 8)} was brought into {(brought.Line ? $"the line `{brought.Into}`" : $"`{brought.Into}`")} "
        + $"by another session's Accept (operation {brought.Operation}), so it lands no second time; a later commit of its own lands as any does";

    private static IEnumerable<TogetherBrought> Brought(string home)
    {
        IReadOnlyList<TogetherOperation> all;
        try
        {
            all = new TogetherOperations(home).All();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var operation in all.Where(each => each.Phase == TogetherPhase.Landed && each.Attempt is not null).OrderBy(each => each.Since))
        {
            foreach (var member in operation.Members)
            {
                yield return new TogetherBrought(operation.Id, member.Session, member.Tree, member.Tip, operation.Attempt!.Target,
                    operation.Attempt.Form == TogetherForms.Merge, operation.Since);
            }
        }
    }
}

/// <summary>
/// The combined Accept (LINESTOGETHER1d, the review-flow design §4): the head's one landing of the operation bound to it. Each member's
/// gate is read as its own Accept reads it, pinned to its bound tip, and one that holds lands nothing. Under the head tree's hold, before
/// anything is made, the head at its checked tip, every member at its bound tip and every member change accounted again; then the attempt
/// is written; then the landing, its record listing the members; again before the push; then what came of it.
/// </summary>
internal sealed class TogetherAccepts(TreeContext context, TreeLands lands)
{
    /// <param name="memberGates">Each member's gate by session; a member with none is held by nothing.</param>
    /// <param name="judged">The head's own judgement again under the hold, as its press makes it.</param>
    /// <param name="beforePush">The head's own judgement before its push.</param>
    public async Task<TreeLanding> LandAsync(
        string operationId, LandingSubject subject, IReadOnlyDictionary<string, IWorkplaceGate?> memberGates, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null, IWorkplaceGate? gate = null,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null, Func<LandedBranch?, bool, CancellationToken, Task<string?>>? beforePush = null)
    {
        var store = new TogetherOperations(context.Home);
        if (store.Of(operationId) is not { } operation) return Refused("nothing is being brought together under that operation");
        if (operation.Phase != TogetherPhase.Ready || operation.Checked is not { } checkedTip)
        {
            return Refused($"its sessions are being brought together and are not ready to accept ({operation.Phase})");
        }

        foreach (var member in operation.Members)
        {
            if (memberGates.GetValueOrDefault(member.Session)?.WithTip(member.Tip).Refusal is { } held)
            {
                return new(false, EntryLandWords.Held(member.Session, held.Message)) { Refusal = held.Refusal };
            }
        }

        if (await TreeGates.CheckoutOfTreeAsync(operation.Head.Tree, ct).ConfigureAwait(false) is not { } root) return Refused("the head's tree is not here");
        var (workspace, repository) = context.OwnerOf(Path.GetFullPath(operation.Head.Tree));
        if ((await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch is not { } line) return Refused("its line is not known");
        var plan = await lands.PlanAsync(operation.Head.Tree, subject, ct, gate?.Process).ConfigureAwait(false);
        var rule = LandingRules.Effective(context.Config(), repository, workspace).Rule;
        var form = plan.Form == LandingForm.Merge ? TogetherForms.Merge : rule.Squash ? TogetherForms.Squash : TogetherForms.Branch;
        var brought = operation.Members.Select(member => new LandedWork(member.Session, member.Tip) { Quest = member.Quest, Unit = member.Unit }).ToList();

        var landed = await lands.LandAsync(operation.Head.Tree, subject with { Brought = brought }, ct, inUse, AcceptedBy.Person,
            gate?.WithTip(checkedTip) ?? gate, joins: false, judged: async (waited, token) =>
            {
                if (judged is not null && await judged(waited, token).ConfigureAwait(false) is { } refused) return refused;
                if (await RecheckAsync(root, operation, checkedTip, line, token).ConfigureAwait(false) is { } why) return Refused(why);
                var expected = (await TogetherSettle.RefAsync(root, plan.Target, token).ConfigureAwait(false)).Tip;
                return store.Landing(operation.Id, new TogetherAttempt(form, plan.Target, expected, checkedTip, context.Clock())) is null
                    ? Refused("it is no longer ready to accept")
                    : null;
            },
            beforePush: async (landing, waited, token) =>
                (beforePush is null ? null : await beforePush(landing, waited, token).ConfigureAwait(false))
                ?? await RecheckAsync(root, operation, checkedTip, line, token).ConfigureAwait(false),
            together: operation.Id).ConfigureAwait(false);

        await SettleAsync(store, root, operation.Id, landed, plan.Target, ct).ConfigureAwait(false);
        return landed.Landed
            ? landed with { Message = $"{landed.Message} Brought in with it: {string.Join(", ", operation.Members.Select(each => $"`{each.Session}`"))}." }
            : landed;
    }

    /// <summary>What came of the attempt: landed where the landing says so, else settled by its git effects as a restart settles it.</summary>
    private async Task SettleAsync(TogetherOperations store, string root, string id, TreeLanding landed, string target, CancellationToken ct)
    {
        if (store.Of(id) is not { Phase: TogetherPhase.Landing, Attempt: { } attempt } operation) return;
        if (landed.Landed)
        {
            var into = attempt.Form == TogetherForms.Merge ? target : landed.Branch ?? target;
            store.Correlate(id, resulting: (await TogetherSettle.RefAsync(root, into, ct).ConfigureAwait(false)).Tip);
            store.Settle(id, TogetherSettled.Landed, context.Clock());
            return;
        }

        var integration = new IntegrationStore(context.Home);
        var evidence = await TogetherSettle.ReadAsync(root, operation, each => TogetherWatch.Recorded(context.Home, operation.Repository, each),
            integration.Request, integration.Of, ct).ConfigureAwait(false);
        store.Settle(id, TogetherSettle.Decide(attempt, evidence), context.Clock(), evidence.Request?.Id);
    }

    /// <summary>
    /// The head at its checked tip, every member at its bound tip, and every member change accounted again from the head's tip; the
    /// words of what failed, or null.
    /// </summary>
    private static async Task<string?> RecheckAsync(string root, TogetherOperation operation, string checkedTip, string line, CancellationToken ct)
    {
        if (!string.Equals(await TipAsync(operation.Head.Tree, ct).ConfigureAwait(false), checkedTip, StringComparison.OrdinalIgnoreCase))
        {
            return "the head moved since it was checked, so it is checked again before it lands";
        }

        foreach (var member in operation.Members)
        {
            if (!string.Equals(await TipAsync(member.Tree, ct).ConfigureAwait(false), member.Tip, StringComparison.OrdinalIgnoreCase))
            {
                return $"`{member.Session}` moved since it was bound";
            }
        }

        var check = await TogetherChecks.AccountAsync(root, operation, checkedTip, line, null, ct).ConfigureAwait(false);
        return check.Failed is { } failed ? $"its sessions' work is no longer all accounted for at the head ({failed})" : null;
    }

    private static async Task<string?> TipAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } tip ? tip : null;
    }

    private static TreeLanding Refused(string why) => new(false, $"Nothing was landed: {why}.") { Refusal = AutoLandingCode.Refused };
}
