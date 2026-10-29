using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

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
