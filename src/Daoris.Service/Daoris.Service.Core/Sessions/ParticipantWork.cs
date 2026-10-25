namespace Daoris.Knowledge;

/// <summary>
/// The participant's work (FOUNDATION1c, D212; the foundation design §2.2): the capability a record was opened for, one closed code read
/// from the marks the record carries by one rule, never stored. The service answers it as <c>work</c> to a caller on this machine
/// (<c>ToSession</c>), never on the wire, and the page draws a record by it where its host sends it.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> (<c>.claude/knowledge/twins.md</c>): the driver's <c>ParticipantWork</c> reads the same rule from its own record
/// view (<c>WorkMarks</c>), the page's <c>identity.ts</c> draws by what a host answers, and the three share no code. The file between them
/// is <c>fixtures/participant-work.json</c> among the service's tests, which each side's tests hold row for row, with every place a
/// reader of the same marks answered otherwise before this rule, named and decided there.</para>
/// <para><b>The one rule</b>, the first that holds: a mirror is another machine's, with no work here (null); <c>opinion</c> is
/// <see cref="Review"/>; <c>serves</c> with <c>piece</c> is <see cref="Partner"/>; <c>serves</c> alone is <see cref="Help"/>; <c>ask</c> is
/// <see cref="Intake"/>; the Ask Daoris repository is <see cref="AskDaoris"/>; a chat is <see cref="Chat"/>; otherwise <see cref="Build"/>. A
/// co-worker is a relation, <c>serves</c>, never a work of its own (D203 point 1): a partner's marks are a helper's with a piece. An
/// empty mark is none, as an absent one is.</para>
/// <para><see cref="Design"/> and <see cref="Brainstorm"/> are in the closed set and read from no marks: a quest's session of design or
/// decision work is built, admitted and read as <see cref="Build"/>, and a brainstorm's pass has no record.</para>
/// </remarks>
public static class ParticipantWork
{
    /// <summary>A driven quest's session, and every record no other mark names.</summary>
    public const string Build = "build";

    /// <summary>A driven quest's session of design or decision work: admitted and called exactly as <see cref="Build"/>, read as it from the marks.</summary>
    public const string Design = "design";

    /// <summary>A conversation the person holds.</summary>
    public const string Chat = "chat";

    /// <summary>Ask Daoris's conversation, told by the repository it is recorded in (<see cref="SessionLedger.HelpRepository"/>).</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>An ask's intake, told by the ask it answers.</summary>
    public const string Intake = "intake";

    /// <summary>A helper, told by the session it serves.</summary>
    public const string Help = "help";

    /// <summary>A partner, told by the session it serves and the piece it writes.</summary>
    public const string Partner = "partner";

    /// <summary>A second opinion's reviewer, told by the opinion it reads for.</summary>
    public const string Review = "review";

    /// <summary>A brainstorm's pass, which keeps no record.</summary>
    public const string Brainstorm = "brainstorm";

    /// <summary>Every work, the closed set.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Build, Design, Chat, AskDaoris, Intake, Help, Partner, Review, Brainstorm];

    /// <summary>
    /// The work <paramref name="session"/> was opened for, from its marks; null where it has none here, another machine's record.
    /// </summary>
    public static string? Of(Session session)
    {
        if (session.Origin is not null) return null;
        if (Marked(session.Opinion)) return Review;
        if (Marked(session.Serves)) return Marked(session.Piece) ? Partner : Help;
        if (Marked(session.Ask)) return Intake;
        if (SessionLedger.IsHelp(session)) return AskDaoris;
        return session.Kind == SessionKind.Chat ? Chat : Build;
    }

    private static bool Marked(string? mark) => !string.IsNullOrEmpty(mark);
}
