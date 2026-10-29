namespace Daoris.Driver;

/// <summary>
/// D154's rows 5 and 6 as a named version says them (WORKFLOW1f, the workflow design §4.6): the environment its look step reads the
/// work in, or none where it has no look; and why its look cannot start, where it names what the repository does not declare.
/// </summary>
/// <param name="Environment">The environment the look is in; null where the version has no look, or its look cannot start for want of one.</param>
/// <param name="Cannot">Why the look cannot start (§3.7), a sentence naming the Setup row that declares what it needs; else null.</param>
public sealed record WorkflowLook(string? Environment, string? Cannot);
