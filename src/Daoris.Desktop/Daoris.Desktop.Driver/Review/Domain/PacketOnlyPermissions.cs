namespace Daoris.Driver;

/// <summary>
/// The rules a packet's reader is handed where its agent takes Daoris's rules: every tool its harness offers denied whole, its servers'
/// among them, and nothing allowed or asked, so it reads the packet in its prompt and nothing else. A tool denied whole leaves the
/// harness's offered list, which the real-process proof reads empty (<c>PacketOnlyReaderProcessTests</c>).
/// </summary>
public static class PacketOnlyPermissions
{
    /// <summary>
    /// Every tool Claude Code offers a session, each denied whole: those the pinned binary's <c>init</c> line listed (2.1.322) and those
    /// another door or mode of it offers.
    /// </summary>
    public static IReadOnlyList<string> Tools { get; } =
    [
        "Agent", "AskUserQuestion", "Bash", "BashOutput", "CronCreate", "CronDelete", "CronList", "DesignSync", "Edit", "EnterPlanMode",
        "EnterWorktree", "ExitPlanMode", "ExitWorktree", "Glob", "Grep", "KillShell", "LS", "LSP", "ListAgents", "ListMcpResourcesTool",
        "Monitor", "MultiEdit", "NotebookEdit", "NotebookRead", "PowerShell", "PushNotification", "Read", "ReadMcpResourceTool",
        "RemoteTrigger", "ReportFindings", "ScheduleWakeup", "SendMessage", "Skill", "SlashCommand", "Task", "TaskCreate", "TaskGet",
        "TaskList", "TaskOutput", "TaskStop", "TaskUpdate", "TodoWrite", "ToolSearch", "WebFetch", "WebSearch", "Workflow", "Write",
    ];

    /// <summary>The servers a door may offer it, each denied whole: the protocol door's own file tools and Daoris's connector.</summary>
    public static IReadOnlyList<string> Servers { get; } = ["mcp__acp", $"mcp__{KnowledgeConnector.ServerName}"];

    /// <summary>What auto mode's classifier is told beside the rules.</summary>
    public const string HardDeny =
        "Using any tool while reading a packet: running any command or program, reading, searching, editing, writing or deleting any "
        + "file, fetching or searching the web, calling any server, starting a subagent or a task, and scheduling anything. The reader "
        + "reads the packet in its prompt alone and answers only in its last message.";

    /// <summary>The rules <paramref name="composed"/> becomes for a packet's reader: its denies kept, every tool and server denied beside them.</summary>
    public static RuleLists Shape(RuleLists composed) =>
        new([], [], [.. composed.Deny.Union(Tools.Concat(Servers), StringComparer.Ordinal)]);
}
