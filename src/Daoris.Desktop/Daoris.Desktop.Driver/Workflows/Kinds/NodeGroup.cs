namespace Daoris.Driver;

/// <summary>The groups the step picker offers the kinds in, in its order: the phases of a piece of work's way to the line.</summary>
public enum NodeGroup
{
    /// <summary>The agent's work.</summary>
    Work,

    /// <summary>What waits on the person: their Accept, look, go-ahead or task.</summary>
    You,

    /// <summary>What judges the work before it lands: the gates, the second opinion and the holds.</summary>
    Checks,

    /// <summary>What git does with the work on its way to the line: the rebase, its resolution, the merge, the push, the tidy and what watches them.</summary>
    Git,

    /// <summary>Other work and the graph's own wiring.</summary>
    Others,
}
