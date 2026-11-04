using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One repository a choice reaches (the integration design §17.7): why its declarations cannot carry the version, and whether its line integrates with no gates.</summary>
public sealed record WorkflowReach(string Repository, IReadOnlyList<string> Cannots, bool NoGates)
{
    /// <summary>The saved workflow whose graph reaches the repository, where a place's reach names it.</summary>
    public string? Workflow { get; init; }

    /// <summary>The sentence the door says of it, or null where it reaches nothing worth saying.</summary>
    public string? Said => Cannots.Count > 0 ? $"`{Repository}`: cannot start — {string.Join(" ", Cannots)}"
        : NoGates ? IntegrateCarry.NoGatesSaid(Repository) : null;
}
