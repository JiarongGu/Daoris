using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The kinds a named workflow may hold, as main runs them — the CLI's
/// <c>WORKFLOW_KINDS</c>, cell for cell. A kind this build runs <see cref="None"/> of is never offered, and a version naming it
/// does not read here. The opinion is <c>partial</c>: its gate reads its rule, with Current's limit for it.
/// </summary>
public static class WorkflowKindTable
{
    /// <summary>A kind this build does not run: designed, never offered.</summary>
    public const string None = "none";

    /// <summary>At most this many steps in a version, a group's members counted.</summary>
    public const int MaxSteps = 24;

    /// <summary>A workflow keeps its newest this many versions, and every version a kept run names.</summary>
    public const int KeptVersions = 20;

    private static readonly WorkflowKindField Failed = new("failed", WorkflowFieldTypes.Choice, ["wait", "send-back", "stop"], false, "wait");

    public static IReadOnlyList<WorkflowKindRow> Table { get; } =
    [
        // The checks its sessions run, as Current draws a repository's rule; absent is today's.
        new(WorkflowKinds.Work, WorkflowRuntime.Built, null,
            [new("checks", WorkflowFieldTypes.Choice, SessionChecks.Choices, false, null) { WhereSet = true }]),
        new(WorkflowKinds.Opinion, WorkflowRuntime.Partial, WorkflowLimits.OpinionPartial,
        [
            new("reviewers", WorkflowFieldTypes.Names, null, false, null),
            new("required", WorkflowFieldTypes.Flag, null, false, false),
            new("steps", WorkflowFieldTypes.Flag, null, false, false),
            new("recheck", WorkflowFieldTypes.Flag, null, false, true),
        ]),
        new(WorkflowKinds.Look, WorkflowRuntime.Partial, WorkflowLimits.LookPartial,
            [new("environment", WorkflowFieldTypes.Environment, null, false, null)]),
        new(WorkflowKinds.Landing, WorkflowRuntime.Built, null,
        [
            new("form", WorkflowFieldTypes.Choice, [LandingForm.Merge, LandingForm.Branch], true, null),
            new("accept", WorkflowFieldTypes.Choice, ["you", "automatic"], true, null),
            new("pattern", WorkflowFieldTypes.Pattern, null, false, null),
            new("plugin", WorkflowFieldTypes.PluginOrNone, null, false, null),
        ]),
        new(WorkflowKinds.PullRequest, WorkflowRuntime.Partial, WorkflowLimits.PullRequestAtCleanUp, []),
        new(WorkflowKinds.GoAhead, WorkflowRuntime.Partial, WorkflowLimits.GoAheadPartial,
        [
            new("act", WorkflowFieldTypes.Text, null, true, null),
            new("on", WorkflowFieldTypes.Text, null, true, null),
            new("refused", WorkflowFieldTypes.Choice, ["stop", "wait"], false, "stop"),
        ]),
        new(WorkflowKinds.Check, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("checks", WorkflowFieldTypes.Checks, null, false, "required"),
            Failed,
        ]),
        new(WorkflowKinds.Stage, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("stage", WorkflowFieldTypes.Id, null, true, null),
            new("start", WorkflowFieldTypes.Choice, ["you", "automatic"], false, "you"),
            Failed,
        ]),
        new(WorkflowKinds.All, None, null, [new("steps", WorkflowFieldTypes.Members, null, true, null), Failed]),
    ];

    /// <summary>A kind's row, or null for a kind nobody has.</summary>
    public static WorkflowKindRow? Of(string kind) => Table.FirstOrDefault(row => row.Kind == kind);
}
