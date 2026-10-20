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
        _ => why,
    };

    /// <summary>
    /// Whether a word is one of the reasons a hand-over's box takes: the five a hand-over is asked for (SESSCTX1b keeps the box's
    /// check on them; <see cref="Continues"/> is every reason a record names).
    /// </summary>
    public static bool Known(string? why) => why is Press or Near or Limit or Agent or Curve;

    /// <summary>Whether a word is one of the reasons a record continues another by (SESSCTX1b): each has its <c>note.handed.why.*</c> line.</summary>
    public static bool Continues(string? why) => Known(why) || why is Resume or CarryOn or Retry;
}

/// <summary>Who asked for a hand-over (MIXAGENT1a-i, D163), by code.</summary>
public static class HandOverBy
{
    /// <summary>The person, by a press or the terminal verb.</summary>
    public const string Person = "person";

    /// <summary>Daoris, by its own look at an account.</summary>
    public const string Daoris = "daoris";

    /// <summary>The agent, by its own ask.</summary>
    public const string Agent = "agent";

    /// <summary>Whether a word is one of the askers.</summary>
    public static bool Known(string? by) => by is Person or Daoris or Agent;
}

/// <summary>
/// The session a new one continues, why (<see cref="HandOverWhy"/>) and who asked (<see cref="HandOverBy"/>): its record's
/// <c>from</c> link (MIXAGENT1a-iii, D163 point 4), named at the open by the driver, the half that knows. A conversation handed
/// over, a quest's session carried on after a hand-over, and one carried on onto another account after a limit say it; since
/// SESSCTX1b every carry-on and fallback says it too (<see cref="HandOverWhy.CarryOn"/>, <see cref="HandOverWhy.Retry"/>), so a
/// brief follows the line. A conversation's own going-on continues its own record and says none.
/// </summary>
public sealed record ContinuedFrom(string Session, string Why, string By);

/// <summary>A conversation's hand-over as asked (MIXAGENT1a-i): the agent and account it goes to, why, by whom, and when.</summary>
/// <param name="Agent">The adapter the new conversation runs on.</param>
/// <param name="Account">The account it runs as, or null for the one the roster picks.</param>
/// <param name="Why">One of <see cref="HandOverWhy"/>.</param>
/// <param name="AskedBy">One of <see cref="HandOverBy"/>.</param>
public sealed record HandOverRequest(string Agent, string? Account, string Why, string AskedBy, DateTimeOffset At);

/// <summary>What a hand-over's ask came to (MIXAGENT1a-i): kept, or refused by a code, and its sentence.</summary>
/// <param name="Kept">The box is kept: the shell's chat runner takes it.</param>
/// <param name="Why">Why not, by a code of <see cref="WordsNever"/> or <see cref="HandOver"/>; null where it was kept.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record HandOverAnswer(bool Kept, string? Why, string Message);

// SESSCTX1b (D189 point 4): the briefing a hand-over composed here is replaced by ContextBrief.Compose, Daoris's brief of the whole
// line, which a hand-over, an account move, a carry-on and a fallback are all handed; the outgoing agent's note (MIXAGENT1c's
// `handover_propose`) is its own section there, in HandOverNote's one wording.

/// <summary>The two lines a hand-over writes into conversations' records (MIXAGENT1a-i), each by its code with facts only.</summary>
public static class HandOverNotes
{
    /// <summary>The old conversation's end: where it went. Never an account's name or a path (D125 §3.6).</summary>
    public static Noted HandedOver(string agent, string session) =>
        Noted.Of(
            NoteCodes.ChatHandedOver, $"The conversation went on with {agent}, in conversation {session}.",
            ("agent", agent), ("session", session));

    /// <summary>A quest's session end, stopped so its quest goes on with another agent (MIXAGENT1a-ii): which agent. Never an account's name or a path.</summary>
    public static Noted QuestHandedOver(string agent) =>
        Noted.Of(NoteCodes.EndedHandedOver, $"Handed over to {agent}: its quest goes on with it, in the same tree.", ("agent", agent));

    /// <summary>The new conversation's line where Daoris's briefing did not reach its agent.</summary>
    public static Noted Unsaid() =>
        Noted.Of(NoteCodes.ChatBriefingUnsaid, "What Daoris kept of the earlier conversation did not reach the agent: tell it where you were.");

    /// <summary>The new conversation's first line: where it came from and why.</summary>
    public static Noted TakenOver(string agent, string session, string why) =>
        Noted.Of(
            NoteCodes.ChatTakenOver, $"The conversation went on from {agent} conversation {session}, handed over because {HandOverWhy.Noted(why)}.",
            ("agent", agent), ("session", session), ("why", why));

    /// <summary>
    /// A move to a new part (SESSCTX1c, D189 point 8; the session-context design §7), the line on the part it left: the session it
    /// went on in, the agent and account that one runs on, why by the link's code, the context this part stood at as it moved (H,
    /// its high-water in tokens) and the characters of the brief the new part was handed (B). The account by its id, as
    /// <see cref="Moved"/>'s are: a session's events stay on this machine. A value not known is left out, and its words with it.
    /// </summary>
    public static Noted MovedOn(string session, string agent, string? account, string why, long? context, int brief) =>
        Noted.Of(
            NoteCodes.MovedOn,
            $"Went on in session {session} with {agent}{As(account)}, because {HandOverWhy.Noted(why)}: "
            + (context is { } tokens
                ? $"its context stood at {tokens} tokens, and the new session was handed Daoris's brief of it, {brief} characters."
                : $"the new session was handed Daoris's brief of it, {brief} characters."),
            ("session", session), ("agent", agent), ("account", account), ("why", why), ("tokens", context), ("count", brief));

    /// <summary>The same move's line on the new part (<see cref="MovedOn"/>): the session it continues, the agent and account that one ran on.</summary>
    public static Noted MovedFrom(string session, string agent, string? account, string why, long? context, int brief) =>
        Noted.Of(
            NoteCodes.MovedFrom,
            $"Continues session {session}, which ran on {agent}{As(account)}, because {HandOverWhy.Noted(why)}: "
            + (context is { } tokens
                ? $"its context stood at {tokens} tokens, and it was handed Daoris's brief of it, {brief} characters."
                : $"it was handed Daoris's brief of it, {brief} characters."),
            ("session", session), ("agent", agent), ("account", account), ("why", why), ("tokens", context), ("count", brief));

    private static string As(string? account) => account is { Length: > 0 } named ? $" as `{named}`" : "";

    /// <summary>
    /// A conversation moved by the usage curve (SWITCH1c-i; the switch design §4), its last line: the account it went on on, and
    /// how far over its curve the account it left was. It names accounts, as the start's <c>opening.*</c> lines do: a
    /// conversation's record stays on this machine (<see cref="NotePart"/>). Its new conversation's first line is
    /// <see cref="MovedFrom"/>; SWITCH1c-ii writes both.
    /// </summary>
    /// <param name="from">The account it ran on.</param>
    /// <param name="move">What <see cref="SwitchDecision"/> said: the account it went to, and the window over its curve.</param>
    public static Noted Moved(string from, SwitchChoice.MoveTo move)
    {
        var (used, curve) = (RotationWords.Whole(move.Used), RotationWords.Points(move.Curve));
        return Noted.Of(
            NoteCodes.ChatMoved,
            $"Moved to `{move.Account}` at the end of its turn: `{from}` had used {used}% of its {move.Window} limit, over its curve's {curve}%; `{move.Account}` has room.",
            ("to", move.Account), ("from", from), ("window", move.Window), ("used", used), ("curve", curve));
    }

    /// <summary>A conversation moved by the usage curve, its new conversation's first line: the account it continues from, and why (<see cref="Moved"/>).</summary>
    public static Noted MovedFrom(string from, SwitchChoice.MoveTo move)
    {
        var (used, curve) = (RotationWords.Whole(move.Used), RotationWords.Points(move.Curve));
        return Noted.Of(
            NoteCodes.ChatMovedFrom,
            $"Continues from `{from}`, which had used {used}% of its {move.Window} limit, over its curve's {curve}%.",
            ("from", from), ("window", move.Window), ("used", used), ("curve", curve));
    }
}

/// <summary>What a conversation's hand-over came to (MIXAGENT1a-i): the new conversation, or why none, as a start-from's answer.</summary>
/// <param name="SessionId">The new conversation, where it was taken at once; null where the box waits or nothing was done.</param>
/// <param name="Kept">The ask stands: taken now (<paramref name="SessionId"/>) or kept for the conversation's turn to end.</param>
/// <param name="Why">Why nothing was done, by a code of <see cref="WordsNever"/> or <see cref="HandOver"/>; null otherwise.</param>
public sealed record HandedOver(string? SessionId, bool Kept, string? Why, string Message);
