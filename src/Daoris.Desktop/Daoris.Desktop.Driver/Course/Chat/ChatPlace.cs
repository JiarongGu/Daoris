namespace Daoris.Driver;

/// <summary>What differs between a conversation in a repository and Ask Daoris's in its room (HELP1a).</summary>
/// <param name="Name">What the conversation is for: the repository, or <see cref="HelpRoom.Repository"/>.</param>
/// <param name="Tree">Where its process runs.</param>
/// <param name="Rules">What its agent may do, from this machine's rules file and its session id.</param>
/// <param name="Plugins">Whether the plugins' servers are handed — and Daoris's browser brought up for one.</param>
/// <param name="ConnectorOnPipe">
/// Whether the knowledge connector is handed on the pipe door too. The protocol door always carries
/// it; a repository's own `.mcp.json` wires it on the pipe, and a room has none.
/// </param>
/// <param name="Posture">The protocol door's mode for this conversation, or null for the adapter's own (D81).</param>
/// <param name="ToolsUpFront">
/// Whether its tools load with the first request, by the adapter's own switch (HELP5): true for the room,
/// whose every answer reads the family, and false for a repository, which keeps the harness's default.
/// </param>
internal sealed record ChatPlace(
    string Name, string Tree, Func<PermissionFile, string, RuleLists> Rules, bool Plugins, bool ConnectorOnPipe,
    string? Posture, bool ToolsUpFront)
{
    /// <summary>The checkouts it may also write into (D107), which its tree guard lets a write reach.</summary>
    public IReadOnlyList<string> Also { get; init; } = [];

    /// <summary>
    /// Whose plugins' servers it is handed, where <see cref="Plugins"/> hands any (HELPPLUG1a): a repository's conversation
    /// those declared for sessions, Ask Daoris's those declared for it.
    /// </summary>
    public string ServersTo { get; init; } = PluginServerTo.Sessions;

    /// <summary>
    /// Whether its harness conversation's id is kept for it (MSG1c, D137 §4.2), so the conversation goes on when the
    /// person writes to it after it ended: a repository's conversation, and since ASKHIST1 Ask Daoris's too.
    /// </summary>
    public bool Keeps { get; init; }

    /// <summary>
    /// Ask Daoris's workspace, as its record keeps it (ASKSCOPE1a): one runs at a time in each, so a new one or one going on
    /// sets aside only its own workspace's. Null for a repository's conversation.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The workspace its tools are bound to (ASKSCOPE1c, <see cref="HelpCircle"/>): Ask Daoris's room's, under either mode, so its
    /// connector answers from that workspace alone. Null for <c>default</c>'s room, the whole machine, and for a repository's.
    /// </summary>
    public string? Circle { get; init; }

    /// <summary>The native door's permission mode for it, where one is named (HELPMODE1a1, Ask Daoris in full access); null keeps the adapter's own.</summary>
    public string? PermissionMode { get; init; }

    /// <summary>
    /// Whether the plugins' servers it is handed are every one the plugins here declare, for whomever and approved or not (HELPMODE1a1,
    /// Ask Daoris in full access), rather than those <see cref="ServersTo"/> takes.
    /// </summary>
    public bool EveryServer { get; init; }

    /// <summary>
    /// Whether it is handed the tree guard where the person's rules keep it on (PERM3): every conversation, but Ask Daoris in full
    /// access (HELPMODE1a1), whose housekeeping is outside its room.
    /// </summary>
    public bool Guarded { get; init; } = true;

    /// <summary>
    /// The checkouts it may read through <c>checkout_read</c> (D107, HELPMODE1c4): Ask Daoris's in restricted mode, the readable ones
    /// the person left on, none in full access, whose own shell reads them, and none for a repository's conversation. What its
    /// receipt says it was handed (HELPWIRED1), which each call is judged by.
    /// </summary>
    public IReadOnlyList<HelpRead> Reads { get; init; } = [];

    /// <summary>The checkouts the person had turned off as its start was composed (HELPMODE1c4), what a later narrowing is compared with.</summary>
    public IReadOnlyList<string> CheckoutsOff { get; init; } = [];

    /// <summary>
    /// The housekeeping operations its start was handed (HELPMODE1c2a): Ask Daoris's, as the person chose them when the place was
    /// composed, in either mode; none for a repository's conversation. Its receipt keeps them, which each call is judged by.
    /// </summary>
    public IReadOnlyList<string> Housekeeping { get; init; } = [];

    /// <summary>The chosen settings its start was handed (HELPMODE1c3), as <see cref="Housekeeping"/> is.</summary>
    public IReadOnlyList<string> Settings { get; init; } = [];
}
