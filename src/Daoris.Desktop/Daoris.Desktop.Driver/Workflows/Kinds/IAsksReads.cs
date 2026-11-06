namespace Daoris.Driver;

/// <summary>What a pass reads for a node beyond its record, once the node waits active or unknown.</summary>
[Flags]
public enum PassRead
{
    None = 0,

    /// <summary>The opinion and review gates of the branch, read at its tip.</summary>
    Gates = 1,

    /// <summary>What the line holds of the branch at its tip.</summary>
    Content = 2,

    /// <summary>The branch's remote-tracking ref.</summary>
    Remote = 4,
}

/// <summary>A kind whose node waits on a read the pass makes for it (D247 §3.3), so the pass asks it of the unit and names no kind.</summary>
public interface IAsksReads
{
    /// <summary>The reads its node waits on.</summary>
    PassRead Asks { get; }
}

/// <summary>The kind whose reach is the landing the nodes after it bind to: the pass finds the landing through it.</summary>
internal interface IMakesLanding;
