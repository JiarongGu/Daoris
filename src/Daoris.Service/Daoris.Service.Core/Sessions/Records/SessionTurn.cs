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

    /// <summary>The input tokens its door reported for it; null where it reported none, or the turn runs.</summary>
    public long? Input { get; init; }

    /// <summary>The output tokens its door reported for it; null where it reported none, or the turn runs.</summary>
    public long? Output { get; init; }

    /// <summary>The record it ran in: a move to another account or agent still runs its turn in a record of its own on the same line.</summary>
    public string? Part { get; init; }

    /// <summary>The agent that ran it, kept with the turn at its open.</summary>
    public string? Agent { get; init; }

    /// <summary>The account it ran on, this machine's own, kept with the turn at its open.</summary>
    public string? Account { get; init; }

    /// <summary>The adapter's version it ran through, kept with the turn at its open.</summary>
    public string? Adapter { get; init; }

    /// <summary>The captured run its conversation wrote, a machine-local path; null while it runs and before its record names one.</summary>
    public string? Transcript { get; init; }

    /// <summary>The conversation it continued or began: the agent's own id for it, read from its transcript's name.</summary>
    public string? Conversation => Transcript is { Length: > 0 } transcript ? Path.GetFileNameWithoutExtension(transcript) : null;

    /// <summary>When it ended: the next turn's start, or its record's end; null while it runs.</summary>
    public DateTimeOffset? Ended { get; init; }

    /// <summary>How it ended, kept where its record went on after it; null for the record's last turn, whose end is the record's own.</summary>
    public SessionTurnEnd? End { get; init; }
}
