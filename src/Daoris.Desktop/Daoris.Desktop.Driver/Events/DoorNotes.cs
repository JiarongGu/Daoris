namespace Daoris.Driver;

/// <summary>
/// An agent's refusal to resume a conversation, as the notes read it: its reason and the agent's own words where it refused in
/// any. The door's failure, <c>AcpResumeRefused</c>, is the agents' and answers this port.
/// </summary>
public interface IResumeRefused
{
    /// <summary>Why, by a code the log writes and a line the note says, which never quotes the agent.</summary>
    IContinueReason Why { get; }

    /// <summary>The agent's own words, where it refused in any: for the record's conversation on this machine, never a note.</summary>
    string? Words { get; }
}

/// <summary>
/// The notes for a conversation's door that failed: each English as its site always wrote it, byte for byte, with its parts. An
/// exception's message is a program's words, never a value, since it may hold a path.
/// </summary>
public static class DoorNotes
{
    /// <summary>The protocol session could not open, the program's own message beneath (the chat runner's record).</summary>
    public static Noted NotOpen(string message) =>
        Noted.Of(NoteCodes.DoorNotOpen, "the ACP session could not open:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A conversation's turn that could not be taken, the program's own message beneath (the chat runner's record).</summary>
    public static Noted TurnNotTaken(string message) =>
        Noted.Of(NoteCodes.DoorTurnNotTaken, "the turn could not be taken:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A driven session's protocol session that failed, the program's own message beneath.</summary>
    public static Noted Failed(string message) =>
        Noted.Of(NoteCodes.DoorFailed, "— the ACP session failed:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A conversation the agent would not resume: its refusal, as the chat runner has always said it.</summary>
    /// <param name="ranOn">The adapter it ran on, which <c>elsewhere</c>'s line names; null where none is known.</param>
    public static Noted ResumeRefused(IResumeRefused refused, string? ranOn) => Refusal(refused, ranOn).Then(".");

    /// <summary>An answer the agent would not resume its conversation for: its refusal, then that it is carried on.</summary>
    /// <param name="ranOn">The adapter it ran on, which <c>elsewhere</c>'s line names; null where none is known.</param>
    public static Noted AnswerCarriedOn(IResumeRefused refused, string? ranOn) =>
        Refusal(refused, ranOn)
            .Then("; ", Noted.Of(NoteCodes.DoorAnswerCarriedOn, "your answer is carried on in a new session"))
            .Then(".");

    /// <summary>
    /// The refusal's message after the lead's dash: its reason's sentence, coded by its reason, then the agent's words where it
    /// refused in any, as the door's failure composes its message.
    /// </summary>
    private static Noted Refusal(IResumeRefused refused, string? ranOn)
    {
        var said = new Noted("— ", []).Then("", Noted.Of(NoteCodes.DoorResumeRefused, refused.Why.Sentence, NoteCodes.Reason(refused.Why, ranOn)));
        return refused.Words is { Length: > 0 } words ? said.Then(": ", Noted.Said(words, NoteBy.Agent)) : said;
    }
}
