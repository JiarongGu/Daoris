namespace Daoris.Driver;

/// <summary>
/// How a conversation that ended says so: the lines its record concludes with. Course's, the lowest
/// subdomain that reads them; the chat runner's own names answer through them.
/// </summary>
internal static class ChatEnded
{
    /// <summary>
    /// What a conversation's ending note adds where Daoris kept no id for its harness conversation: words
    /// written to it once it ended cannot go on in it, which the record says before anyone writes.
    /// </summary>
    public const string NotKept = "Daoris kept no id for its conversation, so words written to it cannot go on in it.";

    /// <summary>The English a conversation the person ended concludes with (<see cref="EndedNote"/>): what the limits stage leaves by.</summary>
    internal const string EndedByPersonNote = "the person ended the conversation.";

    /// <summary>
    /// How a conversation that simply ended says so, and <see cref="NotKept"/> after it
    /// where no id is kept, each line with its code.
    /// </summary>
    /// <param name="stopped">The person ended it.</param>
    internal static Noted EndedNote(bool stopped, bool notKept)
    {
        var ended = stopped
            ? Noted.Of(ChatNoteCodes.ChatEndedByPerson, EndedByPersonNote)
            : Noted.Of(ChatNoteCodes.ChatEnded, "the conversation ended; its commits are its record.");
        return notKept ? ended.Then(" ", Noted.Of(ChatNoteCodes.ChatNotKept, NotKept)) : ended;
    }
}
