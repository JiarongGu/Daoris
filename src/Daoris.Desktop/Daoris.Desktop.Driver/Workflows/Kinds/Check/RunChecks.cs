namespace Daoris.Driver;

/// <summary>
/// What a <c>check</c> of a graph run verifies, an ask's delivery run or a chain's: its repository at the branch tip the attempt is bound to,
/// under the declaration accepted there. The check's <c>reached</c> choice is carried with it.
/// </summary>
public static class RunChecks
{
    /// <summary>The send for <paramref name="attempt"/> of the run <paramref name="unit"/> names; null where the run is not bound here or its repository has no checkout here.</summary>
    public static VerifySend? SendOf(string home, string unit, NodeAttempt attempt, NodeDeclaration node)
    {
        var id = CheckExecution.RunOf(unit);
        if (!SafeNames.IsPlainId(id) || WorkflowRunBindings.Read(home, id) is not { } binding) return null;
        var root = GitFolders.Read(home).FirstOrDefault(checkout => string.Equals(checkout.Repository, binding.Repository, StringComparison.OrdinalIgnoreCase)).Root;
        if (root is null) return null;
        var accepted = IntegrationAcceptance.Of(DriverConfig.Load(DriverConfig.ResolvePath(home)), binding.Repository);
        return new VerifySend(binding.Run, attempt.Call.Invocation, binding.Repository, root, attempt.Call.SubjectRevision, accepted)
        {
            Gates = CheckNode.GatesOf(node),
            Reached = CheckNode.ReachedChoice(node),
            Bound = WorkPerformer.Timeout(node),
        };
    }
}
