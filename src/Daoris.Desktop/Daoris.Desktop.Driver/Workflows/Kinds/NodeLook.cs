namespace Daoris.Driver;

/// <summary>How the chart draws a kind's node: the variant its box and glyph take.</summary>
public enum NodeLook
{
    Work,
    You,
    Check,
    Holds,
    Landing,
    Line,
    Other,

    /// <summary>A plugin's step; one declaring outcomes is drawn as a decision.</summary>
    Plugin,
}
