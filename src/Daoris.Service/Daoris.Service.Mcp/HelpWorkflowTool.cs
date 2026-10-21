using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's <c>workflow_propose</c> (WORKFLOW1h-a): the <c>workflow</c> kind's tool, written by <see cref="HelpProposalBox.ProposeWorkflow"/>.
/// Since FABRIC-PIPELINE1i it takes a verification pipeline's <c>graph</c> beside <c>steps</c>, and a <c>route</c> with no version.
/// </summary>
[McpServerToolType]
public sealed class HelpWorkflowTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "workflow_propose")]
    [Description(
        "Ask Daoris only: propose a whole version of a workflow (how work in a repository or workspace goes from its start to "
        + "its landing) for the person to apply, new or an edit of an existing one, and optionally that the scope use it. "
        + "Nothing changes until the person presses Apply, and the driver refuses a version it cannot take. Send the whole "
        + "steps, never a change to them. A verification pipeline, which verifies the work that lands, takes its whole graph "
        + "instead of steps. A route with no steps and no graph chooses a workflow saved here for that route, saving nothing. "
        + "A new workflow is named by the person's words in `name`, at most 60 characters; left out, its id names it. An edit "
        + "keeps its name, so send none with a base.")]
    public string ProposeWorkflow(
        [Description("The repository or workspace the workflow applies to, one word.")]
        string scope,
        [Description("The workflow's id: lower-case letters, digits and dashes, at most 40. A new id proposes a new workflow.")]
        string workflow,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The whole steps, a JSON list of step objects as the workflow editor's file holds them. Leave out with a graph, or for a route alone.")]
        string? steps = null,
        [Description("The version an edit is made on. Leave out for a new workflow, and for a route alone.")]
        int? @base = null,
        [Description("True to propose that the scope also use this workflow as its default.")]
        bool choose = false,
        [Description("Not proposed yet: leave out.")]
        string? changes = null,
        [Description("Not proposed yet: leave out.")]
        string? kind = null,
        [Description("Not proposed yet: leave out.")]
        string? import = null,
        [Description("A new workflow's name, the person's words: a short line, at most 60 characters. Leave out to name it by its id, and for an edit.")]
        string? name = null,
        [Description("A verification pipeline's whole graph, its JSON object (schemaVersion 2, nodes and wires), holding at least one `check` and "
            + "landing nothing. A new workflow sent as a graph is made a verification pipeline. Leave out with steps.")]
        string? graph = null,
        [Description("The route the scope chooses this workflow for: `default`, `landed` (the work that lands, a verification pipeline only), "
            + "or `kind:<id>` for one of its kinds. With no steps and no graph, it chooses a workflow saved here and saves nothing.")]
        string? route = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeWorkflow(scope, workflow, @base, steps, choose, changes, kind, import, why, intake?.Session, DateTimeOffset.UtcNow, name, graph, route).Message;
    }
}
