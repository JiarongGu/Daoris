namespace Daoris.Driver;

/// <summary>
/// A check's attempt held unknown for the person (FABRIC-PIPELINE1d): its unit and attempt, for their word on it (<c>WORK_HELD</c>), and the
/// <see cref="HeldResolutions.Check"/> words they may say, none once their stop is kept as the <see cref="Resolution"/>.
/// </summary>
public sealed record WorkflowRunHeld(string Unit, int Attempt, IReadOnlyList<string> Words)
{
    /// <summary>The person's word kept on it: <c>stop</c>, shown as <i>Not now</i>.</summary>
    public string? Resolution { get; init; }
}
