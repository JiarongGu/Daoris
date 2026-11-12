namespace Daoris.Driver;

/// <summary>
/// The one acceptance of a line's <c>daoris.gates.json</c>, for every purpose the file serves: the blob kept as the integration's
/// (<see cref="IntegrationAcceptance"/>) and, where the repository's landed route verifies, as the gates that route runs. A person's press
/// and a full-access repository's own line both write it here, so the two records never part.
/// </summary>
public static class DeclarationAcceptance
{
    /// <summary>
    /// What full access accepts as the line's declaration lands: <paramref name="owed"/>'s blob accepted in the file at
    /// <paramref name="configPath"/>, and the sentence that says it (what changed against the blob accepted before); null where it was
    /// accepted meanwhile, or where its checkout is gone.
    /// </summary>
    internal static async Task<string?> AcceptByModeAsync(string home, string configPath, DeclarationOwed owed, CancellationToken ct)
    {
        var config = DriverConfig.Load(configPath);
        if (owed.Root is not { } root) return null;
        var before = IntegrationAcceptance.Of(config, owed.Repository)
            ?? (WorkflowSelection.ResolveLanded(config, owed.Repository, owed.Workspace).Gates);
        var was = before is { } blob ? await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false) : null;
        var now = await IntegrationAcceptance.TextAsync(root, owed.Blob, ct).ConfigureAwait(false);
        Accept(home, config, owed.Repository, owed.Workspace, owed.Blob).Save(configPath);
        return $"{owed.Repository}'s {DeclarationChange.Between(was, now).Words(owed.Line)}";
    }

    /// <summary>The one withdrawal: <paramref name="repository"/>'s acceptance dropped for every purpose, as <see cref="Accept"/> kept it.</summary>
    public static DriverConfig Withdraw(string home, DriverConfig config, string repository, string? workspace)
    {
        var next = config.WithIntegrationGates(repository, null);
        var landed = WorkflowSelection.ResolveLanded(next, repository, workspace);
        if (landed.Workflow == WorkflowSelection.Current || landed.Gates is null) return next;
        var found = WorkflowPorts.Store(home).Load(landed.Workflow);
        return WorkflowSelection.Apply(next, new WorkflowUse(landed.Workflow, repository, null, null)
        {
            Landed = true,
            Gates = null,
            Pipeline = found?.Read.Pipeline,
        });
    }

    /// <summary>
    /// <paramref name="config"/> with <paramref name="blob"/> (the line's declaration, or <see cref="IntegrationAcceptance.None"/>) accepted for
    /// <paramref name="repository"/>; the landed route's gates follow it only where that route names a verification pipeline saved under <paramref name="home"/>.
    /// </summary>
    public static DriverConfig Accept(string home, DriverConfig config, string repository, string? workspace, string blob)
    {
        var next = config.WithIntegrationGates(repository, blob);
        var landed = WorkflowSelection.ResolveLanded(next, repository, workspace);
        if (landed.Workflow == WorkflowSelection.Current) return next;
        var found = WorkflowPorts.Store(home).Load(landed.Workflow);
        if (found?.Read.Verifies != true) return next;
        return WorkflowSelection.Apply(next, new WorkflowUse(landed.Workflow, repository, null, null)
        {
            Landed = true,
            Gates = blob,
            Pipeline = found.Read.Pipeline,
        });
    }
}
