namespace Daoris.Driver;

/// <summary>
/// The agent would not resume the conversation an answer continues (ANSWER1a, D131 §2): nothing was prompted, and the
/// answer is carried on in a new session, saying <see cref="Why"/>.
/// </summary>
/// <remarks>
/// A <see cref="DriverException"/> like every failure of this door, so a caller that does not continue anything reads it
/// as one; the continuation catches it first, because a refused resume is not a turn that failed.
/// </remarks>
public sealed class AcpResumeRefused(ContinueReason why, string? words)
    : DriverException(words is { Length: > 0 } ? $"{why.Sentence}: {words}" : why.Sentence), IResumeRefused
{
    /// <summary>Why, by a code the log writes and a line the note says, which never quotes the agent.</summary>
    public ContinueReason Why { get; } = why;

    /// <summary>The agent's own words, where it refused in any: for the record's conversation on this machine, never a note.</summary>
    public string? Words { get; } = words;
}
