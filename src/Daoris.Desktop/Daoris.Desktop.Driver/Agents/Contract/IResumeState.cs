namespace Daoris.Driver;

/// <summary>
/// What a native resume's one retry reads and marks, a port the run declares and Course's
/// <c>ResumeAsk</c> implements, so the retry sits behind <see cref="AgentRun"/> and names no Course type.
/// </summary>
public interface IResumeState
{
    /// <summary>The harness named the conversation on its wire, so it opened it.</summary>
    bool Named { get; }

    /// <summary>The agent refused the resume.</summary>
    bool Refused { get; }

    /// <summary>The one retry is made.</summary>
    bool Retried { get; }

    /// <summary>Told once the harness named its conversation: it opened the conversation its input carried the person's words to.</summary>
    void Opened();

    /// <summary>The same resume, tried once more: nothing the first try learned of the wire stands.</summary>
    void Again();
}
