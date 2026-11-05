namespace Daoris.Driver;

/// <summary>
/// What a fresh home is given once (WORKFLOWCLEAN1 1a; the cleanup design §1.1): the copy of <c>merge-after-accept</c> saved, and
/// <c>defaultWorkflow</c> naming it, so a place follows a saved graph from its first start, and the supervisor's seeded rules. A home that
/// already has a <c>driver.json</c> is never seeded; it goes through the conversion, on the person's press.
/// </summary>
public static class HomeSeed
{
    /// <summary>
    /// Seed the home where <paramref name="configPath"/> is not there yet. The copy is written first and the file after, so a stop between
    /// them leaves a saved copy and no default, which the next start seeds again as a home without a file.
    /// </summary>
    /// <returns>Whether the home was seeded.</returns>
    public static bool Once(string configPath, string home)
    {
        if (File.Exists(configPath)) return false;
        try
        {
            var shipped = WorkflowDefaults.Of(SetupPresets.Merge)!;
            var store = WorkflowPorts.Store(home);
            var copy = CopyOf(store, shipped) ?? Save(home, store, shipped);
            DriverConfig.Empty.WithDefaultWorkflow(copy).Save(configPath);
            // Its rules at today's numbers too, so its lines read them from the first start; a store that does not take them leaves the
            // home for the conversion to seed.
            SupervisorRuleBook.SeedFresh(home);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or ChoiceWords)
        {
            // Not seeded: the place then follows Current, which is today's behaviour, and the next start tries again.
            return false;
        }
    }

    private static string? CopyOf(IWorkflowStore store, WorkflowDefault shipped) =>
        new PlannedWorkflows(store, "").CopyOf(shipped, opinion: false, look: false);

    private static string Save(string home, IWorkflowStore store, WorkflowDefault shipped)
    {
        var made = WorkflowDefaults.Copy(shipped, opinion: false, look: false, id => store.Load(id) is not null);
        var plan = WorkflowOperations.PlanGraph(home, null, made.Id, null, made.Name, "screen", PlannedWorkflows.At, made.Graph, made.From);
        store.Write(made.Id, plan.Plan.Result!);
        return made.Id;
    }
}
