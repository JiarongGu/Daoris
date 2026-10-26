namespace Daoris.Driver;

/// <summary>Why a conversation was handed to another agent, by code (MIXAGENT1a-i, D163): the page's <c>note.handed.why.*</c>.</summary>
public static class HandOverWhy
{
    /// <summary>The person pressed it.</summary>
    public const string Press = "press";

    /// <summary>The account it ran on was near its limit.</summary>
    public const string Near = "near";

    /// <summary>The account it ran on reached its limit.</summary>
    public const string Limit = "limit";

    /// <summary>The agent asked for it.</summary>
    public const string Agent = "agent";

    /// <summary>
    /// Daoris moved it on its own by the person's switching setting (D165 point 3): its account was over the curve that setting
    /// draws, and another account of the same agent had room. A reason a record continues another by (MIXAGENT1a-iii).
    /// </summary>
    /// <remarks>A box takes it since SWITCH1c-i, which declares its coded line; the automatic move that asks for it is SWITCH1c-ii's.</remarks>
    public const string Curve = "curve";

    /// <summary>
    /// Its own conversation was proven lost, and it went on by itself in a new part (SESSCTX1b, D189 point 3; the session-context
    /// design §7). A reason a record continues another by; no box takes it.
    /// </summary>
    public const string Resume = "resume";

    /// <summary>
    /// A carry-on on the same account (D80, TOOL4f): after a cut-off, a time-out, a crash, a refused turn or an interrupted take
    /// (SESSCTX1b). A reason a record continues another by; no box takes it.
    /// </summary>
    public const string CarryOn = "carry-on";

    /// <summary>
    /// A driven record's fallback (D131 point 3): its resume was refused and it went on in a new part (SESSCTX1b). A reason a record
    /// continues another by; no box takes it.
    /// </summary>
    public const string Retry = "retry";

    /// <summary>
    /// A conversation's replacement (MODELROLE1d2, D205 §3.5(b)): the record it continues ended <c>choice-not-offered</c> at its open,
    /// before any prompt, and this one opened on the walk's next answer with its words. A reason a record continues another by; no box
    /// takes it.
    /// </summary>
    public const string NotOffered = "not-offered";

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
        _ => why,
    };

    /// <summary>
    /// Whether a word is one of the reasons a hand-over's box takes: the five a hand-over is asked for (SESSCTX1b keeps the box's
    /// check on them; <see cref="Continues"/> is every reason a record names).
    /// </summary>
    public static bool Known(string? why) => why is Press or Near or Limit or Agent or Curve;

    /// <summary>Whether a word is one of the reasons a record continues another by (SESSCTX1b): each has its <c>note.handed.why.*</c> line.</summary>
    public static bool Continues(string? why) => Known(why) || why is Resume or CarryOn or Retry or NotOffered;
}
