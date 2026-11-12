namespace Daoris.Driver;

/// <summary>
/// The words a row shows that are not a record state: each a fact about the session's quest, or for an answered park about its
/// record, derived here and never written back to the record, so there is no new session state.
/// </summary>
public static class ShownState
{
    /// <summary>The last session here of a quest parked on its failed sessions.</summary>
    public const string Parked = "parked";

    /// <summary>The last session here of a quest taken and waiting on a question asked of another repository.</summary>
    public const string AwaitingReply = "awaiting-reply";

    /// <summary>
    /// A park the person answered, still parked until the driver's next look takes it up: the one word derived from the record
    /// rather than its quest, since its state still says it waits on the person.
    /// </summary>
    public const string Answered = "answered";

    /// <summary>
    /// An ended record the person's words wait on, whose run the planner starts: the same session goes on with them, so it is
    /// working, as an answered park is.
    /// </summary>
    public const string GoingOn = "going-on";

    /// <summary>
    /// A session that ended its turn on its own helper's ask: it waits on that helper and goes on with its answer, so nothing about
    /// it waits on the person until the helper ends with no answer.
    /// </summary>
    public const string AwaitingHelper = "awaiting-helper";

    /// <summary>A session that ended its turn on its own check: the check waits its turn on the machine and the verdict goes on with it.</summary>
    public const string AwaitingCheck = "awaiting-check";

    /// <summary>
    /// A <c>failed</c> record an account's limit ended: no failure, since its line goes on by itself at the reset or on another
    /// account. The page's <c>limited</c>.
    /// </summary>
    public const string Limited = "limited";

    /// <summary>
    /// An ended record whose words are held for an account's reset: the wait, in place of the state it ended in, which read as a
    /// finished session that is stuck. Derived from the hold and never written back, so the record's state is what an older build
    /// and the service read.
    /// </summary>
    public const string LimitWaits = "limit-waits";

    /// <summary>
    /// A record's state as every reader says it: <see cref="Limited"/> for a <c>failed</c> one an account's limit ended, else its
    /// ledger state. The list, the terminal's <c>sessions</c> and Ask Daoris's session tools read this one rule.
    /// </summary>
    public static string Of(SessionRecord record) => record.State == "failed" && record.Limit ? Limited : record.State;
}
