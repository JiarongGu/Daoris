namespace Daoris.Driver;

/// <summary>How a pass answered a door that asked it for a unit (<see cref="PassAsks"/>), so a consumer can say why a run is not progressed.</summary>
public enum PassOutcome
{
    /// <summary>The pass kept the unit's judgement.</summary>
    Judged,

    /// <summary>A whole pass the door asked for read and ended.</summary>
    Ended,

    /// <summary>The pass ran and the unit is not one the last whole read governs (a run not read yet, ended, or a conversation's with no candidate): left to the next whole read.</summary>
    NotGoverned,

    /// <summary>The pass ran and ended without judging the unit.</summary>
    NotJudged,

    /// <summary>The pass did not run or failed: a drain, a closing loop, no lease, an error.</summary>
    Failed,

    /// <summary>The door's bound passed before any pass answered: the ask waits behind a pass still working, or for a pass not begun.</summary>
    Bound,
}

/// <summary>What a <see cref="PassOutcome"/> says.</summary>
public static class PassOutcomes
{
    /// <summary>Whether the outcome is an answer: the unit judged, or a whole pass read and ended.</summary>
    public static bool IsAnswer(this PassOutcome outcome) => outcome is PassOutcome.Judged or PassOutcome.Ended;

    /// <summary>The name a measure's row carries.</summary>
    public static string Name(this PassOutcome outcome) => outcome switch
    {
        PassOutcome.Judged => "judged",
        PassOutcome.Ended => "ended",
        PassOutcome.NotGoverned => "not-governed",
        PassOutcome.NotJudged => "not-judged",
        PassOutcome.Failed => "failed",
        _ => "bound-passed",
    };
}
