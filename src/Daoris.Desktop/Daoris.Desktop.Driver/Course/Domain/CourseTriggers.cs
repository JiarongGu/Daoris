namespace Daoris.Driver;

// What pulls the course. Each member is one of its triggers; the census (fixtures/course-sites.json) names
// the site each path is decided at.

/// <summary>What asks the course how a line goes on.</summary>
public enum CourseTrigger
{
    /// <summary>Words kept on a parked or ended record.</summary>
    WordsKept,

    /// <summary>A solicited answer: a helper's, a second opinion's findings or the supervisor's.</summary>
    Answer,

    /// <summary>A limit's end: the part ended on an account's limit.</summary>
    LimitEnd,

    /// <summary>A limit's wake: the cool-off the line waited on has passed.</summary>
    LimitWake,

    /// <summary>A transient end past its bound: the course decides only what follows the bound.</summary>
    TransientPastBound,

    /// <summary>A turn's end with nothing taking: the curve's moment.</summary>
    TurnEnd,

    /// <summary>A hand-over, pressed or asked.</summary>
    HandOver,

    /// <summary>The briefed press.</summary>
    BriefedPress,

    /// <summary>A conversation proven lost.</summary>
    ConversationLost,

    /// <summary>The planner's carry-ons, which of them in <see cref="CourseCarryOn"/>.</summary>
    CarryOn,

    /// <summary>A workflow's send-back to an ended conversation: not a course decision of its own, an ended chat's go-on.</summary>
    SendBack,

    /// <summary>*Go on in a new session*.</summary>
    GoOnNew,

    /// <summary>*Start a conversation with these words*: a new line.</summary>
    StartFrom,

    /// <summary>The come-up: the sweep passing each record when the shell starts.</summary>
    ComeUp,
}

/// <summary>Which of the planner's carry-ons asked: the reasons a driven line goes on by itself.</summary>
public enum CourseCarryOn
{
    /// <summary>The part is cut off before its work is done.</summary>
    CutOff,

    /// <summary>A stop is released.</summary>
    StopReleased,

    /// <summary>The part is retried after a refusal.</summary>
    Retried,

    /// <summary>An account's limit ended the part.</summary>
    Limited,

    /// <summary>The quest is brought forward.</summary>
    BroughtForward,
}
