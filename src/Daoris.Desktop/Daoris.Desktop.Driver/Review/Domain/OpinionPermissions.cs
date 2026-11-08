namespace Daoris.Driver;

/// <summary>
/// The rules a second opinion's session is handed, over the ones
/// every session is: where its agent takes Daoris's rules, every edit tool is denied, the <c>commit</c> default is withheld,
/// and its connector offers reading and <c>opinion_give</c>, and nothing that closes, publishes, permits or asks.
/// </summary>
/// <remarks>
/// <para><b>Laid over the person's composition, never instead of it.</b> The defaults still on, the machine's rules, the
/// workspace's and the repository's are composed as for any session (<see cref="PermissionRules.Compose"/>); this takes away
/// what a reviewer is not given and adds what it is denied. A person's own deny is handed as written, and deny wins at the
/// harness. <c>no-push</c> and the tree guard, on its copy, stand as for every session.</para>
///
/// <para><b>Denied, not only withheld.</b> The copy holds the repository's own settings, whose allow-list may name an edit, a
/// commit or a connector tool; only a deny outranks it there. The copy is the floor whatever an agent's rules say: these
/// rules are what Claude Code honours on top of it.</para>
///
/// <para><b>Reading is its job</b>, so its history is read with git's own reading commands unasked, as an intake's room names
/// its tools: an ask is a refusal here. With <c>verify</c>, what the repository declares safe is the person's rules'
/// to allow, as for any session; no rule here widens it.</para>
/// </remarks>
public static class OpinionPermissions
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>
    /// Its connector's tools it may call: the knowledge it reads, the quests it reads and their handbacks, a repository's
    /// memory it reads, and its one say. The service's <c>CallTable.ReviewerServed</c> serves a reviewer the same on every
    /// door, held to this list by its tests.
    /// </summary>
    private static readonly string[] Offered =
    [
        "registry", "knowledge_search", "knowledge_get", "knowledge_repositories", "knowledge_convergence", "quest_list", "handback_read",
        "memory_search", "memory_get", "opinion_give",
    ];

    /// <summary>
    /// Its connector's tools it is never given: a quest's take and close, a publish, a go-ahead's ask, a rule's proposal,
    /// a set-up's serving and its ready, a working session's answer, a helper's ask and answer (a reviewer asks for
    /// no helper, and is none), the ask's channel (each answers one question, and the service refuses both
    /// it anyway), an ask to hand over (a reviewer and a helper answer one question and go on with nobody), a line's
    /// history (neither continues another session, and the record it would read is the working session's), the
    /// machine's own index rebuild, a park's question (neither parks, and the service refuses both it anyway), and a change to
    /// a repository's memory or a proposal of one (neither keeps memory, and a proposal is a quest).
    /// </summary>
    private static readonly string[] Acting =
    [
        "quest_respond", "quest_publish", "go_ahead_ask", "permission_propose", "review_serve", "review_ready", "opinion_answer",
        "helper_ask", "helper_answer", "partner_answer", "channel_post", "channel_read", "handover_propose", "line_read", "knowledge_refresh",
        "question_ask", "memory_write", "memory_end", "memory_propose",
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
    /// commit, a branch or a tag, each also in the option-first form a rule must name apart.
    /// </summary>
    private static readonly string[] Writing =
    [
        "Edit", "MultiEdit", "Write", "NotebookEdit",
        .. new[] { "commit", "merge", "rebase", "cherry-pick", "revert", "reset", "tag", "am", "apply", "stash", "switch", "checkout" }
            .SelectMany(act => new[] { $"Bash(git {act}:*)", $"Bash(git -* {act})", $"Bash(git -* {act} *)" }),
    ];

    /// <summary>
    /// What auto mode's classifier is told beside the rules: the forms no rule can name. Handed after the harness's
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
    /// <summary>
    /// What a helper's session is told to the classifier beside the rules: the reviewer's words for a helper, which
    /// reads one question's answer and changes nothing.
    /// </summary>
    public const string HelperHardDeny =
        "Changing anything while helping another session: editing, writing, moving or deleting a file in any form; committing, "
        + "merging, rebasing, resetting, or making or moving a branch or a tag; pushing; deploying or publishing; changing or "
        + "proposing a permission rule; starting, stopping or touching a process, a port or a server of the person's; taking, "
        + "closing, declining or publishing a quest; and asking another agent for help. A helper reads and answers, and changes nothing.";

    /// <param name="helper">
    /// A helper's session rather than a reviewer's: the same read-only shape with its one tool <c>helper_answer</c>
    /// in place of <c>opinion_give</c>, which it is denied, as is <c>helper_ask</c> (a helper asks for no helper).
    /// </param>
    public static RuleLists Shape(RuleLists composed, bool helper = false)
    {
        string[] offered = helper ? [.. Offered.Where(tool => tool != "opinion_give"), "helper_answer"] : Offered;
        string[] acting = helper ? [.. Acting.Where(tool => tool != "helper_answer"), "opinion_give"] : Acting;
        var withheld = new HashSet<string>(
            PermissionRules.Defaults.Where(shipped => shipped.Id == "commit").SelectMany(shipped => shipped.Rules)
                .Concat(acting.Select(tool => Connector + tool)),
            StringComparer.Ordinal);
        // `cd` is its reading's prefix too, so the commit default's one stays given.
        withheld.Remove("Bash(cd:*)");

        IReadOnlyList<string> allow = [.. composed.Allow.Where(rule => !withheld.Contains(rule))];
        var shaped = new RuleLists(allow, composed.Ask, composed.Deny);
        return shaped.Joined(new RuleLists(
            [.. offered.Select(tool => Connector + tool), .. Reading],
            [],
            [.. Writing, .. acting.Select(tool => Connector + tool)]));
    }
}
