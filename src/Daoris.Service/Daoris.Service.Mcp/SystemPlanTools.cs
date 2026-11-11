using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A system department's plan (the place-work design §5.1): one typed tool a session working there submits its intended changes
/// through, which the driver hands to the system's adapter and keeps as the session's next plan (<see cref="SystemPlanBox"/>). The
/// session holds no write path to the system: the plan is reviewed by the person and applied after.
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
        + "directly. Name each resource once, with the whole value it should hold or that it is removed, in the order they should be applied. "
        + "Daoris hands them to the system's adapter, which reads each resource as it stands and keeps the plan: each change typed as "
        + "a create, an update or a delete under the version it read. The answer names the plan's number and its resources; a resource "
        + "already holding what you asked is left out. Nothing in the system changes now: the person reviews the plan, each resource "
        + "before and after, and it is applied after their Accept. If a resource moved before the apply, you are told its present value "
        + "and plan again. A later call replaces nothing: it keeps the next plan, and the newest is the one reviewed.")]
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
