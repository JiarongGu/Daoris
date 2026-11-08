namespace Daoris.Driver;

/// <summary>
/// A node kind's declaration: what the save, the editor, a run's binding, the run view and the terminal read of a node of this kind,
/// pure and built with no wiring. Registered with <see cref="NodeKindAttribute"/>, and found by <see cref="NodeKit.Of"/>.
/// </summary>
public interface INodeKind
{
    /// <summary>Who may report each outcome of <paramref name="node"/>.</summary>
    NodeAuthority Authority(NodeDeclaration node);

    /// <summary>
    /// The subject a run binds <paramref name="node"/> to, <paramref name="afterHanded"/> being the nodes a wire path
    /// leads to from the landing (<see cref="RunSubjects.AfterHanded"/>).
    /// </summary>
    RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded);

    /// <summary>The options a node of this kind takes, with the choices the code holds for them, in the order an editor lists them.</summary>
    IReadOnlyList<NodeOptionField> Options { get; }

    /// <summary>
    /// The first of the kind's own checks <paramref name="node"/>'s declaration breaks, by its field, or null; the checks every declaration is
    /// held to are <see cref="NodeDeclarations.Problem(NodeDeclaration)"/>'s.
    /// </summary>
    string? Problem(NodeDeclaration node);

    /// <summary>The save's refusal of <paramref name="field"/>, which <see cref="Problem"/> named, where the kind says it in words and a code of its own; null for the generic one.</summary>
    WireProblem? DeclarationProblem(NodeDeclaration node, string field) => null;

    /// <summary>Why a node of it is never a graph's end, read after <c>which is never an end:</c>.</summary>
    string NeverEndBecause => "it is reached on a claim, and an end on evidence.";

    /// <summary>
    /// What the save refuses of where <paramref name="node"/> stands in <paramref name="graph"/> by its kind's own rules, each against the node it is
    /// about; the rules every graph is held to are <see cref="NodeGraphs.Problems"/>'.
    /// </summary>
    IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node);

    /// <summary>Its part on the run view: the step it is drawn as, who takes part and their press, and what a version's person part reads it as.</summary>
    NodePart Part(NodeDeclaration node);

    /// <summary>
    /// How the editor offers it:<see cref="PaletteKind.Alone"/>, a step to add; or <c>tail:&lt;method&gt;</c>, a step a <c>land</c>
    /// of that method brings with it.
    /// </summary>
    string Offered { get; }

    /// <summary>The works a role must take to take a node of this kind, the step's own first; none where no agent session takes it.</summary>
    IReadOnlyList<string> RoleTakes { get; }

    /// <summary>The schema a <c>data</c> wire takes from its evidence port where its declaration names none: an observed built-in's record; else null.</summary>
    string? Evidence { get; }

    /// <summary>Its title as <c>daoris workflow show</c> names it, in English; null where its step is named by its id.</summary>
    string? Title { get; }

    /// <summary>How the chart draws it.</summary>
    NodeLook Look { get; }

    /// <summary>Whether it reads the landing record: none is reached before a landing made one, so a way to it passes one.</summary>
    bool ReadsTheLanding { get; }

    /// <summary>Whether it is a gate of the protected order before the landing, which <see cref="ProtectedOrder"/> holds.</summary>
    LandingGuard Guard { get; }

    /// <summary>Whether an end of it is at or before the line: a cleanup waits for no such end.</summary>
    bool EndsByTheLine { get; }

    /// <summary>Whether a run waiting on it is held for the person alone, as a go-ahead holds it: no supervisor decides a step of such a run.</summary>
    bool HoldsRun { get; }

    /// <summary>The acts a landing supervisor may make on <paramref name="node"/> while it waits for the person's press, unqualified; none for a kind it never decides.</summary>
    IReadOnlyList<string> LandingActs(NodeDeclaration node);
}
