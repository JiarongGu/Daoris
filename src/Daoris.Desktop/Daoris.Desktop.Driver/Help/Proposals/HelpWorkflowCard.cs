using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A workflow card's data (WORKFLOW1h-b, the workflow design §8.2), as the page's <c>HelpWorkflowShown</c> takes it: the workflow
/// and its name, the version it is made on (null for a new one) and the one it becomes, the scope, the steps' diff, the person's
/// part and each outward act as <see cref="WorkflowDiff"/> says them, what it needs first, what it applies to, and whether the
/// scope also chooses it. Lines are the driver's English; the page words them (WORKFLOW1h-d).
/// </summary>
/// <param name="Needs">What is missing; while any is, the card is a draft and offers no Apply.</param>
public sealed record HelpWorkflowCard(
    string Id, string Name, int? Base, int Version, string Scope, IReadOnlyList<string> Diff, IReadOnlyList<string> YourPart,
    IReadOnlyList<string> Outward, IReadOnlyList<string> Needs, string AppliesTo, bool Choose)
{
    /// <summary>Whether the scope is a <c>repository</c> or a <c>workspace</c>.</summary>
    public string ScopeKind { get; init; } = "repository";

    /// <summary>The steps Apply saves, as the proposal carried them; null where the version says what the newest says already.</summary>
    public JsonNode? Steps { get; init; }

    /// <summary>The graph Apply saves (FABRIC-PIPELINE1i), as the proposal carried it; null where it carries steps or saves nothing.</summary>
    public JsonNode? Graph { get; init; }

    /// <summary>The choice Apply makes; null where none is asked, or it is made already.</summary>
    public WorkflowUse? Use { get; init; }

    /// <summary>The route the card chooses for (FABRIC-PIPELINE1i): <c>default</c>, <c>landed</c> or <c>kind:&lt;id&gt;</c>; null where none.</summary>
    public string? Route { get; init; }

    /// <summary>
    /// The repositories a chosen graph integrates its landing in with no gates (INTEGRATE1r part r3; the integration design §17.7, F42), by name,
    /// so the page words the line in its own language; the plan's text says the same sentence for the terminal. Empty where none, or where
    /// the card saves a version.
    /// </summary>
    public IReadOnlyList<string> NoGates { get; init; } = [];

    /// <summary>Whether the workflow is a verification pipeline, or is made one by the save (the pipeline design §2.1).</summary>
    public bool Verifies { get; init; }

    /// <summary>
    /// What the change asks less of the person (D157 point 10, the pipeline design §1.3), each said as what it does; empty where it
    /// lowers nothing. An agent's change that lowers anything is applied only by the person's press, as every card is.
    /// </summary>
    public IReadOnlyList<string> Lowers { get; init; } = [];

    /// <summary>
    /// The fingerprint of the draft a <c>draft</c> card changes (FLOWCHART2e), the base its changes are against; null for every other card,
    /// which saves or chooses. A draft card saves no version, so its <see cref="Version"/> is 0.
    /// </summary>
    public string? Draft { get; init; }

    /// <summary>A draft card's changes against its draft, in <see cref="WorkflowGraphDiff"/>'s order, each with what it needs; empty for every other.</summary>
    public IReadOnlyList<GraphChange> Changes { get; init; } = [];

    /// <summary>What a save of a draft card's graph would refuse, each at its step or way: the card's to put right, never a refusal of it.</summary>
    public IReadOnlyList<WireProblem> Problems { get; init; } = [];

    /// <summary>
    /// A draft card's candidate graph, whole, as the proposal carried it, for the editor to take changes from: never <see cref="Graph"/>,
    /// which a save takes, so a draft card never saves. Null for every other card.
    /// </summary>
    public JsonNode? Proposed { get; init; }

    /// <summary>Whether Apply saves a version.</summary>
    public bool Saves => Steps is not null || Graph is not null;
}
