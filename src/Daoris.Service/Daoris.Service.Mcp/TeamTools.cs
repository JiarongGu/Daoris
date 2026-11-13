using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The plan door, <c>team_plan</c>, served to every caller: it reads an ask's plan, or writes its next revision. A session answering an ask
/// plans the ask; a worker serving a part splits that part, with itself as its lead. Each part written is published as a quest under its id,
/// so writing the same part again returns its quest.
/// </summary>
/// <param name="teams">What judges, keeps and publishes a plan.</param>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record: the ask or the part it serves.</param>
[McpServerToolType]
public sealed class TeamTools(TeamDesk teams, IntakeScope? intake = null, SessionLedger? ledger = null)
{
    [McpServerTool(Name = "team_plan")]
    [Description(
        "Read or write the plan of an ask. Leave `parts` out to read the plan, its flags and each part's quest. Give `parts` to write the "
        + "next revision whole: each part is a feature a person can see working, in one repository and lane, with its claims (path globs it "
        + "will change), proof and pack; each becomes a quest under its id, so writing a part again returns its quest. An edge is `after` "
        + "(a build dependency) or `orders` (a landing order only), with its reason. A cycle, an edge to a part nobody wrote, and a claim on a "
        + "record or a generated output are saved with a flag, never refused; the integrator writes records and generated files. A worker "
        + "serving a part splits its own part, one level down, as its lead. One part is a valid plan.")]
    public async Task<string> PlanAsync(
        [Description("The ask, where this session does not answer one or serve a part of one.")] string? ask = null,
        [Description("The parts of the next revision, whole.")]
        TeamPart[]? parts = null,
        [Description("The edges: `kind` is `after` or `orders`; `milestone` is oriented, submitted or landed.")]
        TeamEdge[]? edges = null,
        [Description("One paragraph: why these parts, and what changed since the last revision. Needed with `parts`.")] string? why = null,
        [Description("Across repositories: what each releases, from its `deployables` (`daoris repository deployables`). A `mirror` or `reads` of what another provides is a release order.")]
        DeliveryDeployable[]? deployables = null,
        [Description("Release orders you read beyond the declarations: `source` is `stage-condition`, `contract` or `code-read` (with `evidence`), or `policy`/`person`. No evidence is `policy`.")]
        DeliveryEdge[]? releases = null,
        [Description("What only the person can settle about the delivery, each under a stable `key` so it is asked once.")]
        DeliveryQuestion[]? questions = null,
        [Description("The revision you read, 0 for the first plan. A write over a newer one is refused: read again.")] int? revision = null,
        [Description("The commit of the line the plan was read at.")] string? baseCommit = null,
        [Description("To read: the blob each path holds now, as `{path: blob}`; a pack pointer or release order's evidence (`repository:file`) differing is named stale.")]
        Dictionary<string, string>? seen = null,
        [Description("To read: answer the plans whole as JSON, which `node tools/team-briefs.mjs` composes each part's brief from.")]
        bool? json = null,
        CancellationToken ct = default)
    {
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        var scope = await teams.ScopeOfAsync(record, ct).ConfigureAwait(false);
        var target = scope?.Ask ?? ask?.TrimStart('#');
        if (string.IsNullOrWhiteSpace(target)) return "Name the ask (`ask`): this session answers none and serves no part of one.";

        if (parts is null or { Length: 0 })
        {
            var (plans, rows) = await teams.ReadAsync(target, ct).ConfigureAwait(false);
            if (json == true) return TeamPlanReading.Json(target, plans, rows);
            var (members, decisions) = await teams.TeamAsync(target, ct).ConfigureAwait(false);
            return TeamPlanReading.Of(target, plans, rows, seen ?? [], members, decisions, await teams.Scouts.ScoutedAsync(target, ct).ConfigureAwait(false));
        }

        var save = new TeamSave(target, parts, edges ?? [], why ?? "")
        {
            Scope = scope?.Scope, Expect = revision ?? 0, By = session, Base = baseCommit,
            Deployables = deployables ?? [], Releases = releases ?? [], Questions = questions ?? [],
        };
        return (await teams.SaveAsync(save, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;
    }
}
