namespace Daoris.Driver;

/// <summary>
/// How a conversation that ended says so (MSG1c, the language design §4 rows 61–62): the lines its record concludes with. Course's, the lowest
/// subdomain that reads them; the chat runner's own names answer through them.
/// </summary>
internal static class ChatEnded
{
    /// <summary>
    /// What a conversation's ending note adds where Daoris kept no id for its harness conversation (MSG1c, D137 §4.2): words
    /// written to it once it ended cannot go on in it, which the record says before anyone writes.
    /// </summary>
    public const string NotKept = "Daoris kept no id for its conversation, so words written to it cannot go on in it.";

    /// <summary>The English a conversation the person ended concludes with (<see cref="EndedNote"/>): what the limits stage leaves by.</summary>
    internal const string EndedByPersonNote = "the person ended the conversation.";

    /// <summary>
    /// How a conversation that simply ended says so (the language design §4 rows 61–62), and <see cref="NotKept"/> after it
    /// where no id was kept (MSG1c; the code LANG1a added, its note under D142), each line with its code.
    /// </summary>
    /// <param name="stopped">The person ended it.</param>
    internal static Noted EndedNote(bool stopped, bool notKept)
    {
        var ended = stopped
            ? Noted.Of(NoteCodes.ChatEndedByPerson, EndedByPersonNote)
            : Noted.Of(NoteCodes.ChatEnded, "the conversation ended; its commits are its record.");
        return notKept ? ended.Then(" ", Noted.Of(NoteCodes.ChatNotKept, NotKept)) : ended;
    }
}
