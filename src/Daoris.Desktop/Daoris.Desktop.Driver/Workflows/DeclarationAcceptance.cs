namespace Daoris.Driver;

/// <summary>
/// The one acceptance of a line's <c>daoris.gates.json</c>, for every purpose the file serves: the blob kept as the integration's
/// (<see cref="IntegrationAcceptance"/>) and, where the repository's landed route verifies, as the gates that route runs. A person's press
/// and a full-access repository's own line both write it here, so the two records never part.
/// </summary>
public static class DeclarationAcceptance
{
    /// <summary>
    /// What the risk policy takes as the line's declaration lands (<see cref="IntegrationOwed.Passed"/>): <paramref name="owed"/>'s blob accepted in
    /// the file at <paramref name="configPath"/>, and the one line telling the person what changed and the rule that passed it; null where it
    /// is accepted meanwhile, the policy no longer passes it, or its checkout is gone. Both are judged again on the file as its writer
    /// reads it under its lock (<see cref="DriverConfigDisk.Edit"/>), the verdicts asked of the policy that file holds, so a choice another
    /// writer made meanwhile, a raised row included, is never written over.
    /// </summary>
    internal static async Task<string?> AcceptPassedAsync(string home, string configPath, DeclarationOwed owed, CancellationToken ct)
    {
        if (owed.Root is not { } root) return null;
        var before = Before(DriverConfig.Load(configPath), owed);
        var was = before is { } blob ? await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false) : null;
        var now = await IntegrationAcceptance.TextAsync(root, owed.Blob, ct).ConfigureAwait(false);
        var accepted = false;
        RiskVerdict? by = null;
        DriverConfigDisk.Edit(configPath, current =>
        {
            if (Before(current, owed) != before) return current;
            var verdicts = DeclarationVerdicts.Of(current, owed.Repository, owed.Workspace, owed.Line, owed.Blob, owed.ChangedUnits, owed.Verdicts.ToDictionary(each => each.Unit, each => each.Digest, StringComparer.Ordinal));
            if (!DeclarationVerdicts.Passes(verdicts)) return current;
            accepted = true;
            by = DeclarationVerdicts.Deciding(verdicts);
            return Accept(home, current, owed.Repository, owed.Workspace, owed.Blob);
        });
        return accepted
            ? $"{owed.Repository}'s {DeclarationChange.Between(was, now).Words(owed.Line).TrimEnd('.')} (passed by the risk policy: {by!.Why})."
            : null;
    }

    /// <summary>
    /// A change that alters no command (<see cref="IntegrationOwed.Kept"/>) accepted and said with what it set aside; null where its judged
    /// acceptance no longer stands, judged on the file as its writer reads it under its lock, so a withdrawal another writer made
    /// meanwhile is never undone.
    /// </summary>
    internal static Task<string?> AcceptKeptAsync(string home, string configPath, DeclarationOwed owed, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (owed.Against is not { } against || Before(DriverConfig.Load(configPath), owed) != against) return Task.FromResult<string?>(null);
        var accepted = false;
        DriverConfigDisk.Edit(configPath, current =>
        {
            if (Before(current, owed) != against) return current;
            accepted = true;
            return Accept(home, current, owed.Repository, owed.Workspace, owed.Blob);
        });
        if (!accepted) return Task.FromResult<string?>(null);
        var kept = DeclarationSetAside.Said(owed.SetAside) is { Length: > 0 } parts ? $", only {parts}" : "";
        var narrowed = DeclaredNarrowing.Said(owed.Narrowed) is { Length: > 0 } said ? $" ({said})" : "";
        return Task.FromResult<string?>($"{owed.Repository}'s daoris.gates.json on {owed.Line} changed no command{kept}{narrowed}, so it stays accepted as it landed.");
    }

    /// <summary>
    /// <paramref name="pending"/>'s acceptance converted: each unit's digest computed from the accepted blob's text and kept beside it; false
    /// where the text does not read, or the file as its writer reads it under its lock moved or converted it already.
    /// </summary>
    internal static async Task<bool> ConvertAsync(string configPath, DeclarationUnconverted pending, CancellationToken ct)
    {
        if (DeclarationUnits.Of(await IntegrationAcceptance.TextAsync(pending.Root, pending.Blob, ct).ConfigureAwait(false)) is not { } units) return false;
        var converted = false;
        DriverConfigDisk.Edit(configPath, current =>
        {
            if (IntegrationAcceptance.Of(current, pending.Repository) != pending.Blob
                || (current.IntegrationUnits.TryGetValue(pending.Repository, out var held) && held.Blob == pending.Blob))
            {
                return current;
            }

            converted = true;
            return current.WithIntegrationUnits(pending.Repository, new AcceptedUnits(pending.Blob, units));
        });
        return converted;
    }

    /// <summary>The blob <paramref name="owed"/> is judged against under <paramref name="config"/>: the integration's acceptance, else the landed route's gates.</summary>
    private static string? Before(DriverConfig config, DeclarationOwed owed) =>
        IntegrationAcceptance.Of(config, owed.Repository) ?? WorkflowSelection.ResolveLanded(config, owed.Repository, owed.Workspace).Gates;

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
