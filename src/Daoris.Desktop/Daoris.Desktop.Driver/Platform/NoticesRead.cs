using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// A press waiting for the person's confirmation, as the terminal says it (NOTIFY2i): by its act and the session it is about, never a value
/// it carries. <see cref="Source"/> is <c>terminal</c> for a press sent over the pipe, <c>service</c> for one at the service's door.
/// </summary>
/// <param name="Act">A terminal's press by its route, its module before it where the module is not the driver's (as a notice names one); the
/// service's by its own act.</param>
/// <param name="Session">The session a terminal's press is about, where its wording names one.</param>
public sealed record PressWaiting(
    string Source, string Id, string Act, string? Session, DateTimeOffset? Asked, DateTimeOffset Expires, bool Shown);

/// <summary>A row of <i>What needs you</i> as the terminal says it: its group, its kind, what it is about, and its first line of words.</summary>
/// <param name="Subject">The session, quest, ask or id it is about, the first of them it names.</param>
/// <param name="Says">Its note's, detail's or title's first line, where it has one.</param>
public sealed record NeedWaiting(string Group, string Kind, string Subject, string? Workspace, DateTimeOffset? Since, string? Says);

/// <summary>An ask with messages past the person's mark (NOTIFY2g's <c>messages</c> partition): the service's count and its newest.</summary>
public sealed record AskUnread(string Ask, string? Workspace, bool Teammate, int Unread, string? Who, string? Line, DateTimeOffset? At);

/// <summary>
/// What came to the person, as <c>daoris-driver notices</c> reads it (NOTIFY2i): the running Daoris's presses, rows and messages where it
/// answered, null where they were not read; and the notices of the record either way.
/// </summary>
public sealed record NoticesLook(
    IReadOnlyList<PressWaiting>? Presses, IReadOnlyList<NeedWaiting>? Needs, IReadOnlyList<AskUnread>? Messages, IReadOnlyList<Notice> Notices)
{
    /// <summary>Whether the live groups were read: through the running Daoris, never from the record alone.</summary>
    public bool Live => Presses is not null;
}

/// <summary>
/// <c>daoris-driver notices</c> (NOTIFY2i, D196 point 8, the notification design §9): what came to the person, read and never changed — what
/// waits for their confirmation, what needs them by group, unread messages by ask, and <i>Done and failed</i>, as text or <c>--json</c>.
/// </summary>
/// <remarks>
/// <para><b>Through the running Daoris</b>, the open read <c>NOTIFICATIONS</c> answers these lines from what its windows hold (the
/// confirmation channel, the <c>attention</c> and <c>notices</c> slices), so the terminal says what the bell counts. <b>Alone</b>, the
/// notices' record is all there is: the presses, what needs the person and the asks' messages are judged in the running Daoris, and are
/// said not read rather than judged here.</para>
/// <para><b>It changes nothing</b>: nothing is marked read, dismissed or answered. A notice read in a window is listed as read.</para>
/// </remarks>
public static class NoticesRead
{
    /// <summary>The verb's usage line.</summary>
    public const string Usage = "usage: daoris-driver notices [--json]";

    /// <summary>The groups of <i>What needs you</i> in the order the view shows them, with their words.</summary>
    public static IReadOnlyList<(string Group, string Words)> Groups { get; } =
        [("holding", "Holding work"), ("word", "Waiting for your word"), ("ready", "Ready for you")];

    /// <summary>
    /// Each kind's group, the page's <c>ATTENTION_GROUP</c> (<c>work/attention/attention.ts</c>), held to it by a test; a kind neither
    /// names waits for the person's word, as the page reads it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> KindGroups { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["parked"] = "holding", ["parked-quest"] = "holding", ["go-ahead"] = "holding", ["permission"] = "holding",
        ["answer"] = "holding", ["confirmation"] = "holding", ["trust"] = "holding", ["account-wait"] = "holding",
        ["signed-out"] = "holding", ["quiet"] = "holding", ["proposal"] = "word", ["intake"] = "word", ["departure"] = "word", ["set-up"] = "word",
        ["opinion"] = "word", ["rule"] = "word", ["helper"] = "word", ["hand-over"] = "word", ["work"] = "word",
        ["space"] = "word", ["unanswerable"] = "word", ["git-held"] = "word", ["review"] = "ready",
    };

    /// <summary>
    /// A row's group, the page's <c>attentionGroup</c>: its kind's, but a hand-over ask whose session stands still on it, which holds work,
    /// and a stale go-ahead, which waits for the person's word.
    /// </summary>
    public static string Group(string kind, bool stands, bool stale) =>
        kind == "hand-over" && stands ? "holding"
        : kind == "go-ahead" && stale ? "word"
        : KindGroups.GetValueOrDefault(kind, "word");

    /// <summary>The look from the record alone: the notices, the live groups not read. Null where the record does not read this moment.</summary>
    public static NoticesLook? Alone(string home, DateTimeOffset now) =>
        Notices.Read(home, now) is { } notices ? new(null, null, null, notices) : null;

    /// <summary>
    /// The verb: through the running Daoris where one answers <c>NOTIFICATIONS</c>, else from the record alone, in the same words.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, string home, InstanceWorld? instance, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        if (args.Any(arg => arg != "--json"))
        {
            error.WriteLine($"notices: {Usage}");
            return 2;
        }

        var json = args.Contains("--json");
        if (instance is not null && await InstanceDoor.TryReadAsync(
                OpenRoutes.Notifications, new { json, said = true }, TreesSaid.ReadWords(output, instance.Error), instance, ct)
            .ConfigureAwait(false) is { } viaInstance)
        {
            return viaInstance;
        }

        var said = Said(Alone(home, DateTimeOffset.UtcNow), json, DateTimeOffset.UtcNow);
        said.Print(output, error);
        return said.Code;
    }

    /// <summary>
    /// What the verb prints of <paramref name="look"/>, which the instance's <c>NOTIFICATIONS</c> answers as <c>said</c>: lines, or with
    /// <paramref name="json"/> one element, whole. A record that did not read is said, exit 2.
    /// </summary>
    public static TreesSaid Said(NoticesLook? look, bool json, DateTimeOffset now)
    {
        if (look is null)
        {
            return new([], ["notices: the notices' record did not read this moment (a write under way, or a file that does not parse); run this again."], 2);
        }

        return new(json ? [Json(look).TrimEnd('\n')] : Lines(look, now), [], 0);
    }

    /// <summary>The text form: each group that holds anything, under its head with its count.</summary>
    public static IReadOnlyList<string> Lines(NoticesLook look, DateTimeOffset now)
    {
        var lines = new List<string>();
        if (!look.Live)
        {
            lines.Add("notices: read without the running Daoris, from the notices' record alone. What waits for your confirmation, what "
                      + "needs you and your asks' messages are the running Daoris's to read, and are not read here.");
        }

        if (look.Presses is { Count: > 0 } presses)
        {
            lines.Add($"Waiting for your confirmation ({presses.Count})");
            foreach (var press in presses.OrderBy(press => press.Expires)) lines.Add($"  {Press(press, now)}");
        }

        if (look.Needs is { Count: > 0 } needs)
        {
            lines.Add($"What needs you ({needs.Count})");
            foreach (var (group, words) in Groups)
            {
                var held = needs.Where(need => need.Group == group).ToList();
                if (held.Count == 0) continue;
                lines.Add($"  {words} ({held.Count})");
                foreach (var need in held) lines.Add($"    {Need(need, now)}");
            }
        }

        var answered = look.Notices.Where(notice => notice.Kind == Notices.Answered && !notice.Read).ToList();
        var asks = (look.Messages ?? []).Where(ask => ask.Unread > 0).ToList();
        if (asks.Count + answered.Count > 0)
        {
            lines.Add($"Messages ({asks.Sum(ask => ask.Unread) + answered.Count} unread)");
            foreach (var ask in asks) lines.Add($"  {Ask(ask)}");
            foreach (var notice in answered) lines.Add($"  {Join("answered", notice.Session, notice.Repository, notice.Workspace, Ago(notice.At, now))}");
        }

        var done = look.Notices.Where(notice => notice.Kind != Notices.Answered).ToList();
        if (done.Count > 0)
        {
            lines.Add($"Done and failed ({done.Count(notice => !notice.Read)} unread)");
            foreach (var notice in done) lines.Add($"  {(notice.Read ? " " : "•")} {Done(notice, now)}");
        }

        if (lines.Count == (look.Live ? 0 : 1))
        {
            lines.Add(look.Live ? "notices: nothing waits for you, and nothing is unread." : "notices: the record holds no notice.");
        }

        return lines;
    }

    private static string Press(PressWaiting press, DateTimeOffset now)
    {
        var left = press.Expires - now;
        var time = left <= TimeSpan.Zero ? "its time is up"
            : left.TotalMinutes >= 1 ? $"{(int)left.TotalMinutes}m {left.Seconds}s left"
            : $"{left.Seconds}s left";
        return Join(
            press.Act, press.Session is { } session ? $"session {session}" : null,
            press.Source == "service" ? "at the service's door" : "from a terminal",
            press.Source == "service" ? null : press.Shown ? "shown" : "not shown yet", time);
    }

    private static string Need(NeedWaiting need, DateTimeOffset now)
    {
        var line = Join(need.Kind, need.Subject, need.Workspace, need.Since is { } since ? Ago(since, now) : null);
        return need.Says is { Length: > 0 } says ? $"{line} — {says}" : line;
    }

    private static string Ask(AskUnread ask)
    {
        var line = Join(ask.Teammate ? $"a teammate's ask {ask.Ask}" : $"ask {ask.Ask}", ask.Workspace, $"{ask.Unread} unread");
        return ask.Line is { Length: > 0 } said ? $"{line} — {ask.Who ?? "agent"}: {said}" : line;
    }

    private static string Done(Notice notice, DateTimeOffset now) => Join(
        (notice.State ?? notice.Code) is { } what ? $"{notice.Kind} {what}" : notice.Kind,
        notice.Route ?? notice.Session ?? notice.Quest, notice.Repository, notice.Workspace, Ago(notice.At, now));

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>How long ago, in the terminal's short words.</summary>
    private static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var span = at > now ? TimeSpan.Zero : now - at;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h ago"
            : $"{(int)span.TotalDays}d ago";
    }

    /// <summary>
    /// The <c>--json</c> form: <c>{ from, presses, needs, messages, notices }</c>, <c>from</c> <c>instance</c> or <c>record</c>, each live
    /// group null where it was not read, and every notice of the record, read or not.
    /// </summary>
    public static string Json(NoticesLook look) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("from", look.Live ? "instance" : "record");
        Array("presses", look.Presses, press =>
        {
            writer.WriteString("source", press.Source);
            writer.WriteString("id", press.Id);
            writer.WriteString("act", press.Act);
            Optional("session", press.Session);
            Optional("asked", press.Asked is { } asked ? Moment(asked) : null);
            writer.WriteString("expires", Moment(press.Expires));
            writer.WriteBoolean("shown", press.Shown);
        });
        Array("needs", look.Needs, need =>
        {
            writer.WriteString("group", need.Group);
            writer.WriteString("kind", need.Kind);
            writer.WriteString("subject", need.Subject);
            Optional("workspace", need.Workspace);
            Optional("since", need.Since is { } since ? Moment(since) : null);
            Optional("says", need.Says);
        });
        Array("messages", look.Messages, ask =>
        {
            writer.WriteString("ask", ask.Ask);
            Optional("workspace", ask.Workspace);
            writer.WriteBoolean("teammate", ask.Teammate);
            writer.WriteNumber("unread", ask.Unread);
            Optional("who", ask.Who);
            Optional("line", ask.Line);
            Optional("at", ask.At is { } at ? Moment(at) : null);
        });
        Array("notices", look.Notices, notice =>
        {
            writer.WriteString("id", notice.Id);
            writer.WriteString("kind", notice.Kind);
            writer.WriteString("at", Moment(notice.At));
            writer.WriteBoolean("read", notice.Read);
            Optional("workspace", notice.Workspace);
            Optional("session", notice.Session);
            Optional("quest", notice.Quest);
            Optional("repository", notice.Repository);
            Optional("state", notice.State);
            Optional("route", notice.Route);
            Optional("act", notice.Act);
            Optional("code", notice.Code);
        });
        writer.WriteEndObject();

        void Array<T>(string name, IReadOnlyList<T>? items, Action<T> write)
        {
            if (items is null)
            {
                writer.WriteNull(name);
                return;
            }

            writer.WriteStartArray(name);
            foreach (var item in items)
            {
                writer.WriteStartObject();
                write(item);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        void Optional(string name, string? value)
        {
            if (value is not null) writer.WriteString(name, value);
        }
    });

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
