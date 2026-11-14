namespace Daoris.Knowledge;

/// <summary>
/// One agent conversation's turn inside a session: what woke it, where, and who ran it. A session's turns are the turns of every record
/// of its line, oldest first, numbered from one; the first turn's record is the session's id for the whole piece of work.
/// </summary>
/// <param name="Number">Its place among the session's turns, from one.</param>
/// <param name="Cause">What woke it, one of <see cref="SessionTurns"/>' causes.</param>
/// <param name="At">Its start.</param>
public sealed record SessionTurn(int Number, string Cause, DateTimeOffset At)
{
    /// <summary>The workflow step that woke it, where one is named.</summary>
    public string? Node { get; init; }

    /// <summary>That step's attempt, where one is named.</summary>
    public int? Attempt { get; init; }

    /// <summary>The record it ran in: a move to another account or agent runs its turn in a record of its own on the same line.</summary>
    public string? Part { get; init; }

    /// <summary>The agent that ran it (the record's door).</summary>
    public string? Agent { get; init; }

    /// <summary>The account it ran on, this machine's own.</summary>
    public string? Account { get; init; }

    /// <summary>The adapter's version it ran through.</summary>
    public string? Adapter { get; init; }

    /// <summary>The conversation it continued or began: the agent's own id for it, where its record names one.</summary>
    public string? Conversation { get; init; }

    /// <summary>When it ended: the next turn's start, or its record's end; null while it runs.</summary>
    public DateTimeOffset? Ended { get; init; }
}
