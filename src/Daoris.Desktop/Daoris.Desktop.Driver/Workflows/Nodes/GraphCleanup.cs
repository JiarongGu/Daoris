namespace Daoris.Driver;

/// <summary>A graph's cleanup (§E7): when it goes, and for an end after the line, the bound in days (1-90, §C2).</summary>
public sealed record GraphCleanup(string Until, int? Days = null)
{
    /// <summary>At <i>held</i>, with no bound: today's, and what a graph naming none takes.</summary>
    public static readonly GraphCleanup Held = new(GraphCleanupUntil.Held);
}
