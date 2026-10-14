using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The tools not yet a class of their own: review, go-ahead, helper and opinion (PART10b), and Ask Daoris's
/// <c>*_propose</c> (PART10c). The search, quest and rule tools are <see cref="KnowledgeSearchTools"/>,
/// <see cref="QuestTools"/> and <see cref="RuleProposalTools"/>; every class is listed on <see cref="KnowledgeServer"/>.
/// </summary>
/// <remarks>
/// Ask Daoris's <c>*_propose</c> tools are a file each, <c>KnowledgeTools.Help.&lt;Kind&gt;.cs</c>, one per kind
/// of proposal the box writes (MOD6).
/// </remarks>
/// <param name="intake">
/// Which ask this connector speaks for, when the session is an intake; and which session it is, when
/// the driver started it — what a proposal, a go-ahead, a set-up, an opinion and a helper's ask record as theirs.
/// </param>
/// <param name="ledger">The ledger, for what a session's connector writes on its own record or reads of it.</param>
/// <param name="help">Where Ask Daoris's proposals are written (HELP1c, D89) — under the home.</param>
/// <param name="opinions">Second opinions (XAGENT1c): what a reviewer says and what a working session answers.</param>
/// <param name="helpers">Helpers (MIXAGENT2a-i): where a working session's ask and a helper's answer are written — under the home.</param>
[McpServerToolType]
public sealed partial class KnowledgeTools(
    IntakeScope? intake = null, SessionLedger? ledger = null, HelpProposalBox? help = null, OpinionDesk? opinions = null,
    HelperBox? helpers = null)
{
}
