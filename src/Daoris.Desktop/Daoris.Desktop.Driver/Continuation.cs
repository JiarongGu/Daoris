namespace Daoris.Driver;

/// <summary>Why an answer was carried on in a new session rather than resuming its own: a code and the note's one line.</summary>
/// <param name="Code">An identifier from <see cref="ContinueWhy"/>: what the machine log writes as <c>why</c>.</param>
/// <param name="Sentence">The note's line. It travels, so it never names an account nor quotes the agent (D131 §2).</param>
public sealed record ContinueReason(string Code, string Sentence);

/// <summary>
/// The reasons an answered park is carried on in a new session (ANSWER1a, D131 §2), each a code the machine log writes and
/// a line the notes say. The first five are judged before a spawn; the last four are the wire's.
/// </summary>
public static class ContinueWhy
{
    /// <summary>The start runs on another account than the park did: a limit cools it, a rotation or D130's list chose another.</summary>
    public const string Account = "account";

    /// <summary>The person changed the adapter since.</summary>
    public const string Adapter = "adapter";

    /// <summary>No conversation id was kept: a park from before this build, or a wire that never said one.</summary>
    public const string Unkept = "unkept";

    /// <summary>Its tree is gone.</summary>
    public const string Tree = "tree";

    /// <summary>The adapter has no resume on its door.</summary>
    public const string Unable = "unable";

    /// <summary>The agent advertised neither <c>session/resume</c> nor <c>loadSession</c>.</summary>
    public const string Offered = "offered";

    /// <summary>The agent no longer has the conversation: <c>resource_not_found</c>, or the native door ended before opening it.</summary>
    public const string Gone = "gone";

    /// <summary>The agent refused the resume otherwise, or the resumed run could not start.</summary>
    public const string Refused = "refused";

    /// <summary>The record had already ended as the answer reached it: a service from before ANSWER1b.</summary>
    public const string Ended = "ended";

    /// <summary>A reason whose line is fixed: every code but <see cref="Adapter"/> and <see cref="Unable"/>, which name adapters.</summary>
    public static ContinueReason Of(string code) => new(code, code switch
    {
        Account => "its conversation stays with the account it ran on, and this start runs on another",
        Unkept => "Daoris kept no id for its conversation",
        Tree => "its tree is gone",
        Offered => "the agent offers no way to resume a conversation",
        Gone => "the agent no longer has its conversation",
        Refused => "its conversation could not be resumed",
        Ended => "its record had already ended",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "a reason whose line names an adapter is made by its own method"),
    });

    /// <summary>The adapter changed between the park and the answer.</summary>
    public static ContinueReason AdapterChanged(string ranOn, string startsOn) =>
        new(Adapter, $"it ran on `{ranOn}`, and starts here now run on `{startsOn}`");

    /// <summary>The adapter's door has no resume.</summary>
    public static ContinueReason CannotResume(string adapter) => new(Unable, $"`{adapter}` cannot resume a conversation");
}

/// <summary>
/// Whether an answered park resumes its own harness conversation (ANSWER1a, D131 §1–§2), judged before anything is
/// spawned. Pure but for one look at whether the park's tree still stands.
/// </summary>
/// <remarks>
/// <para><b>The same account, always.</b> The harness keeps a conversation in the configuration home of the account it
/// ran on, so another account would not find it, and a record names one account (D125 §7). A limit cooling the park's
/// account, a rotation and D130's list each land a start on another account, and each is a carry-on.</para>
///
/// <para><b>The order is the record first, then what the start runs as, then what is kept, then the door</b>, so the
/// line said is the one a person can act on first: an ended record before an account, an account before an id.</para>
/// </remarks>
public static class Continuations
{
    /// <summary>Null where the park resumes; else the first reason it does not.</summary>
    /// <param name="park">The park as the last run read it: its state, adapter, account and tree.</param>
    /// <param name="adapter">The adapter this machine's starts ride now.</param>
    /// <param name="doorResumes">
    /// Whether that adapter's door can resume at all: the protocol door's agent decides on its wire, so true for it;
    /// the native door only where its adapter knows the harness's own resume (<see cref="ISessionAdapter.Resumes"/>).
    /// </param>
    /// <param name="profile">The account the start would run on, as the selection resolved it; null for the tool's own home.</param>
    /// <param name="kept">The conversation id kept for the park, or null.</param>
    public static ContinueReason? Judge(
        PriorSession park, string adapter, bool doorResumes, string? profile, HarnessConversation? kept)
    {
        if (!park.AnsweredPark) return ContinueWhy.Of(ContinueWhy.Ended);

        var ranOn = park.Adapter ?? kept?.Adapter;
        if (ranOn is { Length: > 0 } && !string.Equals(ranOn, adapter, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.AdapterChanged(ranOn, adapter);
        }

        if (!string.Equals(park.Profile ?? "", profile ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Account);
        }

        if (park.Tree is not { Length: > 0 } tree || !Directory.Exists(tree)) return ContinueWhy.Of(ContinueWhy.Tree);

        if (kept is null || !string.Equals(kept.Adapter, adapter, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Unkept);
        }

        return doorResumes ? null : ContinueWhy.CannotResume(adapter);
    }

    /// <summary>What the record says while its resumed conversation runs: the conclusion's note replaces it.</summary>
    public const string Working = "resumes its own conversation with your answer, in the tree it worked in.";

    /// <summary>
    /// The resumed run's first line in its record (D131 §1): the door it resumed on, and the harness's version where it
    /// moved since the record opened, since the record names the version it opened on and a newer one is never refused.
    /// </summary>
    public static string Opening(string adapter, string? now, string? then) =>
        now is { Length: > 0 } && then is { Length: > 0 } && !string.Equals(now, then, StringComparison.Ordinal)
            ? $"— your answer is the next turn of its own conversation, resumed on `{adapter}` {now}; it opened on {then}."
            : $"— your answer is the next turn of its own conversation, resumed on `{adapter}`.";

    /// <summary>The note a fallback ends the park with: what it asked, as its record said, and why the answer went to a new session.</summary>
    public static string EndedNote(PriorSession park, ContinueReason why) =>
        $"{park.Note ?? "It stopped to ask you."}\n\nCarried on in a new session, because {why.Sentence}.";

    /// <summary>The sentence the carrying-on session's note ends with (D131 §2).</summary>
    public static string CarriedOn(ContinueReason why) => $" A new session, because {why.Sentence}.";

    /// <summary>
    /// <c>session.answered</c> (D131 §2, D94 §4): once per answer the driver takes up, whether its own conversation resumed
    /// and, where not, why by code. Never the answer, the agent's words or an account. Written on the account lines'
    /// channel, which writes any catalogued line as it is given.
    /// </summary>
    /// <param name="session">The parked session the person answered.</param>
    /// <param name="why">Null where it resumed.</param>
    public static AccountLine Answered(string session, string adapter, ContinueReason? why) =>
        new("session.answered", [("session", session), ("adapter", adapter), ("resumed", why is null), ("why", why?.Code)]);
}
