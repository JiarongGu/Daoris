namespace Daoris.Driver;

/// <summary>
/// A focus chosen before a conversation's first words is its first focus, so the agent opened ahead for it under a room written for
/// another place is ended at once and the conversation opened ahead again under the focus chosen: a harness reads its room at its start,
/// and a place picked before the first message would otherwise be ignored.
/// </summary>
public static class HelpFocusAhead
{
    /// <summary>Whether the person has said a word to <paramref name="session"/>, one shown waiting before its agent took it included.</summary>
    public static bool Spoken(SessionEvents events, string session) =>
        events.After(session, 0).Events.Any(e => e is { Kind: SessionEventKind.User, Origin: "person" });

    /// <summary>
    /// Whether <paramref name="session"/> has no words yet, nothing waiting, and its newest start (<see cref="HelpWired"/>) handed a room
    /// for another place than the focus it holds now. A word held in its turns (<paramref name="queue"/>) is a word said, which the reopen would end unanswered.
    /// </summary>
    public static bool Reopens(string home, string session, SessionEvents events, ChatQueue? queue = null)
    {
        var kept = new HelpConversations(home).Read(session);
        if (kept.FocusNext is not null || kept.Focus is not { } focus) return false;
        if (queue is { Taking: true } or { Opening: true } or { Queued.Count: > 0 }) return false;
        if (Spoken(events, session)) return false;
        return new HelpWired(home).Read(session).FirstOrDefault()?.Focus is { } started && !started.Equals(focus);
    }
}
