using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Where the call layer places a connector's session (FOUNDATION1r; the foundation design §2.2, §2.3): the work its record is read as,
/// a brainstorm's turn, or why it could not be placed. Read from the session the driver named on the connector it offers
/// (<see cref="IntakeScope.Session"/>), never from anything a call says.
/// </summary>
/// <param name="Work">A <see cref="ParticipantWork"/> code, or one of the placements below.</param>
/// <param name="Turn">A brainstorm's turn, <see cref="Generator"/> or <see cref="Synthesizer"/>; null for every other work.</param>
public sealed record SessionWork(string Work, string? Turn = null)
{
    public const string Generator = "generator";
    public const string Synthesizer = "synthesizer";

    /// <summary>A connector that names no session, a person's own agent's: kept to its scope before the call layer.</summary>
    public static readonly SessionWork Unnamed = new("unnamed");

    /// <summary>A named session no record of this machine places: none is kept, or it is another machine's.</summary>
    public static readonly SessionWork Unplaced = new("unplaced");

    /// <summary>A named session whose record could not be read.</summary>
    public static readonly SessionWork Unread = new("unread");

    /// <summary>A brainstorm generator's pass, known by its turn (BRAINSTORM1a2), which keeps no record.</summary>
    public static readonly SessionWork Generating = new(ParticipantWork.Brainstorm, Generator);

    /// <summary>A brainstorm synthesis's pass (<c>s1</c>, <c>s1r</c>).</summary>
    public static readonly SessionWork Synthesizing = new(ParticipantWork.Brainstorm, Synthesizer);

    public override string ToString() => Turn is null ? Work : $"{Work}:{Turn}";
}

/// <summary>
/// The connector's tools by role (COAGENT1b, D199 point 4; the co-agent design §7.4): what a partner may call, what it is refused, and
/// what every record that serves another is refused; and a brainstorm pass, which may call its result tools alone (BRAINSTORM1a2, D176
/// point 2). The partner's two lists together name every tool the host advertises, held by a
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
        "go_propose", "hand_propose", "handback_read", "handover_propose", "helper_answer", "helper_ask", "housekeeping_clean_up",
        "housekeeping_discard_branch", "housekeeping_inspect", "knowledge_refresh", "opinion_answer", "opinion_give", "pause_propose",
        "permission_propose", "plugin_propose", "quest_list", "quest_publish", "quest_respond", "question_ask", "repository_propose",
        "review_ready", "review_serve", "role_propose", "session_propose", "setting_propose", "settings_cap", "settings_cooloff", "settings_language",
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
    public static string? Refusal(string? tool, SessionWork role)
    {
        var name = tool ?? "";
        if (role == SessionWork.Unread) return Unread(tool);
        // BRAINSTORM1a2 (D176 point 2), held here as its harness rules hold it: a pass reads its room and calls its result tool alone,
        // and the two result tools judge the turn themselves.
        if (role.Work == ParticipantWork.Brainstorm && name is not (BrainstormBox.GiveTool or BrainstormBox.SynthesizeTool))
        {
            return $"`{name}` is not a brainstorm pass's to call: a pass reads the room it was handed and gives its one answer through its "
                   + "turn's result tool, and calls nothing else.";
        }

        if (role.Work == ParticipantWork.Partner && !Allowed.Contains(name))
        {
            return $"`{name}` is not a partner's to call: a session Daoris started to write a piece asks nobody and acts on nothing outside "
                   + "its copy. Leave your files in your copy, and say your answer with partner_answer.";
        }

        if (role.Work is ParticipantWork.Help or ParticipantWork.Partner && Serving.Contains(name))
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
/// The connector's session's work (FOUNDATION1r): a brainstorm's turn placed first, else the record's work by the one rule, read once it
/// is found (the marks are set at the open and never change), and read again while it is not.
/// </summary>
/// <param name="ledger">The ledger the record is read from; none reads a named session as unread.</param>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="brainstorm">The brainstorm turn the driver named on this connector, where it offers a pass one; none names none.</param>
public sealed class SessionWorks(SessionLedger? ledger, IntakeScope? intake, BrainstormScope? brainstorm = null)
{
    private SessionWork? _found;

    /// <summary>The session's work, or its placement where no work places it.</summary>
    public async Task<SessionWork> ReadAsync(CancellationToken ct)
    {
        if (_found is { } found) return found;
        // A brainstorm pass is known by its turn, never by a record: none is opened for it (BRAINSTORM1a2).
        if (brainstorm is { Active: true } turn) return turn.Synthesis ? SessionWork.Synthesizing : SessionWork.Generating;
        if (intake?.Session is not { Length: > 0 } session) return SessionWork.Unnamed;
        if (ledger is null) return SessionWork.Unread;

        var record = await ledger.FindAsync(session, ct).ConfigureAwait(false);
        if (record is null) return SessionWork.Unplaced;
        _found = ParticipantWork.Of(record) is { } work ? new SessionWork(work) : SessionWork.Unplaced;
        return _found;
    }
}

/// <summary>
/// Every <c>tools/call</c> judged by the session's role before its tool runs (COAGENT1b, the co-agent design §7.4): a refused call is
/// answered with its sentence and never reaches the tool.
/// </summary>
public static class RoleFilter
{
    /// <summary>Each call refused where <paramref name="role"/>'s answer refuses it, else passed on.</summary>
    public static IMcpServerBuilder RefuseOutsideRole(this IMcpServerBuilder builder, Func<CancellationToken, Task<SessionWork>> role) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var name = request.Params?.Name;
            SessionWork work;
            try
            {
                work = await role(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                work = SessionWork.Unread;
            }

            var refusal = ConnectorRoles.Refusal(name, work);

            return refusal is null
                ? await next(request, cancellationToken).ConfigureAwait(false)
                : new CallToolResult { Content = [new TextContentBlock { Text = refusal }] };
        }));
}
