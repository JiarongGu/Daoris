namespace Daoris.Driver;

/// <summary>One thing the performer did, on one unit's node.</summary>
public sealed record WorkPerformed(string Unit, string Node, string What, string? Code = null)
{
    /// <summary>What a refusal says beside its code: for <see cref="StepTenureFiles.EndedEpoch"/>, who holds the step now and since when.</summary>
    public string? Detail { get; init; }
}
