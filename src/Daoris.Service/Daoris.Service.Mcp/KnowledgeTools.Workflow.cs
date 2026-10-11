using System.ComponentModel;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// An intake's kind proposal as it writes it (WORKFLOW1i, the workflow design §4.3): a kind and the person's words it rests on,
/// for the person's press. A kind only, never a workflow: the workflow a kind gives is the driving machine's.
/// </summary>
public sealed record WorkflowProposal(
    [property: Description("The kind of work the ask is, a kind's id of the workspace such as docs: lower-case letters, digits and dashes.")]
    string? Kind,
    [property: Description(
        "The person's own words that say why, copied exactly from what they asked or said since, in at most 300 characters. "
        + "A reason that is not their words is refused.")]
    string? Reason);
