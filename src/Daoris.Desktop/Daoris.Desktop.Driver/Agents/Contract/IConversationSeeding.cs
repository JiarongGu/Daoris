namespace Daoris.Driver;

/// <summary>
/// How a provider hands a session's context to its agent as the agent's own conversation (the context's Seed form): the session's turns,
/// tool calls and results, and Daoris's record of it as the host's words, written in the agent's format under an account's home so the
/// agent resumes it under its own resume and goes on, even from turns another agent made. A provider whose agent takes no conversation in
/// binds none (<see cref="ConversationSeedings"/>), and its moves are handed Daoris's brief, the floor every door can take.
/// </summary>
public interface IConversationSeeding
{
    /// <summary>Whether <paramref name="door"/>'s agent resumes a conversation written in its format; false until one was measured to load.</summary>
    bool Seeds(string door);

    /// <summary>Writes the conversation in the door's format under <see cref="SeedAsk.Home"/>; what it placed, or why it placed nothing.</summary>
    SeedPlacement Place(SeedAsk ask);

    /// <summary>Takes out what <see cref="Place"/> wrote, where the door would not resume it.</summary>
    void Remove(string home, SeedPlacement placed);
}

/// <summary>What a seed is written from and where.</summary>
/// <param name="Door">The door the new part runs through.</param>
/// <param name="Home">The account's home the agent reads its conversations from.</param>
/// <param name="Tree">The working directory the conversation runs in, which an agent filing conversations by directory names its folder after.</param>
/// <param name="Conversation">The id the agent resumes it under, which Daoris chooses.</param>
/// <param name="History">The session's recorded events, oldest first: the person's and Daoris's words, the agent's words and its tool calls.</param>
/// <param name="Record">Daoris's record of the session, told after the history as the host's words; blank where there is none.</param>
/// <param name="At">When it is written, which an agent dating its conversations files it under.</param>
public sealed record SeedAsk(
    string Door, string Home, string Tree, string Conversation, IReadOnlyList<SessionEvent> History, string Record, DateTimeOffset At);

/// <summary>What a seed placed: the conversation's id and its files under the home, or why nothing was placed.</summary>
/// <param name="Conversation">The id the agent resumes it under.</param>
/// <param name="Files">The files written, relative to the home.</param>
/// <param name="Refused">Null where placed, else one of <see cref="SeedPlacement"/>'s codes.</param>
public sealed record SeedPlacement(string Conversation, IReadOnlyList<string> Files, string? Refused)
{
    /// <summary>The door's agent states no seed (its format takes no conversation in, or none was measured to load).</summary>
    public const string Unseeded = "unseeded";

    /// <summary>The session holds nothing to write.</summary>
    public const string Empty = "nothing-to-seed";

    /// <summary>The files could not be written.</summary>
    public const string Failed = "seed-failed";

    public static SeedPlacement Not(string conversation, string why) => new(conversation, [], why);
}
