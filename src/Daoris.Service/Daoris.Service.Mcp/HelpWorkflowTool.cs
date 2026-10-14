using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>workflow_propose</c> (WORKFLOW1h-a): the <c>workflow</c> kind's tool, written by <see cref="HelpProposalBox.ProposeWorkflow"/>.</summary>
[McpServerToolType]
public sealed class HelpWorkflowTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "workflow_propose")]
    [Description(
        "Ask Daoris only: propose a whole version of a workflow (how work in a repository or workspace goes from its start to "
        + "its landing) for the person to apply, new or an edit of an existing one, and optionally that the scope use it. "
        + "Nothing changes until the person presses Apply, and the driver refuses a version it cannot take. Send the whole "
        + "steps, never a change to them. A new workflow is named by the person's words in `name`, at most 60 characters; "
        + "left out, its id names it. An edit keeps its name, so send none with a base.")]
    public string ProposeWorkflow(
        [Description("The repository or workspace the workflow applies to, one word.")]
        string scope,
        [Description("The workflow's id: lower-case letters, digits and dashes, at most 40. A new id proposes a new workflow.")]
        string workflow,
        [Description("The whole steps, a JSON list of step objects as the workflow editor's file holds them.")]
        string steps,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The version an edit is made on. Leave out for a new workflow.")]
        int? @base = null,
        [Description("True to propose that the scope also use this workflow.")]
        bool choose = false,
        [Description("Not proposed yet: leave out.")]
        string? changes = null,
        [Description("Not proposed yet: leave out.")]
        string? kind = null,
        [Description("Not proposed yet: leave out.")]
        string? import = null,
        [Description("A new workflow's name, the person's words: a short line, at most 60 characters. Leave out to name it by its id, and for an edit.")]
        string? name = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeWorkflow(scope, workflow, @base, steps, choose, changes, kind, import, why, intake?.Session, DateTimeOffset.UtcNow, name).Message;
    }
}
