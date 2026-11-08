namespace Daoris.Driver;

/// <summary>Which branch a run's reads name for its work.</summary>
internal static class RunBranches
{
    /// <summary>
    /// The branch a run's work stands on: the one its tree's agent moved onto where the look learned it, else its session branch, which then
    /// does not move, so a read of it would never see the tree's work.
    /// </summary>
    public static string Standing(WorkUnit branch) => branch.Records.Branch?.Now ?? branch.Subject.Artifact ?? "";
}
