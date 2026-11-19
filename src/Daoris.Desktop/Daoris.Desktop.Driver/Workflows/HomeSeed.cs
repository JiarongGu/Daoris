namespace Daoris.Driver;

/// <summary>
/// What a home is given once. Where it has no <c>driver.json</c> yet: the copy of <c>merge-after-accept</c> saved, and
/// <c>defaultWorkflow</c> naming it, so a place follows a saved graph from its first start. Where the machine's supervisor rules were
/// never written, with or without a <c>driver.json</c> beside them (a terminal's verb writes one): the seeded rules and
/// <i>Second check</i>'s first choices, the rules' own record saying the home is seeded.
/// </summary>
public static class HomeSeed
{
    /// <summary>
    /// Seed the home. The copy is written first and the file after, so a stop between them leaves a saved copy and no default, which the
    /// next start seeds again as a home without a file. The roles' seed is tried at every start until the rules are written.
    /// </summary>
    /// <returns>Whether the home got its workflow's seed.</returns>
    public static bool Once(string configPath, string home)
    {
        var fresh = !File.Exists(configPath);
        var seeded = fresh && SeedWorkflow(configPath, home);
        SeedRoles(home, fresh);
        return seeded;
    }

    private static bool SeedWorkflow(string configPath, string home)
    {
        try
        {
            var shipped = WorkflowDefaults.Of(SetupPresets.Merge)!;
            var store = WorkflowPorts.Store(home);
            var copy = CopyOf(store, shipped) ?? Save(home, store, shipped);
            DriverConfig.Empty.WithDefaultWorkflow(copy).Save(configPath);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or ChoiceWords)
        {
            // Not seeded: the place then follows Current, which is today's behaviour, and the next start tries again.
            return false;
        }
    }

    /// <summary>
    /// The rules at today's numbers and the second check's first choices, where the machine's rules were never written; the choices go
    /// first, so the rules' record is written last and marks the seed done. A fresh home writes as a settled start does, since it holds
    /// nothing a confirmation could take back; a home whose file came first waits for its start to settle, and a later start seeds it.
    /// </summary>
    private static void SeedRoles(string home, bool fresh)
    {
        Func<StoreSettled>? settled = fresh ? () => StoreSettled.Yes : null;
        try
        {
            if (new SupervisorRuleStore(home, settled).Seeded()) return;
            SecondCheck.SeedFresh(home, settled);
            SupervisorRuleBook.SeedFresh(home, settled);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or ChoiceWords
                                          or DesktopStoreException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Not seeded: until the rules are written the seeded rules decide all the same, and the next start tries again.
        }
    }

    private static string? CopyOf(IWorkflowStore store, WorkflowDefault shipped) =>
        new PlannedWorkflows(store, "").CopyOf(shipped, opinion: false, look: false);

    internal static string Save(string home, IWorkflowStore store, WorkflowDefault shipped)
    {
        var made = WorkflowDefaults.Copy(shipped, opinion: false, look: false, id => store.Load(id) is not null);
        var plan = WorkflowOperations.PlanGraph(home, null, made.Id, null, made.Name, "screen", PlannedWorkflows.At, made.Graph, made.From);
        store.Write(made.Id, plan.Plan.Result!);
        return made.Id;
    }
}
