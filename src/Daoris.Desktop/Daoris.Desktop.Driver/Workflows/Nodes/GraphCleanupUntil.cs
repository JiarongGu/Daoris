namespace Daoris.Driver;


/// <summary>When a graph's cleanup goes (§C3's <c>wait</c>, the graph's <c>cleanup</c>, §E7).</summary>
public static class GraphCleanupUntil
{
    /// <summary>At <i>held</i>: the line holds the work. The default, today's.</summary>
    public const string Held = "held";

    /// <summary>At the graph's end, for an end after the line, with an optional bound in days.</summary>
    public const string End = "end";
}
