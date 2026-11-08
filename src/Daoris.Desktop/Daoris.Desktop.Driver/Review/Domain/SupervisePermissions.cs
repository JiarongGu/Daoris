namespace Daoris.Driver;

/// <summary>
/// The rules a supervisor's turn is handed where its agent takes Daoris's rules: the reviewer's read-only shape, with the harness's shell
/// tools denied whole, so no command runs whatever an allow names (the person's interpreter's, a widening's, the copy's), and none of the
/// reviewer's git reading or its say; its connector offers the reads the call table serves it. It reads through the harness's read tools
/// and is handed what it decides on, and its decision takes effect only through its kind's keeping under its role's grants.
/// </summary>
public static class SupervisePermissions
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>The harness's shell tools as the tree guard's matcher names them, each denied whole.</summary>
    public static IReadOnlyList<string> Shell { get; } = ["Bash", "PowerShell"];

    /// <summary>Its connector's tools it may call: the service's <c>CallTable.SupervisorServed</c>, held alike by its tests.</summary>
    public static IReadOnlyList<string> Offered { get; } =
    [
        "handback_read", "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "line_read", "memory_get",
        "memory_search", "quest_list", "registry",
    ];

    /// <summary>What auto mode's classifier is told beside the rules.</summary>
    public const string HardDeny =
        "Changing anything while deciding as a supervisor: editing, writing, moving or deleting a file in any form; running any command or "
        + "program; committing, pushing or publishing; changing a role, its grants, a permission rule or Daoris's settings; starting, "
        + "stopping or touching a process, a port or a server; and taking, closing or publishing a quest. A supervisor reads, and decides "
        + "only in its last message, under its role's grants.";

    /// <summary>The rules <paramref name="composed"/> becomes for a supervisor's turn.</summary>
    public static RuleLists Shape(RuleLists composed)
    {
        var reviewer = OpinionPermissions.Shape(composed);
        var offered = Offered.Select(tool => Connector + tool).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> allow =
        [
            .. reviewer.Allow.Where(rule => !Shelled(rule) && (!rule.StartsWith(Connector, StringComparison.Ordinal) || offered.Contains(rule))),
            .. offered.Where(rule => !reviewer.Allow.Contains(rule)),
        ];
        IReadOnlyList<string> deny =
        [
            .. reviewer.Deny.Where(rule => !offered.Contains(rule)),
            .. Shell.Where(tool => !reviewer.Deny.Contains(tool)),
            .. new[] { "opinion_give", "helper_answer" }.Select(tool => Connector + tool).Where(rule => !reviewer.Deny.Contains(rule)),
        ];
        return new RuleLists(allow, [.. reviewer.Ask.Where(rule => !Shelled(rule))], deny);
    }

    private static bool Shelled(string rule) => Shell.Any(tool => rule == tool || rule.StartsWith(tool + "(", StringComparison.Ordinal));
}
