namespace Daoris.Driver;

/// <summary>
/// The <c>brainstorm</c> posture (BRAINSTORM1a2; D176 points 1 and 2, its amendment of D72): what a brainstorm's pass is handed where
/// its agent takes Daoris's rules. Reading in its room and its one result tool, and nothing else: no edit tool, no shell, no web, no
/// <c>commit</c>, and no connector tool beyond its result, so no knowledge search, no quest, no helper's ask and no channel.
/// </summary>
/// <remarks>
/// <para><b>Laid over the person's composition, narrowing only.</b> The person's asks and denies are handed as written, and deny wins
/// at the harness (D72). Their allows are not: a pass reads its room alone (D176 point 2), and a person's allow of a shell command, a
/// folder or the web was made for sessions that work in a repository, which a pass is not. Every connector tool but the turn's is
/// denied by name, since the person's composition and the defaults name them all, and an unlisted one is the classifier's to judge.</para>
///
/// <para><b>Measured only where it is handed.</b> Claude Code, by either door, honours these rules; for any other maker the room's
/// contents are the floor until its posture is measured (BRAINSTORM1c, D53).</para>
/// </remarks>
public static class BrainstormPermissions
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>A generator's one result tool.</summary>
    public const string Give = "brainstorm_give";

    /// <summary>The synthesis's one result tool.</summary>
    public const string Synthesize = "brainstorm_synthesize";

    /// <summary>The result tool a turn answers through: a synthesis's (<c>s1</c>, <c>s1r</c>), else a generator's.</summary>
    public static string ToolOf(string turn) => turn.StartsWith('s') ? Synthesize : Give;

    /// <summary>
    /// Every tool the connector offers, each denied a pass but its turn's own. 🔴 The service's <c>McpToolRegistrationTests</c> holds
    /// this list to what its host advertises, so a tool the connector gains is denied here in the same change.
    /// </summary>
    internal static readonly string[] ConnectorTools =
    [
        "accept_propose", "agent_propose", "agent_settings_propose", "ask_propose", "brainstorm_give", "brainstorm_synthesize",
        "browser_propose", "channel_post", "channel_read", "clear_propose", "delete_propose", "go_ahead_ask", "go_propose",
        "hand_propose", "handover_propose", "helper_answer", "helper_ask", "housekeeping_clean_up", "housekeeping_discard_branch", "housekeeping_inspect",
        "knowledge_convergence", "knowledge_get", "knowledge_refresh",
        "knowledge_repositories", "knowledge_search", "line_read", "opinion_answer", "opinion_give", "pause_propose", "permission_propose",
        "plugin_propose", "quest_list", "quest_publish", "quest_respond", "registry", "repository_propose", "review_ready",
        "review_serve", "session_propose", "setting_propose", "settings_cap", "settings_cooloff", "settings_language", "settings_notify",
        "settings_timeout", "setup_propose", "sync_propose", "workflow_propose",
    ];

    /// <summary>What a pass is denied in the harness's own rule language beside the connector's tools: writing, the shell and the web.</summary>
    private static readonly string[] Withheld = ["Edit", "Write", "NotebookEdit", "Bash", "WebFetch", "WebSearch"];

    /// <summary>
    /// What auto mode's classifier is told beside the rules (D122 §3.7): the forms no rule can name. Handed after the harness's own
    /// list and the defaults', never instead of them.
    /// </summary>
    public const string HardDeny =
        "Doing anything but reading this folder and giving one answer while taking part in a brainstorm: editing, writing, moving or "
        + "deleting a file in any form; running any command or program; reading a file outside this folder; reaching the web or any "
        + "network; calling any tool but the one result tool; committing, pushing, deploying or publishing; and asking another agent "
        + "anything. A brainstorm's pass reads its packet and gives its answer, and changes nothing.";

    /// <summary>
    /// The rules a pass is handed: a read of its <paramref name="room"/> and its one <paramref name="tool"/> allowed, the person's asks
    /// and denies kept, and every write, the shell, the web and the connector's other tools denied, each rule once.
    /// </summary>
    public static RuleLists Shape(RuleLists composed, string room, string tool) =>
        new RuleLists([PermissionRules.ReadRule(room), Connector + tool], composed.Ask, composed.Deny).Joined(new RuleLists(
            [],
            [],
            [.. Withheld, .. ConnectorTools.Where(each => each != tool).Select(each => Connector + each)]));
}
