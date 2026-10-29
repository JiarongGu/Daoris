namespace Daoris.Driver;

// FOUNDATION1e part 1 (the foundation design §3.1): what pulls the course. Each member is one of §3.1's triggers; the survey's paths A to N
// (the foundation survey §2.2) are reached by them as the design's paths table states, and the census (fixtures/course-sites.json) names
// the site each path is decided at today.

/// <summary>What asks the course how a line goes on (the foundation design §3.1's triggers).</summary>
public enum CourseTrigger
{
    /// <summary>Words kept on a parked or ended record (paths A, B).</summary>
    WordsKept,

    /// <summary>A solicited answer: a helper's, a second opinion's findings or the supervisor's (A, B).</summary>
    Answer,

    /// <summary>A limit's end: the part ended on an account's limit (D, F).</summary>
    LimitEnd,

    /// <summary>A limit's wake: the cool-off the line waited on has passed (E, F).</summary>
    LimitWake,

    /// <summary>A transient end past its bound: the course decides only what follows the bound (N, D210 point 3).</summary>
    TransientPastBound,

    /// <summary>A turn's end with nothing taking: the curve's moment (G).</summary>
    TurnEnd,

    /// <summary>A hand-over, pressed or asked (H, I).</summary>
    HandOver,

    /// <summary>The briefed press (J).</summary>
    BriefedPress,

    /// <summary>A conversation proven lost (J, B).</summary>
    ConversationLost,

    /// <summary>The planner's carry-ons (K), which of them in <see cref="CourseCarryOn"/>.</summary>
    CarryOn,

    /// <summary>A workflow's send-back to an ended conversation: not a course decision of its own, an ended chat's go-on as B (L).</summary>
    SendBack,

    /// <summary>*Go on in a new session* (C).</summary>
    GoOnNew,

    /// <summary>*Start a conversation with these words*: a new line (C).</summary>
    StartFrom,

    /// <summary>The come-up: the sweep passing each record when the shell starts.</summary>
    ComeUp,
}

/// <summary>Which of the planner's carry-ons asked (K): the reasons a driven line goes on by itself.</summary>
public enum CourseCarryOn
{
    /// <summary>The part was cut off before its work was done.</summary>
    CutOff,

    /// <summary>A stop was released.</summary>
    StopReleased,

    /// <summary>The part was retried after a refusal.</summary>
    Retried,

    /// <summary>An account's limit ended the part.</summary>
    Limited,

    /// <summary>The quest was brought forward.</summary>
    BroughtForward,
}
