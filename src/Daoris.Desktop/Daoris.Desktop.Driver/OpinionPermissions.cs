namespace Daoris.Driver;

/// <summary>
/// The rules a second opinion's session is handed (XAGENT1d, D155 point 5; the second-agent design §5.2, §5.4), over the ones
/// every session is: where its agent takes Daoris's rules, every edit tool is denied, the <c>commit</c> default is withheld,
/// and its connector offers reading and <c>opinion_give</c>, and nothing that closes, publishes, permits or asks.
/// </summary>
/// <remarks>
/// <para><b>Laid over the person's composition, never instead of it.</b> The defaults still on, the machine's rules, the
/// workspace's and the repository's are composed as for any session (<see cref="PermissionRules.Compose"/>); this takes away
/// what a reviewer is not given and adds what it is denied. A person's own deny is handed as written, and deny wins at the
/// harness (D72). <c>no-push</c> and the tree guard, on its copy, stand as for every session.</para>
///
/// <para><b>Denied, not only withheld.</b> The copy holds the repository's own settings, whose allow-list may name an edit, a
/// commit or a connector tool; only a deny outranks it there. The copy is the floor whatever an agent's rules say (§5.1): these
/// rules are what Claude Code honours on top of it (PERM1), measured for a reviewer by XAGENT1j.</para>
///
/// <para><b>Reading is its job</b>, so its history is read with git's own reading commands unasked, as an intake's room names
/// its tools (FG5): an ask is a refusal here (D52). With <c>verify</c>, what the repository declares safe is the person's rules'
/// to allow, as for any session; no rule here widens it.</para>
/// </remarks>
public static class OpinionPermissions
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>Its connector's tools it may call: the knowledge it reads, the quests it reads, and its one say.</summary>
    private static readonly string[] Offered =
    [
        "registry", "knowledge_search", "knowledge_get", "knowledge_repositories", "knowledge_convergence", "quest_list", "opinion_give",
    ];

    /// <summary>
    /// Its connector's tools it is never given (§5.4): a quest's take and close, a publish, a go-ahead's ask, a rule's proposal,
    /// a set-up's serving and its ready, a working session's answer, and the machine's own index rebuild.
    /// </summary>
    private static readonly string[] Acting =
    [
        "quest_respond", "quest_publish", "go_ahead_ask", "permission_propose", "review_serve", "review_ready", "opinion_answer",
        "knowledge_refresh",
    ];

    /// <summary>What it reads its history with: git's reading commands, and the <c>cd</c> an agent prefixes one with.</summary>
    private static readonly string[] Reading =
    [
        "Bash(cd:*)", "Bash(git log:*)", "Bash(git show:*)", "Bash(git diff:*)", "Bash(git status:*)", "Bash(git blame:*)",
        "Bash(git grep:*)", "Bash(git ls-files:*)", "Bash(git rev-parse:*)", "Bash(git rev-list:*)", "Bash(git merge-base:*)",
        "Bash(git cat-file:*)",
    ];

    /// <summary>
    /// What it is denied in the harness's own rule language: every tool that writes a file, and git's acts that make or move a
    /// commit, a branch or a tag, each also in the option-first form a rule must name apart (UNBLOCK4's lesson).
    /// </summary>
    private static readonly string[] Writing =
    [
        "Edit", "Write", "NotebookEdit",
        .. new[] { "commit", "merge", "rebase", "cherry-pick", "revert", "reset", "tag", "am", "apply", "stash", "switch", "checkout" }
            .SelectMany(act => new[] { $"Bash(git {act}:*)", $"Bash(git -* {act})", $"Bash(git -* {act} *)" }),
    ];

    /// <summary>
    /// What auto mode's classifier is told beside the rules (D122 §3.7): the forms no rule can name. Handed after the harness's
    /// own list and the defaults', never instead of them.
    /// </summary>
    public const string HardDeny =
        "Changing anything while giving a second opinion: editing, writing, moving or deleting a file in any form; committing, "
        + "merging, rebasing, resetting, or making or moving a branch or a tag; pushing; deploying or publishing; changing or "
        + "proposing a permission rule; starting, stopping or touching a process, a port or a server of the person's; and taking, "
        + "closing, declining or publishing a quest. A second opinion reads and says, and changes nothing.";

    /// <summary>
    /// The rules a reviewer is handed: <paramref name="composed"/> with the <c>commit</c> default's allows and the acting tools
    /// taken out of its allows, its offered tools and its reading added, and its writes and the acting tools denied, each rule once.
    /// </summary>
    public static RuleLists Shape(RuleLists composed)
    {
        var withheld = new HashSet<string>(
            PermissionRules.Defaults.Where(shipped => shipped.Id == "commit").SelectMany(shipped => shipped.Rules)
                .Concat(Acting.Select(tool => Connector + tool)),
            StringComparer.Ordinal);
        // `cd` is its reading's prefix too, so the commit default's one stays given.
        withheld.Remove("Bash(cd:*)");

        IReadOnlyList<string> allow = [.. composed.Allow.Where(rule => !withheld.Contains(rule))];
        var shaped = new RuleLists(allow, composed.Ask, composed.Deny);
        return shaped.Joined(new RuleLists(
            [.. Offered.Select(tool => Connector + tool), .. Reading],
            [],
            [.. Writing, .. Acting.Select(tool => Connector + tool)]));
    }
}

/// <summary>
/// What held a reviewer read-only (XAGENT1d, D155 point 5; design §5.2), as the opinion's record says it: the posture is the
/// adapter's, in its own words, and only where measured (D53). A mode's name is never relied on: one called <c>read-only</c>
/// runs a workspace-write sandbox (ACP3's reading of <c>codex-acp</c>).
/// </summary>
public static class OpinionPosture
{
    /// <summary>The agent takes Daoris's rules (Claude Code, by either door), so the opinion's rules held it, beside its copy.</summary>
    public const string RulesAndCopy = "rules-and-copy";

    /// <summary>Its copy alone held it: an agent whose posture as a reviewer nobody has measured is held by the floor only.</summary>
    public const string CopyAlone = "copy-alone";

    /// <summary>What held a reviewer on <paramref name="adapter"/>: its rules where it takes them, else its copy alone.</summary>
    public static string Of(ISessionAdapter adapter) => adapter.TakesSettings ? RulesAndCopy : CopyAlone;

    /// <summary>The posture as the record says it (design §5.2).</summary>
    public static string Said(string posture) => posture == RulesAndCopy
        ? "read-only by its agent's rules and its copy"
        : "read-only by its copy alone";
}
