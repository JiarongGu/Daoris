namespace Daoris.Driver;

/// <summary>
/// The rules a partner's session is handed (COAGENT1b, D199 point 4; the co-agent design §7.4), over the ones every session is: where its
/// agent takes Daoris's rules, its connector offers the reading tools, its lead's line and <c>partner_answer</c>, and denies every other
/// tool; a push is denied whatever the person's defaults say. It writes, in its copy alone, which the tree guard holds it to.
/// </summary>
/// <remarks>
/// A second layer, never the only one: the connector's host refuses the same tools by the record's role before they run
/// (<c>ConnectorRoles</c> in the service, whose lists these are), and an agent that takes no rules, as Codex does not, is held by its copy
/// and its measured enforcement mechanism alone (§3.3).
/// </remarks>
public static class PartnerPermissions
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>Its connector's tools it may call: the knowledge it reads, its lead's line, and its answer (the service's <c>PartnerAllowed</c>).</summary>
    internal static readonly string[] Offered =
    [
        "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "line_read", "partner_answer", "registry",
    ];

    /// <summary>Every other tool the connector offers, each denied by name: a partner asks nobody and acts on nothing outside its copy.</summary>
    internal static IEnumerable<string> Acting => BrainstormPermissions.ConnectorTools.Where(tool => !Offered.Contains(tool));

    /// <summary>A push in every form a rule must name apart, denied whatever the person's defaults say: its clone has no remote to need one.</summary>
    private static readonly string[] Pushing = ["Bash(git push:*)", "Bash(git -* push)", "Bash(git -* push *)"];

    /// <summary>What auto mode's classifier is told beside the rules: the forms no rule can name.</summary>
    public const string HardDeny =
        "Changing anything outside your copy while writing a piece of another session's work: writing, moving or deleting a file outside "
        + "the folder you were started in, by any path or link; pushing anywhere; reaching Daoris's local host, its pipe or the person's "
        + "servers; changing or proposing a permission rule; taking, closing, declining or publishing a quest; and asking another agent "
        + "or the person anything. A partner writes its piece in its copy and answers once.";

    /// <summary>
    /// The rules a partner is handed: <paramref name="composed"/> with the acting tools taken out of its allows, its offered tools added,
    /// and the acting tools and a push denied, each rule once.
    /// </summary>
    public static RuleLists Shape(RuleLists composed)
    {
        var withheld = new HashSet<string>(Acting.Select(tool => Connector + tool), StringComparer.Ordinal);
        IReadOnlyList<string> allow = [.. composed.Allow.Where(rule => !withheld.Contains(rule))];
        return new RuleLists(allow, composed.Ask, composed.Deny).Joined(new RuleLists(
            [.. Offered.Select(tool => Connector + tool)],
            [],
            [.. Pushing, .. Acting.Select(tool => Connector + tool)]));
    }
}
