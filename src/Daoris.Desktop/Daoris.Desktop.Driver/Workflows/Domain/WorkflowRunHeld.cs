namespace Daoris.Driver;

/// <summary>
/// An attempt held for the person, for their word on it (<c>WORK_HELD</c>): a check's held unknown (<see cref="HeldResolutions.Check"/>) or
/// a failed plugin step's kept failure (<see cref="HeldResolutions.Failure"/>), none once their word is kept as the <see cref="Resolution"/>.
/// </summary>
public sealed record WorkflowRunHeld(string Unit, int Attempt, IReadOnlyList<string> Words)
{
    /// <summary>The person's word kept on it: <c>stop</c>, shown as <i>Not now</i>, or <c>again</c> until it is applied.</summary>
    public string? Resolution { get; init; }
}
