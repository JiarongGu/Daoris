namespace Daoris.Driver;

/// <summary>
/// A graph version with no <c>land</c> that ends at the person's review, read as a process: its work is done in its department's own
/// place and accepted there, so nothing lands it and a landing's press under it is refused, saying so; its gates are read as any graph's.
/// </summary>
public static class InPlaceProcess
{
    /// <summary>The words a landing's press under work in place is refused with.</summary>
    public const string Lands = "it lands nothing: its work is done in its department's own place, and your Accept at its review ends it.";

    public static WorkflowProcess Of(DriverConfig config, string repository, string? workspace, WorkflowRunBinding binding, GraphGateReading gates) =>
        WorkflowProcess.Of(config, repository, workspace, binding, gates, Landing.Merge) with { LandingCannot = Lands };
}
