namespace Daoris.Knowledge;

/// <summary>A plan revision as its writer gives it: the parts and edges, and the revision it read before writing.</summary>
/// <param name="Ask">The ask the plan is for.</param>
/// <param name="Parts">The parts, each published as a quest under <c>(ask, part)</c>.</param>
/// <param name="Edges">The orders between them.</param>
/// <param name="Why">One paragraph: why these parts, and what changed since the last revision.</param>
public sealed record TeamSave(string Ask, IReadOnlyList<TeamPart> Parts, IReadOnlyList<TeamEdge> Edges, string Why)
{
    /// <summary>The part this plan splits, where a worker writes its own split; null for the ask's own plan.</summary>
    public string? Scope { get; init; }

    /// <summary>The revision the writer read: 0 for a plan not yet written. A write over a newer revision is refused, so it reads again.</summary>
    public int Expect { get; init; }

    /// <summary>The session writing, or null for a person's hand.</summary>
    public string? By { get; init; }

    /// <summary>The line's commit the plan was read at.</summary>
    public string? Base { get; init; }
}
