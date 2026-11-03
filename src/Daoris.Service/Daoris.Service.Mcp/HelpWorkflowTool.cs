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
        + "graph, never a change to it: a workflow is saved as a graph, and a list of steps is refused. A verification pipeline, "
        + "which verifies the work that lands, takes its whole graph too. A route with no graph chooses a workflow saved here for that route, saving nothing. "
        + "A new workflow is named by the person's words in `name`, at most 60 characters; left out, its id names it. An edit "
        + "keeps its name, so send none with a base. Where the message says a workflow draft is open in the person's editor, "
        + "propose changes to that draft with `draft` and the whole changed `graph` instead.")]
    public string ProposeWorkflow(
        [Description("The repository or workspace the workflow applies to, one word.")]
        string scope,
        [Description("The workflow's id: lower-case letters, digits and dashes, at most 40. A new id proposes a new workflow. "
            + "With `draft`, the draft's workflow as the message names it, or empty for a new one.")]
        string workflow,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("No longer proposed: a list of steps is refused. Send the whole `graph` instead.")]
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
        [Description("The workflow's whole graph, its JSON object (schemaVersion 2, nodes and wires). A development pipeline's graph lands its work "
            + "once, by a `land` whose `method` it chooses; a verification pipeline's holds at least one `check` and lands nothing. A new "
            + "workflow whose graph lands is a development pipeline, and one that lands nothing a verification pipeline.")]
        string? graph = null,
        [Description("The route the scope chooses this workflow for: `default`, `landed` (the work that lands, a verification pipeline only), "
            + "or `kind:<id>` for one of its kinds. With no graph, it chooses a workflow saved here and saves nothing.")]
        string? route = null,
        [Description("The fingerprint of the workflow draft open in the person's editor, as the message told it. With it, `graph` is the "
            + "whole changed draft, of either pipeline, and nothing is saved or chosen: the person takes the changes into the draft and saves "
            + "it themselves, so no steps, base, choose, route or name goes with it. Leave out for a saved workflow.")]
        string? draft = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeWorkflow(scope, workflow, @base, steps, choose, changes, kind, import, why, intake?.Session, DateTimeOffset.UtcNow, name, graph, route, draft).Message;
    }
}
