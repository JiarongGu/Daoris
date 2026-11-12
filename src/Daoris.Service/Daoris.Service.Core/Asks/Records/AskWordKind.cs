namespace Daoris.Knowledge;

/// <summary>How the person gave a word on an ask.</summary>
public enum AskWordKind
{
    /// <summary>The ask's own sentence — only ever its first word, derived from the record.</summary>
    Asked,

    /// <summary>An answer to a session that parked to ask them.</summary>
    Answered,

    /// <summary>A message added to a session while it ran.</summary>
    Added,

    /// <summary>
    /// Words said to a session after it ended, which went on with them: kept once a session took
    /// them — the reopened record itself, or the session a fallback handed them to.
    /// </summary>
    Reopened,

    /// <summary>Their words with a <c>reviewed</c> on a set-up step (design §3.5).</summary>
    Reviewed,

    /// <summary>Their words with a <c>not-yet</c> on a set-up step (design §3.4): what is not right yet.</summary>
    NotYet,

    /// <summary>Their words with a skip of a review (design §3.6).</summary>
    Skipped,

    /// <summary>
    /// Their message on the ask's channel: kept on the ask as every word they give is, beside the channel's own
    /// record of it. A build from before passes a kind it does not know over, so no older reader fails on it.
    /// </summary>
    Channel,

    /// <summary>Their words a requirement quotes, given when the ask is made, kept for the exchange's quote judge.</summary>
    Quoted,
}
