namespace Daoris.Driver;

/// <summary>
/// A conversation's limit as its line's rules read it: read by
/// <see cref="LimitMove"/> and its ended move before their walks. The limits rule's <c>wait</c> keeps it on its own account until that account is back, where that
/// is within the rule's <c>upTo</c>; its <c>ask</c>, and the moves rule's bound, keep it there for the person, whose hand-over or next word
/// goes on. A move to another choice the walk then makes is counted in <c>moves</c>.
/// </summary>
internal static class LimitRuled
{
    /// <summary>
    /// Whether the rules keep <paramref name="sessionId"/> on its own account: waiting there as a picked account's conversation does, the rule's
    /// act said and kept; null where the walk goes on as it always did. Its moves are read under <paramref name="line"/>, its line's key.
    /// </summary>
    public static StartedFrom? Holds(
        ChatContext context, LimitWaits waits, ConversationRules rules, PriorSession prior, string sessionId, string line, ITurnedChat? chat, string ranOn)
    {
        var until = context.Harnesses.CoolingOf(ranOn, prior.Profile, prior.ModelOn(ranOn))?.Until;
        if (rules.Judge(prior, line, StaffConditions.Limit, until) is not { } ruled) return null;
        var now = context.Harnesses.Clock();
        string count, does;
        int n;
        switch (ruled.Course)
        {
            // The rule waits at most its upTo from this judgement (the course holds no window for a conversation): an account back later is walked past now.
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

        if (chat is { Turns.Limited: false }) waits.Wait(sessionId, chat, ranOn, prior.Profile, prior.ModelOn(ranOn));
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
            StaffCourse.LimitMoved(context.Harnesses.StaffFor(context.Home), RuledLine.Of(context.Home, prior).Role, line, prior, taken);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            // A move the store would not count still moved: the next move reads the count it holds.
        }
    }
}
