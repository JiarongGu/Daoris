namespace Daoris.Driver;

/// <summary>What an act may spend beyond its decision: money, a sign-in, or a destination outside the repository.</summary>
public static class ActSpending
{
    public const string None = "none";
    public const string Money = "money";
    public const string SignIn = "sign-in";
    public const string Destination = "destination";
}

/// <summary>
/// One act's row of the census: what it spends, the grant that covers that spend beside the act's own (none where no grant does, so the
/// spend stays the owner role's), and the subject it is pinned to, with which it lapses.
/// </summary>
public sealed record ActSpend(string Spends, string? Covered, string Pin);

/// <summary>
/// The census of every supervisor kind's act and every steward act: each declares what it spends and its pin. An act is
/// admitted only where every grant on its chain (the role that acts, and each role that allowed it) lists both the act and the grant that
/// covers what it spends; a refusal names the grant it lacks. A new act with no row here fails the census test.
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
        ["allow.once"] = Free("the held request"),
        ["allow.reject"] = Free("the held request"),
        ["allow.standing"] = Free("the held request's rule at its place"),
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
        // What a go-ahead lets through is judged by the place's risk and the owner's marks (GoAheadAuthority), never spent by the grant alone.
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

    /// <summary>
    /// The grant missing on <paramref name="chain"/>'s grants for <paramref name="act"/> where it <paramref name="spends"/> (its row's spend, or
    /// none where this use spends nothing): the act itself, else the grant that covers the spend, else the spend's name where no grant covers
    /// it; none where every grant on the chain covers both.
    /// </summary>
    /// <param name="chain">Each grant on the act's chain: the acting role's, then each delegator's.</param>
    public static string? Missing(IReadOnlyList<IReadOnlyCollection<string>> chain, string act, bool spends)
    {
        if (Of(act) is not { } row) return act;
        foreach (var grants in chain)
        {
            if (!grants.Contains(act)) return act;
            if (!spends || row.Spends == ActSpending.None) continue;
            if (row.Covered is not { } covering) return row.Spends;
            if (!grants.Contains(covering)) return covering;
        }

        return null;
    }
}
