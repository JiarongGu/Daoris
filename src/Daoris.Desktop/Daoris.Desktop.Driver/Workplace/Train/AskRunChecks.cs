namespace Daoris.Driver;

/// <summary>What a <c>check</c> of an ask's delivery run verifies: its repository at the ask's branch tip the attempt is bound to, under the declaration accepted there.</summary>
public static class AskRunChecks
{
    /// <summary>The send for <paramref name="attempt"/> of the run <paramref name="unit"/> names; null where it is no ask's run, or its repository has no checkout here.</summary>
    public static VerifySend? SendOf(string home, string unit, NodeAttempt attempt, NodeDeclaration node)
    {
        var id = CheckExecution.RunOf(unit);
        if (!SafeNames.IsPlainId(id) || WorkflowRunBindings.Read(home, id) is not { } binding || !AskRunKey.Is(binding)) return null;
        var root = GitFolders.Read(home).FirstOrDefault(checkout => string.Equals(checkout.Repository, binding.Repository, StringComparison.OrdinalIgnoreCase)).Root;
        if (root is null) return null;
        var accepted = IntegrationAcceptance.Of(DriverConfig.Load(DriverConfig.ResolvePath(home)), binding.Repository);
        return new VerifySend(binding.Run, attempt.Call.Invocation, binding.Repository, root, attempt.Call.SubjectRevision, accepted)
        {
            Gates = CheckNode.GatesOf(node),
            Bound = WorkPerformer.Timeout(node),
        };
    }
}
