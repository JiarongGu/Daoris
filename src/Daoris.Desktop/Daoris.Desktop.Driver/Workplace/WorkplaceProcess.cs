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
}
