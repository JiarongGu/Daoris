namespace Daoris.Driver;

/// <summary>
/// What the workplace reads of the process a run follows at its gate: whether a named workflow governs the work, the landing it lands by,
/// why a named workflow's run or landing cannot start, the version that decided, and the second-opinion rule beside it. The workflows'
/// <c>WorkflowProcess</c> implements this directly, so the trees, the auto-lander and the end judgement name no more of the process than
/// they read; the workflows read the process and judge its gate through <see cref="IWorkplaceWorkflows"/> and the landing gate's port.
/// </summary>
public interface IWorkplaceProcess
{
    /// <summary>Whether a named workflow governs this work: its run was bound to one.</summary>
    bool Named { get; }

    /// <summary>How the work is said to follow it: <c>`docs-to-pr` v2</c>, or Current.</summary>
    string Name { get; }

    /// <summary>The landing rule the work lands by: the declared rule, or the version's landing step over it.</summary>
    Landing Landing { get; }

    /// <summary>Why the named workflow its run was bound to cannot be read here; else null.</summary>
    string? Problem { get; }

    /// <summary>Why it could not be read whether a named workflow governs this work; else null.</summary>
    string? Unread { get; }

    /// <summary>Why the version's landing step cannot start; else null.</summary>
    string? LandingCannot { get; }

    /// <summary>What the landing record keeps of it: the named version that decided; null under Current.</summary>
    LandingWorkflow? Record { get; }

    /// <summary>The run's binding's run, Current's or a named workflow's; null where it has none.</summary>
    string? BoundRun { get; }

    /// <summary>Which level chose the run's workflow, in words (<c>chosen by this repository for Documentation</c>).</summary>
    string Decided { get; }

    /// <summary>What a landing step that cannot start says: the process it follows, and why it cannot.</summary>
    string LandingSays { get; }

    /// <summary>The second-opinion rule the gate reads; null where none is set, or the version has no opinion step.</summary>
    ResolvedOpinion? Opinion { get; }

    /// <summary>Why the version's opinion step cannot start; else null.</summary>
    string? OpinionCannot { get; }

    /// <summary>
    /// The <c>request</c> the bound version's own <c>integrate</c> step names, which its landing records as the
    /// acceptance's provenance; null where the version holds no such step, and under Current, whose rule's <c>integrate</c> is read instead.
    /// </summary>
    string? IntegrateRequest => null;

    /// <summary>
    /// Whether the landing's start judges the quest's holds, the content proof and what the line holds by content itself: under Current, under
    /// a steps version and under a graph version that lands by a <c>handed</c>, where they are judged at the start as before; not where a graph
    /// version lands by a <c>land</c>, whose own steps judge them, or which holds none of them, and lands without.
    /// </summary>
    bool JudgesHolds => true;

    /// <summary>
    /// Whether the runtime performs this work's landing: a run bound to a readable graph version holding a <c>land</c>. Its
    /// doors write the review's word and wait, the look chooses nothing for it, and no door lands it in place; Current, a steps version and a
    /// <c>handed</c> version land through today's doors until the transitional paths end.
    /// </summary>
    bool LandsByRuntime => false;

    /// <summary>
    /// The bound graph version's <c>land</c> for <paramref name="subject"/>'s work, which a door's delivery is written from, as
    /// Current's is from its rule; null under Current, under a steps version and under a graph version that lands by a <c>handed</c>, which land
    /// through today's doors until they end.
    /// </summary>
    BoundLand? Land(LandingSubject subject) => null;
}

/// <summary>A bound graph version's <c>land</c>, as a door's delivery is written from it.</summary>
/// <param name="Method">Its method, written or derived from the landing step it stands for.</param>
/// <param name="Target">The line its <c>target</c> names; null lands into the tree's line.</param>
/// <param name="Squash">Whether it squashes the work into one commit: only a branch, a push and a pull request do.</param>
/// <param name="Message">Its <c>message</c> template rendered for the work; null where it names none, so its commits say today's words.</param>
/// <param name="Ticket">The ticket the work is for, where its message names one.</param>
/// <param name="Options">Its options as the bind resolved them, the JSON its authorization keeps.</param>
/// <param name="Graph">The digest of the version its run was bound to.</param>
/// <param name="Authority">The standing authority's scope an automatic delivery names: the workflow, its version and the repository.</param>
public sealed record BoundLand(
    string Method, string? Target, bool Squash, string? Message, string? Ticket, string Options, string Graph, string Authority);
