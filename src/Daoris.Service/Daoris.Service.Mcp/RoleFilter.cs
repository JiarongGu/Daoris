using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// What a connector's session is, as its record says (COAGENT1b, D199 point 4; the co-agent design §7.4): the lead it serves, where it
/// is a helper's or a partner's record, and the piece it writes, where it is a partner's. Read from the ledger by the session the driver
/// named on the connector it offers (<see cref="IntakeScope.Session"/>), never from anything a call says.
/// </summary>
/// <param name="Serves">The session it serves, or null for every other session.</param>
/// <param name="Piece">The piece it writes, kept only beside <paramref name="Serves"/>, or null.</param>
public sealed record SessionRole(string? Serves, string? Piece)
{
    /// <summary>A session that serves nobody: a working session, a conversation, or a connector that names no session.</summary>
    public static readonly SessionRole None = new(null, null);

    /// <summary>Whether it is a partner's record, which writes a piece for its lead.</summary>
    public bool Partner => Serves is not null && Piece is not null;
}

/// <summary>
/// The connector's tools by role (COAGENT1b, D199 point 4; the co-agent design §7.4): what a partner may call, what it is refused, and
/// what every record that serves another is refused. The partner's two lists together name every tool the host advertises, held by a
/// census (<c>McpRoleFilterTests</c>), so a tool added later is classified when it is added.
/// </summary>
/// <remarks>
/// The refusal is the server's, by the session's record, before the tool runs: a partner on an agent that takes no rules is held to its
/// role all the same, and a native child that inherits the connector's environment inherits its refusals.
/// </remarks>
public static class ConnectorRoles
{
    /// <summary>
    /// What a partner may call: the knowledge and reading tools, its lead's line, and its answer. Nothing that publishes, asks, proposes,
    /// posts or acts.
    /// </summary>
    public static IReadOnlyList<string> PartnerAllowed { get; } =
    [
        "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "line_read", "partner_answer", "registry",
    ];

    /// <summary>Every other tool the host advertises, each refused to a partner by name.</summary>
    public static IReadOnlyList<string> PartnerRefused { get; } =
    [
        "accept_propose", "agent_propose", "agent_settings_propose", "ask_propose", "brainstorm_give", "brainstorm_synthesize",
        "browser_propose", "channel_post", "channel_read", "checkout_read", "clear_propose", "delete_propose", "go_ahead_ask",
        "go_propose", "hand_propose", "handover_propose", "helper_answer", "helper_ask", "housekeeping_clean_up",
        "housekeeping_discard_branch", "housekeeping_inspect", "knowledge_refresh", "opinion_answer", "opinion_give", "pause_propose",
        "permission_propose", "plugin_propose", "quest_list", "quest_publish", "quest_respond", "question_ask", "repository_propose",
        "review_ready", "review_serve", "session_propose", "setting_propose", "settings_cap", "settings_cooloff", "settings_language",
        "settings_notify", "settings_timeout", "setup_propose", "sync_propose", "workflow_propose",
    ];

    /// <summary>
    /// What every record that serves another is refused, a helper's as a partner's: a record with <c>serves</c> publishes, takes and
    /// closes nothing (the co-agent design §7.4).
    /// </summary>
    public static IReadOnlyList<string> ServingRefused { get; } = ["quest_publish", "quest_respond"];

    private static readonly HashSet<string> Allowed = new(PartnerAllowed, StringComparer.Ordinal);

    private static readonly HashSet<string> Serving = new(ServingRefused, StringComparer.Ordinal);

    /// <summary>
    /// The sentence that refuses <paramref name="tool"/> to <paramref name="role"/>, or null where it may be called. A partner is held
    /// to its allow-list, so a name on neither list is refused too.
    /// </summary>
    public static string? Refusal(string? tool, SessionRole role)
    {
        var name = tool ?? "";
        if (role.Partner && !Allowed.Contains(name))
        {
            return $"`{name}` is not a partner's to call: a session Daoris started to write a piece asks nobody and acts on nothing outside "
                   + "its copy. Leave your files in your copy, and say your answer with partner_answer.";
        }

        if (role.Serves is not null && Serving.Contains(name))
        {
            return $"`{name}` is refused to a session that serves another: it publishes, takes and closes no quest. Say what you found in "
                   + "your answer to the session you serve.";
        }

        return null;
    }

    /// <summary>
    /// The sentence for a call whose session's record could not be read: a reading tool still answers, and anything else is refused,
    /// since nothing could say it is allowed.
    /// </summary>
    public static string? Unread(string? tool) =>
        tool is { } name && Allowed.Contains(name)
            ? null
            : $"`{tool}` waits: Daoris could not read this session's record, so it cannot say the tool is this session's to call. Try again.";
}

/// <summary>
/// The connector's session's role, read from the ledger once it is found (a record's <c>serves</c> and <c>piece</c> are set at its open
/// and never change), and read again while it is not.
/// </summary>
/// <param name="ledger">The ledger the record is read from; none reads every session as serving nobody.</param>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
public sealed class SessionRoles(SessionLedger? ledger, IntakeScope? intake)
{
    private SessionRole? _found;

    /// <summary>The session's role; a connector that names no session, or a session with no record, serves nobody.</summary>
    public async Task<SessionRole> ReadAsync(CancellationToken ct)
    {
        if (_found is { } found) return found;
        if (ledger is null || intake?.Session is not { Length: > 0 } session) return SessionRole.None;

        var record = await ledger.FindAsync(session, ct).ConfigureAwait(false);
        if (record is null) return SessionRole.None;
        var role = new SessionRole(Blank(record.Serves), Blank(record.Serves) is null ? null : Blank(record.Piece));
        _found = role;
        return role;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// Every <c>tools/call</c> judged by the session's role before its tool runs (COAGENT1b, the co-agent design §7.4): a refused call is
/// answered with its sentence and never reaches the tool.
/// </summary>
public static class RoleFilter
{
    /// <summary>Each call refused where <paramref name="role"/>'s answer refuses it, else passed on.</summary>
    public static IMcpServerBuilder RefuseOutsideRole(this IMcpServerBuilder builder, Func<CancellationToken, Task<SessionRole>> role) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var name = request.Params?.Name;
            string? refusal;
            try
            {
                refusal = ConnectorRoles.Refusal(name, await role(cancellationToken).ConfigureAwait(false));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                refusal = ConnectorRoles.Unread(name);
            }

            return refusal is null
                ? await next(request, cancellationToken).ConfigureAwait(false)
                : new CallToolResult { Content = [new TextContentBlock { Text = refusal }] };
        }));
}
