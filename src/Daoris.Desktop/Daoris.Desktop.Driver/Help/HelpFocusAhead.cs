namespace Daoris.Driver;

/// <summary>
/// A focus chosen before a conversation's first words is its first focus, so the agent opened ahead for it under a room written for
/// another place is ended at once and the conversation opened ahead again under the focus chosen: a harness reads its room at its start,
/// and a place picked before the first message would otherwise be ignored.
/// </summary>
public static class HelpFocusAhead
{
    /// <summary>
    /// Whether <paramref name="session"/> has no words yet, nothing waiting, and its newest start (<see cref="HelpWired"/>) handed a room
    /// for another place than the focus it holds now. A start that kept no focus, or none yet, reopens nothing.
    /// </summary>
    public static bool Reopens(string home, string session, SessionEvents events)
    {
        var kept = new HelpConversations(home).Read(session);
        if (kept.FocusNext is not null || kept.Focus is not { } focus) return false;
        if (events.Openings([session]).GetValueOrDefault(session) is not null) return false;
        return new HelpWired(home).Read(session).FirstOrDefault()?.Focus is { } started && !started.Equals(focus);
    }
}
