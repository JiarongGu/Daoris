namespace Daoris.Driver;

/// <summary>What *Start a conversation with these words* came to (MSG1f2): the conversation it opened, or why none.</summary>
/// <param name="SessionId">The conversation it opened; null where none opened.</param>
/// <param name="Sent">
/// The words are that conversation's first message and are off the record they were said to; false where nothing started,
/// or the conversation opened without taking them, and the words stay where they were.
/// </param>
/// <param name="Why">Why nothing was done, by a code of <see cref="WordsNever"/> or <see cref="StartFrom"/>; null otherwise.</param>
/// <param name="Message">The sentence, in the driver's words; the screen words <see cref="Why"/> itself.</param>
public sealed record StartedFrom(string? SessionId, bool Sent, string? Why, string Message);
