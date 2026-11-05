namespace Daoris.Driver;

/// <summary>
/// One row of the measurements table (COAGENT1 §3.5): a capability a person measured on one door at one effective configuration, with
/// its outcome and the evidence document that records it. A measurement admits only where it applies: a start at another version or
/// posture reads the capability as declared until it is measured again (COAGENT1 §3.1).
/// </summary>
/// <param name="Door">The door it was measured on.</param>
/// <param name="Capability">The capability, one of <see cref="SheetCapability"/>'s names.</param>
/// <param name="Outcome"><c>yes</c> or <c>no</c>.</param>
/// <param name="Agent">The agent's program's version, as it reported it.</param>
/// <param name="Adapter">The adapter's version, or null for a native door.</param>
/// <param name="Posture">The posture it ran under, in the agent's own words.</param>
/// <param name="Evidence">The evidence document's repository path.</param>
public sealed record Measurement(
    string Door, string Capability, string Outcome, string Agent, string? Adapter, string Posture, string Evidence)
{
    /// <summary>The configuration it applies to, as a sheet's row says it.</summary>
    public string Applies => $"agent {Agent}" + (Adapter is null ? "" : $", adapter {Adapter}") + $", posture {Posture}";

    /// <summary>
    /// The measurements this repository holds, each with its evidence document (a census holds each path to a document that exists).
    /// STEER1's protocol run on Claude Code: a second prompt reaching the turn at its next step, and the steering extension.
    /// </summary>
    public static IReadOnlyList<Measurement> Table { get; } =
    [
        new("claude-code-acp", SheetCapability.WordsAtNextStep, SheetRow.Yes, "2.1.284", "0.84.0", "default", "docs/2026-10-03-steer-evidence.md"),
        new("claude-code-acp", SheetCapability.Steering, SheetRow.Yes, "2.1.284", "0.84.0", "default", "docs/2026-10-03-steer-evidence.md"),
    ];
}
