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

    /// <summary>The landing and what follows it to the line.</summary>
    Landing,

    /// <summary>Other work and the graph's own wiring.</summary>
    Others,
}
