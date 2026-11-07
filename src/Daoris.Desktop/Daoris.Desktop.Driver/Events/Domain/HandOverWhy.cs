namespace Daoris.Driver;

/// <summary>Why a conversation is handed to another agent, by code: the page's <c>note.handed.why.*</c>.</summary>
public static class HandOverWhy
{
    /// <summary>The person pressed it.</summary>
    public const string Press = "press";

    /// <summary>The account it runs on is near its limit.</summary>
    public const string Near = "near";

    /// <summary>The account it ran on reached its limit.</summary>
    public const string Limit = "limit";

    /// <summary>The agent asked for it.</summary>
    public const string Agent = "agent";

    /// <summary>
    /// Daoris moves it on its own by the person's switching setting: its account is over the curve that setting
    /// draws, and another account of the same agent has room. A reason a record continues another by.
    /// </summary>
    /// <remarks>A box takes it, and declares its coded line; the automatic move asks for it.</remarks>
    public const string Curve = "curve";

    /// <summary>
    /// Its own conversation is proven lost, and it goes on by itself in a new part. A reason a record continues another by; no box
    /// takes it.
    /// </summary>
    public const string Resume = "resume";

    /// <summary>
    /// A carry-on on the same account: after a cut-off, a time-out, a crash, a refused turn or an interrupted take.
    /// A reason a record continues another by; no box takes it.
    /// </summary>
    public const string CarryOn = "carry-on";

    /// <summary>
    /// A driven record's fallback: its resume is refused and it goes on in a new part. A reason a record
    /// continues another by; no box takes it.
    /// </summary>
    public const string Retry = "retry";

    /// <summary>
    /// A conversation's replacement: the record it continues ended <c>choice-not-offered</c> at its open,
    /// before any prompt, and this one opens on the walk's next answer with its words. A reason a record continues another by; no box
    /// takes it.
    /// </summary>
    public const string NotOffered = "not-offered";

    /// <summary>A role's policy moved the line to another choice because its service did not answer. A reason a record continues another by.</summary>
    public const string Transient = "transient";

    /// <summary>A role's policy moved the line to another choice after its fatal errors again.</summary>
    public const string Failed = "failed";

    /// <summary>A role's policy moved the line to another choice after a quiet stop.</summary>
    public const string Quiet = "quiet";

    /// <summary>A role's policy moved the line to another choice because its choice reached its daily budget.</summary>
    public const string Budget = "budget";

    /// <summary>The reason as the brief's agent reads it, in Daoris's one English: after <c>because</c>.</summary>
    public static string Briefed(string why) => why switch
    {
        Press => "the person asked for it",
        Near => "the account it ran on was near its limit",
        Limit => "the account it ran on reached its limit",
        Agent => "the agent asked for it",
        Curve => "the account it ran on was over the usage curve the person's switching setting draws",
        Resume => "its own conversation was lost, so it could not go on in it",
        CarryOn => "it stopped before its work was done",
        Retry => "its own conversation could not be resumed",
        NotOffered => "its agent did not offer the model asked on that account, so it never began",
        Transient => "the service it ran on did not answer",
        Failed => "it failed again and again",
        Quiet => "it went quiet and was stopped",
        Budget => "its choice reached its daily budget",
        _ => why,
    };

    /// <summary>The reason as the record's English says it, after <c>because</c>: the page's <c>note.handed.why.*</c> in English.</summary>
    public static string Noted(string why) => why switch
    {
        Press => "the person asked for it",
        Near => "its account was near its limit",
        Limit => "its account reached its limit",
        Agent => "the agent asked for it",
        Curve => "its account was over its usage curve",
        Resume => "its own conversation was lost",
        CarryOn => "it stopped before its work was done",
        Retry => "its own conversation could not be resumed",
        NotOffered => "its agent did not offer the model",
        Transient => "its service did not answer",
        Failed => "it failed again and again",
        Quiet => "it went quiet and was stopped",
        Budget => "its choice reached its daily budget",
        _ => why,
    };

    /// <summary>
    /// Whether a word is one of the reasons a hand-over's box takes: the five a hand-over is asked for (the box's
    /// check is on them; <see cref="Continues"/> is every reason a record names).
    /// </summary>
    public static bool Known(string? why) => why is Press or Near or Limit or Agent or Curve;

    /// <summary>Whether a word is one of the reasons a record continues another by: each has its <c>note.handed.why.*</c> line.</summary>
    public static bool Continues(string? why) => Known(why) || why is Resume or CarryOn or Retry or NotOffered or Transient or Failed or Quiet or Budget;
}
