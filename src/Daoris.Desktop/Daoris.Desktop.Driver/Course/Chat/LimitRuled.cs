namespace Daoris.Driver;

/// <summary>
/// A conversation's limit as its line's rules read it (CONTEXTDRIVE1n; the context counters design §4.2, §4.5): read by
/// <see cref="LimitMove"/> before its walk. The limits rule's <c>wait</c> keeps it on its own account until that account is back, where that
/// is within the rule's <c>upTo</c>; its <c>ask</c>, and the moves rule's bound, keep it there for the person, whose hand-over or next word
/// goes on. A move to another choice the walk then makes is counted in <c>moves</c>.
/// </summary>
internal static class LimitRuled
{
    /// <summary>
    /// Whether the rules keep <paramref name="sessionId"/> on its own account: waiting there as a picked account's conversation does, the rule's
    /// act said and kept; null where the walk goes on as it always did.
    /// </summary>
    public static StartedFrom? Holds(
        ChatContext context, LimitWaits waits, ConversationRules rules, PriorSession prior, string sessionId, ITurnedChat chat, string ranOn)
    {
        var until = context.Harnesses.CoolingOf(ranOn, prior.Profile)?.Until;
        if (rules.Judge(prior, sessionId, StaffConditions.Limit, until) is not { } ruled) return null;
        var now = context.Harnesses.Clock();
        string count, does;
        int n;
        switch (ruled.Course)
        {
            // The rule waits at most its upTo: an account back later than that is walked past now, as the rule's walk would at its end.
            case LineCourse.Wait { Why: CourseWaitWhy.PolicyWait } wait when ruled.Action.UpTo is not { } minutes || wait.Until <= now.AddMinutes(minutes):
                (count, does, n) = (SupervisorRules.Limits, SupervisorRules.Wait, 1);
                break;
            case LineCourse.Hold { Why: CourseHoldWhy.MovesBound }:
                (count, does, n) = (SupervisorRules.Moves, SupervisorRules.Ask, ruled.Moved.Moves);
                break;
            case LineCourse.Hold { Why: CourseHoldWhy.PolicyAsk }:
                (count, does, n) = (SupervisorRules.Limits, SupervisorRules.Ask, 1);
                break;
            default:
                return null;
        }

        if (!chat.Turns.Limited) waits.Wait(sessionId, chat, ranOn, prior.Profile);
        rules.Said(ruled, sessionId, count, does, n, "limit");
        return new StartedFrom(null, false, null, $"`{sessionId}`'s rule on {count} says {does}, so it waits on `{prior.Profile}`.");
    }

    /// <summary>
    /// A limit's walk moved <paramref name="prior"/>'s line to <paramref name="taken"/>, another choice: the move counted against the line's bound,
    /// where its rules set a move on a limit.
    /// </summary>
    public static void Moved(ChatContext context, PriorSession prior, string line, ActorEffective? taken)
    {
        try
        {
            StaffCourse.LimitMoved(context.Harnesses.StaffFor(context.Home), RuledLine.Of(context.Home, prior, line).Role, line, prior, taken);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            // A move the store would not count still moved: the next move reads the count it holds.
        }
    }
}
