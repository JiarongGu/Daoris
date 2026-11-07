namespace Daoris.Driver;

/// <summary>
/// <i>Pass to the lead</i>: the person's press that sends an answer kept for them to the session that
/// asked. It marks the state; the driver's look hands the answer through the one door, the helper's word with its mark unchanged, and keeps
/// <c>answers.passed</c> beside it, which the count reads as the person's word. Nothing is relabelled.
/// </summary>
public static class HelperPassToLead
{
    /// <param name="session">The session the page names, or null where the terminal names the ask alone.</param>
    public static HelperWordAnswer Pass(HelperFiles files, string? session, string askId, DateTimeOffset at)
    {
        var id = askId.Trim().TrimStart('#');
        var ask = files.Asks().FirstOrDefault(each => each.Id == id && (session is null || string.Equals(each.Asker, session, StringComparison.Ordinal)));
        if (ask is null)
        {
            return new(false, HelperAskWhy.Gone,
                session is null ? $"No ask #{id} for a helper is held on this machine." : $"Session {session} holds no ask #{id} for a helper.", false);
        }

        if (!ask.ForPerson) return new(false, HelperAskWhy.Lead, $"#{id}'s answer goes to session {ask.Asker} already.", false);
        return files.ReadState(ask.Id) switch
        {
            { Passed: not null } => new(true, null, $"#{id} is passed to session {ask.Asker} already.", false),
            { State: HelperStates.Answered, Handed: null or HelperState.KeptForPerson } state => Passed(files, ask, state, at),
            { State: HelperStates.Answered } => new(true, null, $"#{id} reached session {ask.Asker} already.", false),
            _ => new(false, HelperAskWhy.Unanswered, $"#{id} has no answer to pass yet.", false),
        };
    }

    private static HelperWordAnswer Passed(HelperFiles files, HelperAsk ask, HelperState state, DateTimeOffset at)
    {
        files.WriteState(state with { Handed = null, Passed = at });
        return new(true, null,
            $"#{ask.Id} is passed to session {ask.Asker}: the driver hands it at its next look, as the helper's answer, with your word beside it.", true);
    }
}
