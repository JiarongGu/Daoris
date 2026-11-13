using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A system department's plan (the place-work design §5.1): one typed tool a session there submits its intended changes through, which
/// the driver hands to the system's adapter and keeps as its next plan (<see cref="SystemPlanBox"/>); no write path, the person reviews.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the request is written and its answer read, under the home.</param>
[McpServerToolType]
public sealed class SystemPlanTools(IntakeScope? intake = null, SessionLedger? ledger = null, SystemPlanBox? box = null)
{
    [McpServerTool(Name = "system_plan")]
    [Description(
        "Plan changes to the system this session's department is: a system reached only through its console, which you never change "
        + "directly. Name each resource once, with the whole value it should hold or that it is removed, in the order to apply. "
        + "Daoris keeps the plan as creates, updates and deletes against what the adapter read; a resource already holding what you "
        + "asked is left out. Nothing changes now: the person reviews it and it applies after their Accept. If a resource moved "
        + "meanwhile you are told its value: plan again. A later call keeps the next plan; the newest is the one reviewed.")]
    public async Task<string> PlanAsync(
        [Description("The changes, in order: each `{ resource, value }` (the resource's whole new value, as text) or `{ resource, remove: true }`.")]
        SystemPlanChange[] changes,
        CancellationToken ct = default)
    {
        var plans = box ?? SystemPlanBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return await plans.PlanAsync(record, session, changes, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }
}
