namespace Daoris.Driver;

/// <summary>What an act may spend beyond its decision: money, a destination outside the repository, or reach beyond its harness's.</summary>
public static class ActSpending
{
    public const string None = "none";
    public const string Money = "money";
    public const string Destination = "destination";
    public const string Permission = "permission";
}

/// <summary>
/// One act's row of the census: what it spends, the grant that covers that spend beside the act's own where the risk policy gates it on a
/// role (none where no grant does), and the subject it is pinned to, with which it lapses.
/// </summary>
public sealed record ActSpend(string Spends, string? Covered, string Pin);

/// <summary>
/// The census of every supervisor kind's act and every steward act: each declares what it spends and its pin. A spend is an act kind of
/// the risk policy (<see cref="KindOf"/>) judged at the act's place. An act is admitted only where every grant on its chain (the role that
/// acts, and each role that allowed it) lists the act and what its spend's verdict asks; a refusal names the grant it lacks, or the spend.
/// A new act with no row here fails the census test.
/// </summary>
public static class ActSpends
{
    private static ActSpend Free(string pin) => new(ActSpending.None, null, pin);

    private static readonly IReadOnlyDictionary<string, ActSpend> Rows = new Dictionary<string, ActSpend>(StringComparer.Ordinal)
    {
        ["question.answer"] = Free("the parked question, unchanged while it waits"),
        ["course.go-on"] = Free("the referred point"),
        ["limit.move"] = Free("the limited end, moved to a choice the staffing declares"),
        ["limit.wait"] = Free("the limited end"),
        ["allow.once"] = new(ActSpending.Permission, null, "the held request"),
        ["allow.reject"] = Free("the held request"),
        ["allow.standing"] = new(ActSpending.Permission, null, "the held request's rule at its place"),
        ["allow.steward"] = Free("the steward's one act it allows"),
        ["files.allow-run"] = Free("the files ask"),
        ["files.allow-copy"] = Free("the files ask"),
        ["files.allow-share"] = new(ActSpending.Destination, null, "the files ask"),
        ["landing.accept"] = Free("the review record's revision"),
        ["landing.approve"] = new(ActSpending.Destination, null, "the approval's step on its attempt"),
        ["landing.reject"] = Free("the approval's step on its attempt"),
        ["memory.keep"] = Free("the staged rows' ids"),
        ["memory.decide"] = Free("the end's id"),
        ["departure.accept"] = Free("the quest's state and answers"),
        // What a go-ahead lets through is judged by the risk policy on its own act (GoAheadAuthority), never spent by the grant alone.
        [GoAheadAuthority.Act] = Free("the go-ahead's number, kind, target and act"),
        // A re-cast naming the prepaid harness spends its balance: the prepaid grant covers it.
        ["vacancy.recast"] = new(ActSpending.Money, "vacancy.prepaid", "the vacancy, until a walk fills it"),
        ["vacancy.relax"] = Free("the vacancy, until a walk fills it"),
        ["vacancy.prepaid"] = new(ActSpending.Money, "vacancy.prepaid", "the vacancy, until a walk fills it"),
        ["vacancy.wait"] = Free("the vacancy, until a walk fills it"),
        ["ask-daoris.report"] = Free("the range of turns read"),
        [StewardActNames.Drive] = Free("the repository"),
        [StewardActNames.Undrive] = Free("the repository"),
        [StewardActNames.Hold] = Free("the repository"),
        [StewardActNames.Resume] = Free("the steward's own hold of the repository"),
        [StewardActNames.Stop] = Free("the quest's running session"),
        [StewardActNames.Finish] = Free("the quest"),
        [StewardActNames.Retry] = Free("the quest's park"),
        [StewardActNames.Workflow] = Free("the repository's workflow"),
        [StewardActNames.Pause] = Free("the quest"),
        [StewardActNames.Unpause] = Free("the steward's own pause of the quest"),
        [StewardActNames.Done] = Free("the quest"),
        [StewardActNames.Delete] = Free("the quest"),
        [StewardActNames.UpdateNow] = Free("the staged update"),
        [StewardActNames.UpdateCancel] = Free("the staged update"),
        [StewardActNames.Clean] = Free("the session's tree"),
        [StewardActNames.HandOver] = Free("the session"),
        [StewardActNames.Setup] = Free("the place set up"),
    };

    /// <summary>Every act the census names: the supervisor kinds' qualified acts and the steward's.</summary>
    public static IReadOnlyCollection<string> Named => (IReadOnlyCollection<string>)Rows.Keys;

    /// <summary>The row of <paramref name="act"/> (a supervisor's qualified, a steward's bare); none for an act the census does not name.</summary>
    public static ActSpend? Of(string act) => Rows.GetValueOrDefault(act);

    /// <summary>The act kind of the risk policy what <paramref name="act"/> spends is; none for an act that spends nothing or the census does not name.</summary>
    public static RiskAct? KindOf(string act) => Of(act)?.Spends switch
    {
        ActSpending.Money => new RiskAct(RiskActs.SpendMoney, null),
        ActSpending.Destination => new RiskAct(act == "files.allow-share" ? RiskActs.PublishOutside : RiskActs.ReleaseStart, null),
        ActSpending.Permission => new RiskAct(RiskActs.ReachWiden, null),
        _ => null,
    };

    /// <summary>The grant missing on <paramref name="chain"/> for <paramref name="act"/>: the act, else what its spend's <paramref name="verdict"/> asks (the spend's name where the owner's, a role's covering grant).</summary>
    /// <param name="chain">Each grant on the act's chain: the acting role's, then each delegator's.</param>
    public static string? Missing(IReadOnlyList<IReadOnlyCollection<string>> chain, string act, bool spends, Func<RiskAct, string> verdict)
    {
        if (Of(act) is not { } row) return act;
        if (chain.Count == 0 || chain.Any(grants => !grants.Contains(act))) return act;
        if (!spends || row.Spends == ActSpending.None) return null;
        var outcome = KindOf(act) is { } kind ? verdict(kind) : RiskVerdict.OwnerOutcome;
        if (outcome == RiskVerdict.Pass) return null;
        if (outcome != RiskVerdict.Gate) return row.Spends;
        return row.Covered is { } covering && chain.Any(grants => !grants.Contains(covering)) ? covering : null;
    }
}
