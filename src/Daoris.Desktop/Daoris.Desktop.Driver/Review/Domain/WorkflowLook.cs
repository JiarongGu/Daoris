namespace Daoris.Driver;

/// <summary>
/// Rows 5 and 6 of the review level's table as a named version says them: the environment its look step reads the
/// work in, or none where it has no look; and why its look cannot start, where it names what the repository does not declare.
/// </summary>
/// <param name="Environment">The environment the look is in; null where the version has no look, or its look cannot start for want of one.</param>
/// <param name="Cannot">Why the look cannot start, a sentence naming the Setup row that declares what it needs; else null.</param>
public sealed record WorkflowLook(string? Environment, string? Cannot)
{
    /// <summary>The look node's id in the graph the run is bound to, where the look is a node; the one the person's skip names.</summary>
    public string? Node { get; init; }
}
