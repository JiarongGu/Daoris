namespace Daoris.Driver;

/// <summary>
/// What the review's gate reads of the process its run follows beyond what the workplace reads (<see cref="IWorkplaceProcess"/>): the
/// review rule as declared and the named version's look (WORKFLOW1f). The workflows' <c>WorkflowProcess</c> implements this directly,
/// so the gate names no more of the process than it reads.
/// </summary>
public interface IReviewedProcess : IWorkplaceProcess
{
    /// <summary>The review rule as declared: its environments, which the chain's and the ask's <c>on</c> reads (§2.3).</summary>
    ResolvedReview? Review { get; }

    /// <summary>Rows 5 and 6 of D154's table as the version says them; null under Current, where the rule says them, as today.</summary>
    WorkflowLook? Look { get; }
}

/// <summary>
/// What a session's checks and its long calls' bound read of the process its run follows (FABRIC-PIPELINE1b): whether a named workflow
/// governs it, a graph version's <c>claimed</c> options and a steps version's <c>work</c> step's <c>checks</c>. The workflows'
/// <c>WorkflowProcess</c> implements this directly.
/// </summary>
public interface ICheckedProcess : IWorkplaceProcess
{
    /// <summary>The options of the bound graph version's <c>claimed</c> node, each its JSON text as written; null where the process has none.</summary>
    IReadOnlyDictionary<string, string>? Claimed { get; }

    /// <summary>The <c>checks</c> field of a steps version's <c>work</c> step, as written; null where it sets none or the version has no such step.</summary>
    string? WorkChecks { get; }
}

/// <summary>
/// The run's own part of the landing gate as the review's gate reads it (WORKFLOW1f): whether it holds the work, what it says and the line
/// the machine log keeps of a landing it held. The workflows' <c>WorkflowGateState</c> implements this directly.
/// </summary>
public interface IWorkflowGatePart
{
    /// <summary>The state the part stands in, one of the workflows' gate states.</summary>
    string State { get; }

    /// <summary>Whether the part has anything to say: it follows a named workflow, or could not tell whether one does.</summary>
    bool Speaks { get; }

    /// <summary>Whether the run's own part lets the work on to the second opinion and the look.</summary>
    bool LetsGo { get; }

    /// <summary>What it says, in the driver's words: the terminal's line and every door's refusal.</summary>
    string Says { get; }

    /// <summary>The process the part was read with.</summary>
    IWorkplaceProcess Process { get; }

    /// <summary>The machine log's line for a landing the part held, at <paramref name="door"/>.</summary>
    LandingLine Held(string session, string? repository, string? workspace, string door);
}

/// <summary>
/// The graph's part of the landing gate as the review's gate reads it (FABRIC-CUTOVER1d): whether it lets the work land, what it says,
/// whether it asks the loop's next pass and the line the machine log keeps of a landing it held. The workflows' <c>GraphGateState</c>
/// implements this directly.
/// </summary>
public interface IGraphGatePart
{
    /// <summary>The state the part stands in, one of the workflows' graph gate states.</summary>
    string State { get; }

    /// <summary>Whether the graph lets the work land.</summary>
    bool LetsGo { get; }

    /// <summary>What it says, in the driver's words.</summary>
    string Says { get; }

    /// <summary>Whether what holds the work asks a pass of the run's next look.</summary>
    bool AsksPass { get; }

    /// <summary>The machine log's line for a landing the part held, for the run <paramref name="process"/> follows, at <paramref name="door"/>.</summary>
    LandingLine Held(string session, string? repository, string? workspace, IWorkplaceProcess? process, string door);
}
