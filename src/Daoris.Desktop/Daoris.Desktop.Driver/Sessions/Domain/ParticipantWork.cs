namespace Daoris.Driver;

/// <summary>
/// The marks a record carries that tell its work (FOUNDATION1c, D212; the foundation design §2.2): handed to <see cref="ParticipantWork.Of"/>
/// as a value, so the rule names no record type above it. Each caller builds it from its own view of the record
/// (the planner's <c>SessionView</c>, its <c>PriorSession</c>, the fields the service answers), and a mark that is empty is none, as an
/// absent one is.
/// </summary>
/// <param name="Mirror">The record is another machine's, a teammate's mirror: no work here.</param>
/// <param name="Kind"><c>driven</c> or <c>chat</c>; null from a host that answers none, read as driven work.</param>
/// <param name="Repository">Where it is recorded: a repository's name, <c>ask #a</c> for an intake, <c>daoris:help</c> for Ask Daoris.</param>
/// <param name="Ask">The ask an intake answers.</param>
/// <param name="Serves">The session a helper or a partner works for.</param>
/// <param name="Piece">The piece a partner writes, kept only beside <paramref name="Serves"/>.</param>
/// <param name="Opinion">The second opinion a reviewer reads for.</param>
public readonly record struct WorkMarks(
    bool Mirror, string? Kind, string? Repository, string? Ask, string? Serves, string? Piece, string? Opinion);

/// <summary>
/// The participant's work (FOUNDATION1c, D212; the foundation design §2.2): the capability a record was opened for, one closed code read
/// from its marks by one rule and never stored, so every reader that must tell the work reads it here.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> (<c>.claude/knowledge/twins.md</c>): the service's <c>ParticipantWork.Of</c> answers it as <c>work</c> on loopback
/// from its own record, and the page's <c>identity.ts</c> draws by what a host answers. The three share no code; the file between them is
/// <c>fixtures/participant-work.json</c> among the service's tests, which each side's tests hold row for row, every place a reader of the
/// same marks answered otherwise before the rule named and decided there.</para>
/// <para><b>The one rule</b>, the first that holds: a mirror is another machine's, with no work here (null); <c>opinion</c> is
/// <see cref="Review"/>; <c>serves</c> with <c>piece</c> is <see cref="Partner"/>; <c>serves</c> alone is <see cref="Help"/>; <c>ask</c> is
/// <see cref="Intake"/>; the Ask Daoris repository is <see cref="AskDaoris"/>; a chat is <see cref="Chat"/>; otherwise <see cref="Build"/>. A
/// co-worker is a relation, <c>serves</c>, never a work of its own (D203 point 1).</para>
/// <para><b>The cap's slot is a property of the work</b> (<see cref="HoldsSlot"/>): what the driver starts takes a place under the cap,
/// a conversation the person holds does not (CAPCOUNT1).</para>
/// </remarks>
public static class ParticipantWork
{
    /// <summary>A driven quest's session, and every record no other mark names.</summary>
    public const string Build = "build";

    /// <summary>A driven quest's session of design or decision work: admitted and called as <see cref="Build"/>, read as it from the marks.</summary>
    public const string Design = "design";

    /// <summary>A conversation the person holds.</summary>
    public const string Chat = "chat";

    /// <summary>Ask Daoris's conversation, told by the repository it is recorded in.</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>An ask's intake, told by the ask it answers.</summary>
    public const string Intake = "intake";

    /// <summary>A helper, told by the session it serves.</summary>
    public const string Help = "help";

    /// <summary>A partner, told by the session it serves and the piece it writes.</summary>
    public const string Partner = "partner";

    /// <summary>A second opinion's reviewer, told by the opinion it reads for.</summary>
    public const string Review = "review";

    /// <summary>A brainstorm's pass, which keeps no record: no marks read it.</summary>
    public const string Brainstorm = "brainstorm";

    /// <summary>Every work, the closed set.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Build, Design, Chat, AskDaoris, Intake, Help, Partner, Review, Brainstorm];

    // A twin of HelpRoom.Repository, which this module may not name (it sits above it) and the service's SessionLedger.HelpRepository:
    // each side's test holds the spelling, and the participant-work table's rows read it.
    internal const string HelpRepository = "daoris:help";

    /// <summary>The work <paramref name="marks"/> tell; null where there is none here, another machine's record.</summary>
    public static string? Of(WorkMarks marks)
    {
        if (marks.Mirror) return null;
        if (Marked(marks.Opinion)) return Review;
        if (Marked(marks.Serves)) return Marked(marks.Piece) ? Partner : Help;
        if (Marked(marks.Ask)) return Intake;
        if (string.Equals(marks.Repository, HelpRepository, StringComparison.Ordinal)) return AskDaoris;
        return string.Equals(marks.Kind, "chat", StringComparison.Ordinal) ? Chat : Build;
    }

    /// <summary>
    /// Whether the work takes a place under the machine's cap (CAPCOUNT1): <c>build</c>, <c>design</c>, <c>intake</c>, <c>help</c>,
    /// <c>partner</c> and <c>review</c> do, what the driver starts for itself; <c>chat</c>, <c>ask-daoris</c> and <c>brainstorm</c> do not,
    /// and a record with no work here holds no place.
    /// </summary>
    public static bool HoldsSlot(string? work) => work is Build or Design or Intake or Help or Partner or Review;

    private static bool Marked(string? mark) => !string.IsNullOrEmpty(mark);
}
