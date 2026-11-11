namespace Daoris.Driver;

/// <summary>
/// The kinds of decision a supervisor answers, each waiting somewhere for an allow or a choice, and each kind's acts, which a supervisor's
/// <c>authority</c> grants as <c>kind.act</c>. A closed vocabulary of the build, as the works are: a supervisor's <c>decides</c> names
/// kinds of it, and one naming none is its scope's generic supervisor, answering what no supervisor of a kind takes there.
/// </summary>
public static class SupervisorKinds
{
    public const string Question = "question";
    public const string Course = "course";
    public const string Limit = "limit";
    public const string Allow = "allow";
    public const string Files = "files";
    public const string Landing = "landing";

    /// <summary>Ask Daoris's acts, read after its turns, observed and never decided; not the working role <c>ask-daoris</c>.</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>The acts a holder's routes make today: an answer to a question, a line's go-on, and a move to another choice.</summary>
    public const string Answer = "answer";
    public const string GoOn = "go-on";
    public const string Move = "move";
    public const string Report = "report";

    private static readonly IReadOnlyDictionary<string, string[]> Named = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Question] = [Answer],
        [Course] = [GoOn],
        [Limit] = [Move, "wait"],
        [Allow] = ["once", "reject", "standing", "steward"],
        [Files] = ["allow-run", "allow-copy", "allow-share"],
        [Landing] = ["accept", "approve", "reject"],
        [AskDaoris] = [Report],
    };

    /// <summary>Every kind, in the editor's order.</summary>
    public static IReadOnlyList<string> All { get; } = [Question, Course, Limit, Allow, Files, Landing, AskDaoris];

    public static bool MachineOnly(string kind) => kind == AskDaoris;

    /// <summary>Every kind's acts, qualified, in the kinds' order: the supervisor's <c>authority</c> vocabulary.</summary>
    public static IReadOnlyList<string> Acts { get; } = [.. All.SelectMany(ActsOf)];

    /// <summary>
    /// What the machine's seeded supervisor grants: the three decisions a terminal holder makes with no other grant, so a home behaves as
    /// before and an agent given the role answers what a terminal would. Every other act is off.
    /// </summary>
    public static IReadOnlyList<string> Seeded { get; } = [Qualified(Question, Answer), Qualified(Course, GoOn), Qualified(Limit, Move)];

    /// <summary>The acts of <paramref name="kind"/>, qualified; none for a kind this build does not know.</summary>
    public static IReadOnlyList<string> ActsOf(string kind) =>
        Named.TryGetValue(kind, out var acts) ? [.. acts.Select(act => Qualified(kind, act))] : [];

    public static bool IsPoint(string kind) => kind is Course or Limit;

    /// <summary>Whether <paramref name="kind"/> is one of the vocabulary.</summary>
    public static bool Known(string kind) => Named.ContainsKey(kind);

    public static string Qualified(string kind, string act) => $"{kind}.{act}";

    /// <summary>The kind a qualified act belongs to; null where it is none of the vocabulary's.</summary>
    public static string? KindOf(string act) =>
        Acts.Contains(act, StringComparer.Ordinal) ? act[..act.IndexOf('.', StringComparison.Ordinal)] : null;
}
