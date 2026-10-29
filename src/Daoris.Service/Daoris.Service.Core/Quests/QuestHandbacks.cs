using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// A quest's handback (HANDBACK1, D219): the note a session gave through its connector as it closed the quest done, said it ready or
/// declined it, as the operation that made that act keeps it, with how it answered each of the person's requirements, when, on which
/// machine, and the session that wrote it. Written once, at that act, in the same write as the move; read back from the quest's
/// history, never from a session's event log. It travels with the quest, as every operation does.
/// </summary>
/// <param name="Quest">The quest it hands back.</param>
/// <param name="Act">The act it was written at: a done, a ready's submission, or a decline.</param>
/// <param name="Note">The note whole, as the session gave it.</param>
/// <param name="Answers">How it answered each of the person's requirements; empty where the quest carries none, and on a decline.</param>
/// <param name="At">When, on the clock of the machine that made it.</param>
/// <param name="Machine">The machine that made it.</param>
/// <param name="Session">The session that wrote it, as its connector's intake named it.</param>
public sealed record QuestHandback(
    string Quest, QuestOperationKind Act, string? Note, IReadOnlyList<QuestAnswer> Answers, DateTimeOffset At, string Machine, string Session)
{
    /// <summary>The act as the wire spells it: <c>done</c>, <c>ready</c> or <c>declined</c>.</summary>
    public string Spelled => Act switch
    {
        QuestOperationKind.Submitted => "ready",
        QuestOperationKind.Declined => "declined",
        _ => "done",
    };
}

/// <summary>A quest's handback read from its history (HANDBACK1, D219).</summary>
public static class QuestHandbacks
{
    /// <summary>
    /// The handback <paramref name="history"/> holds: the latest done, ready or decline the replay applied that names the session that
    /// wrote it. A done the graph's close wrote names none, since its note is the submission's it closed on, so a graph-closed quest's
    /// handback stays that submission. Null where no session's close is recorded: a quest still open or taken with none, and one closed
    /// before HANDBACK1 or through a door that names no session.
    /// </summary>
    public static QuestHandback? Of(IEnumerable<QuestOperation> history)
    {
        QuestHandback? found = null;
        Quest? quest = null;
        foreach (var operation in history)
        {
            if (!QuestLog.Applies(quest, operation)) continue;
            if (QuestStore.KeepsSession(operation.Kind) && operation is { Session: { Length: > 0 } session, Close: null })
            {
                found = new(operation.Quest, operation.Kind, operation.Note, operation.Answers ?? [], operation.At, operation.Machine, session);
            }

            quest = QuestLog.Step(quest, operation);
        }

        return found;
    }
}

/// <summary>
/// One quest's handback as the read answers it (HANDBACK1, D219): what the id named, and the words, phrased once for every door.
/// </summary>
/// <param name="Id">The id asked, as read.</param>
/// <param name="Quest">The quest read, or null where the id names none, or names one outside the workspace asked.</param>
/// <param name="Session">The session the id named, where it named one.</param>
/// <param name="Handback">The quest's handback, where a session's close is recorded on it.</param>
/// <param name="Said">The answer in words: what the connector answers, and the terminal prints.</param>
public sealed record HandbackReading(string Id, Quest? Quest, Session? Session, QuestHandback? Handback, string Said)
{
    /// <summary>Whether the id named a quest or a session here, inside the workspace asked.</summary>
    public bool Found => Quest is not null || Session is not null;

    /// <summary>Whether the id named a quest or a session of another workspace than the one asked, which the read refuses.</summary>
    public bool Outside { get; init; }
}

/// <summary>
/// One quest's handback as anyone entitled to the work reads it (HANDBACK1, D219): its steward, another session of the workspace, a
/// terminal. By the quest's id, or by the id of a session for the quest it served; in any state, open or closed; from one workspace,
/// as <c>quest_list</c> scopes it, or from every one. The connector's <c>handback_read</c>, the local host's <c>GET /api/handbacks/{id}</c>
/// and <c>daoris-driver sessions handback</c> answer through it.
/// </summary>
/// <param name="quests">Where the quest and its history are read.</param>
/// <param name="ledger">Where a session's record is read; none reads every id as a quest's.</param>
public sealed class HandbackReader(QuestStore quests, SessionLedger? ledger = null)
{
    /// <summary>What <paramref name="id"/> hands back, from <paramref name="workspace"/>, or from every workspace where it is null.</summary>
    public async Task<HandbackReading> ReadAsync(string id, string? workspace = null, CancellationToken ct = default)
    {
        var named = id.Trim().TrimStart('#');
        if (named.Length > 0 && await quests.FindAsync(named, ct).ConfigureAwait(false) is { } quest)
        {
            if (Elsewhere(quest.Workspace, workspace) is { } refused) return new(named, null, null, null, $"Refused: quest `#{quest.Id}`{refused}") { Outside = true };
            var handback = QuestHandbacks.Of(await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false));
            return new(named, quest, null, handback, Words(quest, handback));
        }

        if (named.Length > 0 && ledger is not null && await ledger.FindAsync(named, ct).ConfigureAwait(false) is { } session)
        {
            if (Elsewhere(session.Workspace, workspace) is { } refused)
            {
                return new(named, null, null, null, $"Refused: session `{session.Id}`{refused}") { Outside = true };
            }

            if ((session.Quest ?? session.TookQuest) is not { } served)
            {
                return new(named, null, session, null,
                    $"Session `{session.Id}` served no quest, so it has no handback: a handback is the note a session closes its quest with.");
            }

            if (await quests.FindAsync(served, ct).ConfigureAwait(false) is not { } work)
            {
                return new(named, null, session, null, $"Session `{session.Id}` served quest `#{served}`, which is not here, so its handback cannot be read.");
            }

            var handback = QuestHandbacks.Of(await quests.HistoryAsync(work.Id, ct).ConfigureAwait(false));
            var whose = handback is { } said && !string.Equals(said.Session, session.Id, StringComparison.Ordinal)
                ? $" Its handback is session `{said.Session}`'s, not this one's."
                : "";
            return new(named, work, session, handback, $"Session `{session.Id}` served quest `#{work.Id}`.{whose}\n\n{Words(work, handback)}");
        }

        return new(named, null, null, null, $"No quest or session `{named}` here: a quest's id comes from `quest_list`, a session's from its record.");
    }

    /// <summary>The refusal's tail for a record of another workspace than the one asked; null where it is that workspace's, or none was.</summary>
    private static string? Elsewhere(string kept, string? asked) =>
        asked is null || Workspaces.Same(kept, asked)
            ? null
            : $" is workspace `{kept}`'s, and this read answers workspace `{asked}`'s alone.";

    /// <summary>A quest's handback in words: the quest, then the handback whole with whom it is by, or what the quest keeps instead.</summary>
    private static string Words(Quest quest, QuestHandback? handback)
    {
        var text = new StringBuilder($"Quest `#{quest.Id}` **{quest.Title}** — {quest.Status}, asked of `{quest.To}` by `{quest.From}`.\n\n");
        if (handback is { } said)
        {
            text.Append($"Its handback, as session `{said.Session}` wrote it when it {Act(said.Act)} {Moment(said.At)}: that session's claim, "
                        + "so check what it names before you rely on it.");
            if (said.Act == QuestOperationKind.Submitted && quest.Status == QuestStatus.Taken) text.Append(" The quest stays taken until its graph closes it.");
            text.Append("\n\n").Append(Note(said.Note));
            Answered(text, said.Answers);
            return text.ToString();
        }

        if (quest.Submission is null && quest.Status is QuestStatus.Open or QuestStatus.Taken)
        {
            return text.Append($"No handback yet: it is {quest.Status}, and its handback is the note its session gives as it closes it done, says it ready, "
                               + "or declines it.").ToString();
        }

        text.Append("No session's handback is recorded on it: it closed before Daoris kept which session wrote a handback, or through a door "
                    + "that names none. ")
            .Append(quest.Submission is null ? "Its closing note" : "Its submission's note")
            .Append(", as the quest keeps it:\n\n")
            .Append(Note(quest.Submission is { } submitted ? submitted.Note : quest.Note));
        Answered(text, quest.Answers);
        return text.ToString();
    }

    private static string Act(QuestOperationKind act) => act switch
    {
        QuestOperationKind.Submitted => "said it ready",
        QuestOperationKind.Declined => "declined it",
        _ => "closed it done",
    };

    private static string Moment(DateTimeOffset at) => "on " + at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Note(string? note) => string.IsNullOrWhiteSpace(note) ? "It gave no note." : note;

    /// <summary>How each requirement was answered, as <c>quest_list</c> says it.</summary>
    private static void Answered(StringBuilder text, IReadOnlyList<QuestAnswer> answers)
    {
        if (answers.Count > 0) text.Append('\n');
        foreach (var answer in answers)
        {
            text.Append('\n').Append(answer.IsDeparture
                ? $"requirement {answer.Requirement} departed: {answer.Departed} Quoting \"{answer.Quote}\"."
                : $"requirement {answer.Requirement} met: {answer.Met}");
        }
    }
}

/// <summary>
/// What a session's closing note must be (HANDBACK1, D219): the handback itself, since the note is the one record of the work that
/// outlives the session's conversation, and whoever reads the quest later (its steward, another session of the workspace, a terminal)
/// reads the note and never the messages. A note that only points elsewhere is refused at <c>quest_respond</c>.
/// </summary>
/// <remarks>
/// <para><b>Narrow, by named shapes, never by length.</b> A note is read as its parts, split at a sentence's or a clause's punctuation,
/// a dash set apart, a line's end and <c>and</c>. It points elsewhere only where every part is a pointer or a bare word that says nothing
/// of the work (<c>Done</c>, <c>Handback</c>, <c>Closing as done</c>), and one part at least is a pointer. A pointer is a part whose whole
/// is a reference to another message, a commit, a change, a record, a file or a place, in one of the shapes below: <c>see the commit</c>,
/// <c>details are in my last message</c>, <c>my last message has the details</c>, <c>as described above</c>, <c>it's all in the chat</c>,
/// <c>in my last message</c>; and their Chinese forms (<c>详情见我上一条消息</c>). So a short real handback (<c>Fixed X in abc123; tests Y
/// pass</c>) is never refused, nor is a pointer beside words of its own, nor a bare <c>Done.</c> with no pointer at all.</para>
///
/// <para>Each pattern runs without backtracking, so a long note costs its length and nothing more.</para>
/// </remarks>
public static class HandbackNotes
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    /// <summary>Where a note's parts end.</summary>
    private static readonly Regex Ends = new(@"[.!?;:,](?:\s+|$)|\s+[—–-]{1,2}\s+|\r?\n|\s+and\s+|[。！？；：，、]", Options | RegexOptions.IgnoreCase);

    /// <summary>Another message of the conversation: <c>my last message</c>, <c>the summary above</c>.</summary>
    private const string Message =
        @"(?:(?:my|the|our|this|that)\s+)?(?:(?:last|previous|prior|earlier|final|preceding|latest|most recent|above|other)\s+)?"
        + @"(?:message|reply|response|answer|comment|turn|post|output|summary|hand-?back|report|write-?up|notes?)s?(?:\s+(?:above|below))?";

    /// <summary>The conversation itself, or a log of it.</summary>
    private const string Place = @"(?:(?:the|this|my|our)\s+)?(?:chat|conversation|thread|transcript|session|session log|event log|log|logs)";

    /// <summary>A commit, by name or by sha.</summary>
    private const string Commit =
        @"(?:(?:the|my|this|that|our)\s+)?(?:(?:last|latest|final|new)\s+)?commits?(?:\s+message)?(?:\s+#?[0-9a-f]{7,40})?|#?[0-9a-f]{7,40}";

    /// <summary>A change under review, or the branch.</summary>
    private const string Change = @"(?:(?:the|my|this|our)\s+)?(?:pr|pull request|merge request|branch|diff|changes)(?:\s+#?\d+)?";

    /// <summary>A record of the repository's.</summary>
    private const string Record =
        @"(?:(?:the|my|this|our)\s+)?(?:fix log|fix-log|changelog|task archive|archive|readme|docs?|documentation|design|decision|plan)(?:\s+d\d+)?|d\d+";

    /// <summary>A file, by a name with its extension or by a path.</summary>
    private const string File = @"[\w./\\-]+\.(?:md|txt|log|json|ya?ml|cs|ts|tsx|js|mjs|html)|[\w.-]+(?:/[\w.-]+)+/?";

    /// <summary>What else a note points at: an attachment, another quest, above or below.</summary>
    private const string Other = @"(?:the\s+)?attach(?:ed|ments?)|(?:the\s+)?quest\s+#?[0-9a-z]{6,}|#[0-9a-z]{6,}|above|below";

    /// <summary>Whatever a pointer points at.</summary>
    private const string Target = $"(?:{Message}|{Place}|{Commit}|{Change}|{Record}|{File}|{Other})";

    /// <summary>What a pointer says is elsewhere: the details, the handback, everything.</summary>
    private const string Label =
        @"(?:(?:the|my|full|complete|all|more|all the)\s+)*(?:details?|hand-?back|summary|report|write-?up|notes?|explanation|everything|the rest|rest"
        + "|specifics|info|information|results?|outcome|context|rationale)";

    /// <summary>Where a pointer says it is.</summary>
    private const string Where = $@"(?:(?:in|at|on|under|inside|within|from)\s+{Target}|above|below|earlier|before)";

    /// <summary>The Chinese forms' reference: my last message, the commit, above.</summary>
    private const string CjkRef =
        "(?:我的?)?(?:(?:上一?条|最后一?条|上一?个|之前的?|前面的?|上面的?)的?(?:消息|回复|留言|说明|内容|回答)|上文|上述|上面|上|提交(?:信息|记录)?|commit)(?:里|中|里面)?";

    /// <summary>The shapes of a part that is a pointer and nothing else, each matched against the whole part, lowercased.</summary>
    private static readonly Regex[] Pointers =
    [
        // See the commit · refer to my last message · read docs/handback.md for the details.
        new($@"^(?:please\s+)?(?:see|refer to|cf\.?|read)\s+{Target}"
            + @"(?:\s+for\s+(?:the\s+|all\s+the\s+|more\s+|full\s+)?(?:details?|hand-?back|summary|everything|specifics|rest|more|context|what changed))?$", Options),
        // Details are in my last message · handback above · summary in docs/handback.md.
        new($@"^{Label}(?:\s+(?:is|are|was|were|can be found|lives?|sits?|went|is given|are given|is written|are written|is included|are included))?"
            + $@"\s+{Where}$", Options),
        // My last message has the details.
        new($@"^{Target}\s+(?:has|have|holds|contains|carries|gives|says|covers|explains|lists|includes)(?:\s+(?:the|all the|all|my|full|more))?"
            + @"\s+(?:details?|hand-?back|summary|everything|it all|rest|specifics|report|context)$", Options),
        // As described above · noted in the commit.
        new($@"^(?:as\s+)?(?:described|noted|said|stated|explained|summari[sz]ed|reported|detailed|written|mentioned|listed|outlined|covered)\s+{Where}$", Options),
        // It's all in the chat · everything is in the commit message.
        new($@"^(?:it's|it is|everything's|everything is|that's|that is|all|all is)\s+(?:all\s+)?{Where}$", Options),
        // In my last message · above.
        new($@"^{Where}$", Options),
        // 详情见我上一条消息 · 交接在我最后一条消息里 · 见上.
        new("^(?:(?:详情|详细信息|详细|交接|交接内容|总结|说明|细节|具体内容|具体|结果|内容|情况)(?:都|请)?)?(?:见|在|参见|详见|参考|参阅|请看|请见|看)" + CjkRef + "$", Options),
        // 如上所述.
        new("^(?:如|同)" + CjkRef + "(?:所述|所说)?$", Options),
    ];

    /// <summary>The bare words that say nothing of the work: a part of one of these is neither a pointer nor a handback.</summary>
    private static readonly Regex[] Fillers =
    [
        new(@"^(?:(?:all|work|task|quest|it's|it is|this is|everything is)\s+)?(?:done|finished|complete|completed|closed|ready|declined|fixed)$", Options),
        new(@"^(?:closing|marking|declining|finishing|submitting)(?:\s+(?:it|this|the quest|this quest|this one))?"
            + @"(?:\s+(?:as\s+)?(?:done|ready|declined|complete|completed|finished))?$", Options),
        new(@"^(?:ok|okay|thanks|thank you|cheers)$", Options),
        new(@"^(?:the\s+|my\s+)?(?:hand-?back|summary|details|result|results|outcome|note|notes|report|status|update)$", Options),
        new("^(?:完成|已完成|完成了|好|好的|谢谢|交接|详情|总结|结果|说明|已关闭)$", Options),
    ];

    /// <summary>
    /// The pointer <paramref name="note"/> rests on, as it wrote it, where the note only points elsewhere; null where it says something
    /// of its own, says nothing that points, or is empty (a decline's missing reason is the exchange's own refusal).
    /// </summary>
    public static string? PointsElsewhere(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        string? pointer = null;
        foreach (var part in Ends.Split(note))
        {
            var shown = Shown(part);
            if (shown.Length == 0) continue;
            var read = Read(shown);
            if (Pointers.Any(shape => shape.IsMatch(read)))
            {
                pointer ??= shown;
                continue;
            }

            if (Fillers.Any(shape => shape.IsMatch(read))) continue;
            return null;
        }

        return pointer;
    }

    /// <summary>
    /// The refusal of a note that only points elsewhere: the service's words to an agent, in English as every refusal it says to one
    /// is. It quotes the pointer, says why the note is what counts, and names what a handback holds.
    /// </summary>
    public static string Refusal(string pointer) =>
        $"This note only points elsewhere (\"{pointer}\"), and a closing note is the handback itself: the one record of this work that "
        + "whoever reads the quest later reads, which none of your messages is kept with. Say the handback in the note: what changed, "
        + "where (the commits), what you proved, and what you did not. Nothing moved.";

    /// <summary>A part as a refusal quotes it: trimmed of white space and of the marks a part is wrapped in.</summary>
    private static string Shown(string part) => part.Trim().Trim('*', '_', '`', '"', '\'', '“', '”', '‘', '’', '(', ')', '[', ']', '<', '>', '#', '-', '•', ' ', '\t').Trim();

    /// <summary>A part as the shapes read it: lowercased, its apostrophes plain, every run of white space one space.</summary>
    private static string Read(string shown) =>
        Regex.Replace(shown.Replace('’', '\'').ToLowerInvariant(), @"\s+", " ").Trim().TrimEnd('.', '!', '?');
}
