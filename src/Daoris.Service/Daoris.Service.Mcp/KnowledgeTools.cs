using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The tools not yet a class of their own: Ask Daoris's <c>*_propose</c> (PART10c). The others are classes listed on
/// <see cref="KnowledgeServer"/>.
/// </summary>
/// <remarks>
/// Ask Daoris's <c>*_propose</c> tools are a file each, <c>KnowledgeTools.Help.&lt;Kind&gt;.cs</c>, one per kind
/// of proposal the box writes (MOD6).
/// </remarks>
/// <param name="intake">
/// Which ask this connector speaks for, when the session is an intake; and which session it is, when
/// the driver started it — what a proposal records as theirs.
/// </param>
/// <param name="help">Where Ask Daoris's proposals are written (HELP1c, D89) — under the home.</param>
[McpServerToolType]
public sealed partial class KnowledgeTools(IntakeScope? intake = null, HelpProposalBox? help = null)
{
}
