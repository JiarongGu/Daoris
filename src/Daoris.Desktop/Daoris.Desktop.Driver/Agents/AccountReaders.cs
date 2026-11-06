namespace Daoris.Driver;

/// <summary>Where a run's failure is read for its account (the agent-lyntai design §5.2, rounds 4 and 5).</summary>
public enum ReaderSite
{
    /// <summary>A driven run's conclusion: a start's, a continuation's, a helper's, an intake's, an opinion's or a partner's.</summary>
    Conclusion,

    /// <summary>A conversation's failed open or prompt.</summary>
    Conversation,

    /// <summary>A brainstorm's pass.</summary>
    BrainstormPass,
}

/// <summary>The account readers a site calls.</summary>
[Flags]
public enum ReaderSet
{
    None = 0,

    /// <summary>The limit, which cools the account.</summary>
    Limit = 1,

    /// <summary>The sign-in, which reads the account signed out.</summary>
    SignOut = 2,

    /// <summary>The refused credential, which holds the account.</summary>
    Refuse = 4,

    /// <summary>The refused model, which passes the account for that model.</summary>
    Model = 8,
}

/// <summary>Which entry an account reader takes.</summary>
public enum AccountRoute
{
    /// <summary>A Daoris-run kind: the reader reads the failure through the door's tables, as today.</summary>
    Tables,

    /// <summary>A bound kind: the reader takes the typed outcome through its trusted entry, reading no sentence.</summary>
    Trusted,
}

/// <summary>
/// The account readers each door's site has, as today's code calls them (the agent-lyntai design §5.2, D125's recorded state), and the
/// entry a run's kind routes them through. A move changes neither: what reads nothing today goes on reading nothing.
/// </summary>
public static class AccountReaders
{
    /// <summary>
    /// The readers <paramref name="site"/> calls on <paramref name="door"/>: the protocol door's conclusion all four; the native door's
    /// driven conclusion is handed no failure (D125), so only the refused credential, read from the harness's own words, and the refused
    /// model act; a protocol conversation reads its limit and its sign-in, a protocol brainstorm pass its limit; a native conversation and
    /// a native pass read nothing.
    /// </summary>
    public static ReaderSet Of(Transport door, ReaderSite site) => (door, site) switch
    {
        (Transport.Protocol, ReaderSite.Conclusion) => ReaderSet.Limit | ReaderSet.SignOut | ReaderSet.Refuse | ReaderSet.Model,
        (Transport.Native, ReaderSite.Conclusion) => ReaderSet.Refuse | ReaderSet.Model,
        (Transport.Protocol, ReaderSite.Conversation) => ReaderSet.Limit | ReaderSet.SignOut,
        (Transport.Protocol, ReaderSite.BrainstormPass) => ReaderSet.Limit,
        _ => ReaderSet.None,
    };

    /// <summary>The entry a run's kind takes: a bound kind's outcome the trusted entries, a Daoris-run kind's its tables.</summary>
    public static AccountRoute Route(bool bound) => bound ? AccountRoute.Trusted : AccountRoute.Tables;
}
