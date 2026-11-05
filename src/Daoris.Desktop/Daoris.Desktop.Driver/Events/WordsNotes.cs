namespace Daoris.Driver;

/// <summary>
/// The notes for words that did not reach an agent, or that it or the record would not take: each English as its site always
/// wrote it, byte for byte, with its parts.
/// </summary>
public static class WordsNotes
{
    /// <summary>What the agent said when it would not take the person's words mid-turn, in its own words.</summary>
    /// <param name="agentWords">The agent's own words.</param>
    /// <param name="who">What the English calls the one that refused: the chat runner has always said "conversation".</param>
    public static Noted Refused(string agentWords, string who = "session") =>
        Noted.Of(ChatNoteCodes.WordsRefused, $"— the {who} would not take what the person added:")
            .Then(" ", Noted.Said(agentWords, NoteBy.Agent));

    /// <summary>Words that never reached a conversation, the program's own message beneath (it may hold a path).</summary>
    public static Noted NeverReached(string message) =>
        Noted.Of(ChatNoteCodes.WordsNotReached, "— what the person added never reached it:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A message the agent's process had gone before it could take.</summary>
    public static Noted NotSent() => Noted.Of(ChatNoteCodes.WordsNotSent, "the message could not be sent: the agent had gone.");

    /// <summary>The words a conversation started with that could not be taken off their session's record, the service's message beneath.</summary>
    public static Noted StartNotTaken(string session, string message) =>
        Noted.Of(
                ChatNoteCodes.WordsStartNotTaken, $"— the words this conversation started with could not be taken off session `{session}`:",
                ("session", session))
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>Held words the record would not keep once it ended, the service's message beneath.</summary>
    public static Noted NotKept(string message) =>
        Noted.Of(ChatNoteCodes.WordsNotKept, "— what you said as it wound up was not kept for it to go on with:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>
    /// Words the person sent that never reached the session, and why. <paramref name="never"/> is the English between the count
    /// and the reason, which the two doors have always worded a little differently.
    /// </summary>
    public static Noted Unreached(int count, string never, string why) =>
        Noted.Of(
            ChatNoteCodes.WordsUnreached, $"— {count} message(s) the person sent {never}: {UnreachedWhy.English(why)}.",
            ("count", count), ("why", why));

    /// <summary>Words handed to a session that could not be taken off its record, the service's own message beneath.</summary>
    public static Noted NotTaken(string message) =>
        Noted.Of(ChatNoteCodes.WordsNotTaken, "— your words could not be taken off its record:")
            .Then(" ", Noted.Said(message, NoteBy.Program));
}
