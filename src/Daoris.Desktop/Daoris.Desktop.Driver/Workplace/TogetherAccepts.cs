namespace Daoris.Driver;

/// <summary>What a combined Accept brought a member into: the branch, or the line a merge went into, and the tip it is bound at.</summary>
public sealed record TogetherBrought(string Operation, string Session, string Tree, string Tip, string Into, bool Line, DateTimeOffset At);

/// <summary>
/// The inclusion record of every form: a landed operation, not a landing record, says what
/// a member comes into, since a merge keeps none of its own.
/// </summary>
public static class TogetherInclusion
{
    /// <summary>The newest landed operation that brought <paramref name="session"/> in; null where none did, or the store does not read.</summary>
    public static TogetherBrought? Of(string home, string session) =>
        Brought(home).LastOrDefault(each => string.Equals(each.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every session a landed operation brought in, by the newest that did.</summary>
    public static IReadOnlyDictionary<string, TogetherBrought> All(string home)
    {
        var all = new Dictionary<string, TogetherBrought>(StringComparer.OrdinalIgnoreCase);
        foreach (var each in Brought(home)) all[each.Session] = each;
        return all;
    }

    /// <summary>The newest landed operation that brought in the member whose tree is <paramref name="tree"/>.</summary>
    public static TogetherBrought? OfTree(string home, string tree) =>
        Brought(home).LastOrDefault(each => TogetherOperation.SameTree(each.Tree, tree));

    /// <summary>What a member's own landing says where its work is already in.</summary>
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
            foreach (var brought in Of(operation)) yield return brought;
        }
    }

    /// <summary>What a landed operation brought each member into.</summary>
    public static IEnumerable<TogetherBrought> Of(TogetherOperation operation) =>
        operation.Attempt is not { } attempt
            ? []
            : operation.Members.Select(member => new TogetherBrought(
                operation.Id, member.Session, member.Tree, member.Tip, attempt.Target, attempt.Form == TogetherForms.Merge, operation.Since));
}

/// <summary>
/// The combined Accept: the head's one landing of the operation bound to it. Each member's
/// gate is read as its own Accept reads it, pinned to its bound tip, and one that holds lands nothing. Under the head tree's hold, before
/// anything is made, the head at its checked tip, every member at its bound tip and every member change accounted again; then the attempt
/// is written; then the landing, its record listing the members; again before the push; then what came of it.
/// </summary>
/// <remarks>
/// Under a delivery: one Accept carrying several sessions is one delivery, the head's
/// unit's, bound at the checked tip, whose intent chooses the form and the target; each member is brought into it and starts none of its own
/// (<see cref="DeliveryReach"/>), so there is no second fence.
/// </remarks>
internal sealed class TogetherAccepts(TreeContext context, TreeLands lands)
{
    /// <param name="memberGates">Each member's gate by session; a member with none is held by nothing.</param>
    /// <param name="judged">The head's own judgement again under the hold, as its press makes it.</param>
    /// <param name="delivery">The head's unit's delivery this Accept performs; null lands by the rule, as before.</param>
    public async Task<TreeLanding> LandAsync(
        string operationId, LandingSubject subject, IReadOnlyDictionary<string, IWorkplaceGate?> memberGates, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null, IWorkplaceGate? gate = null,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null,
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null, TreeDelivery? delivery = null)
    {
        var store = new TogetherOperations(context.Home);
        if (store.Of(operationId) is not { } operation) return Refused("nothing is being brought together under that operation");
        if (operation.Phase != TogetherPhase.Ready || operation.Checked is not { } checkedTip)
        {
            return Refused($"its sessions are being brought together and are not ready to accept ({operation.Phase})");
        }

        var delivered = delivery?.Store.Delivery(delivery.Claim.Key);
        if (delivery is not null && delivered is null) return Refused($"its delivery {delivery.Claim.Key[..12]} is not in the store");
        if (delivered is not null && !string.Equals(delivered.Intent.Tip, checkedTip, StringComparison.OrdinalIgnoreCase))
        {
            return Refused($"its delivery lands {TextBounds.ShortCommit(delivered.Intent.Tip, 8)}, and its sessions were checked together at "
                + TextBounds.ShortCommit(checkedTip, 8));
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
        var (form, target) = delivered is not null ? FormOf(delivered) : await RuleFormAsync(operation, subject, gate, repository, workspace, ct).ConfigureAwait(false);
        var brought = operation.Members.Select(member => new LandedWork(member.Session, member.Tip) { Quest = member.Quest, Unit = member.Unit }).ToList();

        var landed = await lands.LandAsync(operation.Head.Tree, subject with { Brought = brought }, ct, inUse, AcceptedBy.Person,
            gate?.WithTip(checkedTip) ?? gate, joins: false, judged: async (waited, token) =>
            {
                if (judged is not null && await judged(waited, token).ConfigureAwait(false) is { } refused) return refused;
                if (await RecheckAsync(context.Config(), root, operation, checkedTip, line, token).ConfigureAwait(false) is { } why) return Refused(why);
                var expected = (await TogetherSettle.RefAsync(root, target, token).ConfigureAwait(false)).Tip;
                return store.Landing(operation.Id, new TogetherAttempt(form, target, expected, checkedTip, context.Clock())) is null
                    ? Refused("it is no longer ready to accept")
                    : null;
            },
            together: operation.Id, beside: beside, delivery: delivery).ConfigureAwait(false);

        await SettleAsync(store, root, operation.Id, landed, target, (workspace, line), ct).ConfigureAwait(false);
        return landed.Landed
            ? landed with { Message = $"{landed.Message} Brought in with it: {string.Join(", ", operation.Members.Select(each => $"`{each.Session}`"))}." }
            : landed;
    }

    /// <summary>The form and the target the live rule lands the head by, as its press plans them: the run's bound <c>land</c> where it has one.</summary>
    private async Task<(string Form, string Target)> RuleFormAsync(
        TogetherOperation operation, LandingSubject subject, IWorkplaceGate? gate, string repository, string workspace, CancellationToken ct)
    {
        var plan = await lands.PlanAsync(operation.Head.Tree, subject, ct, gate?.Process).ConfigureAwait(false);
        var squash = gate?.Process is { Named: true } named && named.Landing.Squash;
        return (plan.Form == LandingForm.Merge ? TogetherForms.Merge : squash ? TogetherForms.Squash : TogetherForms.Branch, plan.Target);
    }

    /// <summary>The form and the target a delivery's intent lands the head by: its line for a merge, else its branch, squashed as its options say.</summary>
    private static (string Form, string Target) FormOf(DeliveryRecord delivered) => delivered.Intent.Method == DeliveryMethods.Merge
        ? (TogetherForms.Merge, delivered.Intent.Line)
        : (TreeDelivering.SquashOf(delivered.Authorization.Options) == true ? TogetherForms.Squash : TogetherForms.Branch, delivered.Intent.Branch ?? "");

    /// <summary>What came of the attempt: landed where the landing says so, else settled by its git effects as a restart settles it.</summary>
    private async Task SettleAsync(
        TogetherOperations store, string root, string id, TreeLanding landed, string target, (string Workspace, string Line) place, CancellationToken ct)
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
        var decided = TogetherSettle.Decide(attempt, evidence);
        if (decided == TogetherSettled.Landed) TogetherSettle.WriteMissed(context.Home, operation, evidence, place.Workspace, place.Line, context.Clock());
        store.Settle(id, decided, context.Clock(), evidence.Request?.Id);
    }

    /// <summary>
    /// The head at its checked tip, every member at its bound tip, and every member change accounted again from the head's tip; the
    /// words of what failed, or null.
    /// </summary>
    private static async Task<string?> RecheckAsync(
        DriverConfig config, string root, TogetherOperation operation, string checkedTip, string line, CancellationToken ct)
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

        var rules = await LineRules.AcceptedAsync(config, root, operation.Repository, line, ct).ConfigureAwait(false);
        var check = await TogetherChecks.AccountAsync(root, operation, checkedTip, line, rules, ct).ConfigureAwait(false);
        return check.Failed is { } failed ? $"its sessions' work is no longer all accounted for at the head ({failed})" : null;
    }

    private static async Task<string?> TipAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } tip ? tip : null;
    }

    private static TreeLanding Refused(string why) => new(false, $"Nothing was landed: {why}.") { Refusal = LandingCode.Refused };
}
