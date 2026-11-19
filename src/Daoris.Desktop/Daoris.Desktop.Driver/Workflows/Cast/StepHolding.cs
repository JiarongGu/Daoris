namespace Daoris.Driver;

/// <summary>What an act on a step's holder came to: the tenure it began, or why it is refused (<see cref="StepHolding"/>' codes).</summary>
public sealed record StepHeld(string Run, string Node, string Act, string? Refused)
{
    public bool Done => Refused is null;

    /// <summary>The tenure the act began: its holder, its epoch and since when.</summary>
    public StepTenure? Tenure { get; init; }

    /// <summary>The words that go with a refusal: who holds the step now, or the step's state.</summary>
    public string? Detail { get; init; }

    /// <summary>Why a done act's journal entry is missing (the cast file's refusal), its tenure written all the same; null when kept.</summary>
    public string? Unjournaled { get; init; }
}

/// <summary>The acts a standing step offers on its holder, by its tenure now.</summary>
public static class StepHoldActs
{
    /// <summary>
    /// <i>Take</i> where the person does not hold it, <i>Hand to…</i> always, <i>Give back</i> where its tenure is no cast's; with no tenure
    /// kept yet, take and hand; none on a step the owner's mark keeps theirs.
    /// </summary>
    public static IReadOnlyList<string> Of(StepTenure? tenure) => tenure?.Why == TenureWhy.Marked ? [] :
        [.. CastActs.OnStep.Where(act => act switch
        {
            CastActs.Take => tenure?.Holder != CastHolders.Person,
            CastActs.GiveBack => tenure is { Why: not TenureWhy.Cast },
            _ => true,
        })];

    public static IReadOnlyList<string> Handed(StepTenure? tenure) => tenure?.Why == TenureWhy.Marked ? [] : [CastActs.Take];
}

/// <summary>
/// <i>Take</i>, <i>Hand to…</i> and <i>Give back</i> on a run's step: the step's standing attempt passed to the person, to the holder named or back
/// to the holder the place casts now (<see cref="PlaceCastBook.At"/>), each a new epoch of the attempt's tenure (<see cref="StepTenureFiles"/>)
/// that restarts nothing, and journaled with its reason (<see cref="PlaceCastFiles"/>). A word given in the epoch it ended is refused by
/// every settling door from then on. An attempt with no tenure yet is first held by its cast, so its cast holder's word is the one refused.
/// </summary>
public static class StepHolding
{
    /// <summary>An act that is none of take, hand and give-back.</summary>
    public const string NotAct = "not-hold-act";

    /// <summary>The step has no attempt standing: not reached, or settled.</summary>
    public const string NotStanding = "not-standing";

    /// <summary>The step's holder now is the one the act names: nothing changes and nothing is journaled.</summary>
    public const string Already = "held-already";

    /// <summary>The run's or the tenures' record does not read, or the lease is not held here: nothing is written.</summary>
    public const string Unwritable = PlaceCastFiles.Unwritable;

    /// <param name="epoch">The epoch of the tenure the press's row read: a row read in an ended epoch moves nothing. Null names none.</param>
    /// <param name="by">Who presses, where its channel names an agent (<see cref="SettleBy.OfChannel"/>); null for the person's own.</param>
    public static StepHeld Act(
        NodeRecordFiles records, PlaceCastFiles casts, PlaceRiskBook risk, string run, string node, string act, string? holder, string? why, DateTimeOffset now,
        int? epoch = null, SettleBy? by = null)
    {
        run = run.Trim().TrimStart('#');
        why = string.IsNullOrWhiteSpace(why) ? null : why.Trim();
        StepHeld Refuse(string code) => new(run, node, act, code);
        if (!CastActs.OnStep.Contains(act, StringComparer.Ordinal)) return Refuse(NotAct);
        if (act == CastActs.Hand && !CastHolders.Is(holder?.Trim())) return Refuse(PlaceCastFiles.NotHolder) with { Detail = holder };
        if (new GraphRuns(records).Find(run) is not { } bound) return Refuse(RunNodeWords.NotRun);
        if (bound.Version is not { Graph: { } graph }) return Refuse(RunNodeWords.UnreadVersion);
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } declared) return Refuse(RunNodeWords.NotNode);
        if (records.Read(bound.Unit) is not { } record) return Refuse(Unwritable);
        if (record.Ended is not null) return Refuse(RunNodeWords.Ended);
        var status = WorkPerformer.StatusOf(record, node);
        if (status.State is not (NodeStates.Active or NodeStates.Unknown) || status.Attempt < 1) return Refuse(NotStanding) with { Detail = status.State };

        var (workspace, repository) = (bound.Binding.Workspace, bound.Binding.Repository);
        var role = StepRoles.Of(declared);
        var cast = casts.Book().At(role, workspace, repository, risk.At(workspace, repository));
        var first = AttemptTenures.FirstHolder(declared, cast);
        var target = act switch
        {
            CastActs.Take => CastHolders.Person,
            CastActs.Hand => holder!.Trim(),
            _ => first,
        };
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        // Tenures that do not read move nothing: who holds the step cannot be judged, so every hold act is refused, and said.
        if (!tenures.Reads(bound.Unit))
        {
            tenures.SayUnread(bound.Unit, $"{act} on {node} of #{run}");
            return Refuse(StepTenureFiles.Unread);
        }

        var current = tenures.Current(bound.Unit, node, status.Attempt)
            ?? tenures.Hold(bound.Unit, node, status.Attempt, role, first, AttemptTenures.WhyOf(cast), status.Since ?? now);
        if (current is null) return Refuse(Unwritable);
        StepHeld Held(string code) => Refuse(code) with { Detail = StepTenureFiles.Said(current), Tenure = current };
        if (epoch is { } read && read != current.Epoch) return Held(StepTenureFiles.EndedEpoch);
        // A take is the person's: an agent passes on only a step it holds.
        if (by is { } agent && (act == CastActs.Take || !SettleDoor.Holds(current.Holder, agent.Who))) return Held(SettleDoor.NotHolder);
        if (current.Holder == target) return Held(Already);
        if (current.Why == TenureWhy.Marked) return Held(SettleDoor.Marked);
        if (SettleDoor.HandRefused(cast, target) is { } owners) return Held(owners);

        var code = act switch { CastActs.Take => TenureWhy.TakeBack, CastActs.Hand => TenureWhy.HandOver, _ => TenureWhy.GiveBack };
        var handed = tenures.HandOver(bound.Unit, node, status.Attempt, current.Epoch, target, code, now);
        if (handed is not { Tenure: { } next, Refused: null })
        {
            return Refuse(handed.Refused ?? Unwritable) with { Detail = handed.Now is { } since ? StepTenureFiles.Said(since) : null, Tenure = handed.Now };
        }

        var lost = casts.Keep(new CastEntry(now, act, next.Role, target, why) { Run = run, Node = node, Attempt = next.Attempt, Epoch = next.Epoch });
        if (lost is not null) SayUnjournaled(records.Home, lost, $"{act} on {node} of #{run}");
        return new StepHeld(run, node, act, null) { Tenure = next, Unjournaled = lost };
    }

    /// <summary>A tenure written whose journal entry the cast file refused: said in the driver's log, since the act stands.</summary>
    private static void SayUnjournaled(string home, string refused, string what)
    {
        try
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("cast.journal", ("refused", refused), ("act", what));
        }
        catch (IOException)
        {
            // The log is optional; the act's answer carries the refusal.
        }
    }

    /// <summary>
    /// Whether the word <paramref name="by"/> gives is the person's press on a step another holder holds in the epoch it read, which takes the
    /// step and settles it in one act (<see cref="TakeOnPress"/>): the person's alone, never an agent's or the line's, and never in an ended
    /// epoch or on a step the owner's mark keeps theirs, which the person holds already.
    /// </summary>
    public static bool TakesOnPress(StepTenure? current, SettleBy by) =>
        by.Who == CastHolders.Person && SettleDoor.Admit(current, by) is { Code: SettleDoor.NotHolder };

    /// <summary>
    /// The person's take of the step <paramref name="current"/> holds, made with their press of <paramref name="act"/> once the press is kept,
    /// in the hold of the tenures' lock that judges the press, from the epoch it read: a new epoch held by the person, journaled as a
    /// <i>Take</i> whose reason is the press (<c>pressed &lt;act&gt;</c>). Null where nothing is written.
    /// </summary>
    public static StepTenure? TakeOnPress(StepTenureFiles.TenureHold hold, NodeRecordFiles records, string? run, StepTenure current, string act, DateTimeOffset now)
    {
        if (hold.HandOver(current.Node, current.Attempt, current.Epoch, CastHolders.Person, TenureWhy.TakeBack, now) is not { Tenure: { } next, Refused: null }) return null;
        var lost = new PlaceCastFiles(records.Home, records.HoldsLease).Keep(
            new CastEntry(now, CastActs.Take, next.Role, CastHolders.Person, $"pressed {act}") { Run = run, Node = current.Node, Attempt = next.Attempt, Epoch = next.Epoch });
        if (lost is not null) SayUnjournaled(records.Home, lost, $"pressed {act} on {current.Node} of #{run}");
        return next;
    }

    /// <summary>The terminal's sentence for what an act came to.</summary>
    public static string Said(StepHeld held) => held switch
    {
        { Done: true, Tenure: { } now } => $"workflow: {held.Node} of #{held.Run} is held by {CastHolders.Said(now.Holder)} now (epoch {now.Epoch}); its earlier holder's word is refused.",
        { Refused: Already } => $"workflow: {held.Node} of #{held.Run} is {held.Detail} already; nothing changed.",
        { Refused: NotStanding } => $"workflow: {held.Node} of #{held.Run} has no attempt standing ({held.Detail}); only a step under way or held is handed on.",
        // The step's holder now goes with an agent's refusal; a holder word that is none goes alone.
        { Refused: SettleDoor.NotHolder, Tenure: not null } => $"workflow: {held.Node} of #{held.Run} is {held.Detail}: an agent hands on only a step it holds, and never takes one.",
        { Refused: PlaceCastFiles.NotHolder } => $"workflow: `{held.Detail}` is no holder: name `person`, `agent` or `agent:<name>`.",
        { Refused: SettleDoor.Marked } => $"workflow: {held.Node} of #{held.Run} is {held.Detail}, at a destination your mark keeps yours: nothing hands it on.",
        { Refused: SettleDoor.OwnerRole } => $"workflow: {held.Node} of #{held.Run} is the owner role's, which is yours: it is handed to nobody else.",
        { Refused: StepTenureFiles.EndedEpoch } => $"workflow: {held.Node} of #{held.Run} was passed on since you read it ({held.Detail}); nothing changed.",
        { Refused: StepTenureFiles.Unread } => $"workflow: the tenures of #{held.Run} do not read, so who holds {held.Node} cannot be judged; nothing changed.",
        { Refused: NotAct } => $"workflow: `{held.Act}` is not take, hand or give-back.",
        _ => $"workflow: {held.Node} of #{held.Run} was not handed on ({held.Refused}).",
    };
}
