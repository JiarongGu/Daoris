using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>What became of a go-ahead (KNOWUSE1a, D135 §2).</summary>
public enum GoAheadState
{
    /// <summary>Asked, and the person has not answered it yet.</summary>
    Asked,

    /// <summary>The person said yes to the act it names.</summary>
    Approved,

    /// <summary>The person said no.</summary>
    Refused,
}

/// <summary>One session's request for a go-ahead (KNOWUSE1a): who asked it, on which quest, when, and why.</summary>
/// <param name="Session">The session that asked.</param>
/// <param name="Quest">The quest it worked; null for an intake, which works none.</param>
/// <param name="At">When it asked.</param>
/// <param name="Why">Why the work needs it, in the session's words.</param>
public sealed record GoAheadRequest(string Session, string? Quest, DateTimeOffset At, string Why);

/// <summary>The person's answer to a go-ahead (KNOWUSE1a): yes or no, their words where they gave any, and when.</summary>
public sealed record GoAheadAnswer(bool Approved, string? Words, DateTimeOffset At);

/// <summary>
/// A go-ahead held on an ask (KNOWUSE1a, D135 §2): the person's yes for one act outside a repository, asked once and named
/// by the act — its kind, where it lands and what it touches — with every session's request for it and the person's answer.
/// </summary>
/// <param name="Number">Its number on the ask, from 1, in the order asked: how a session and the person name it.</param>
/// <param name="Kind">One of <see cref="GoAheadAct.Kinds"/>.</param>
/// <param name="On">Where it lands, as <see cref="GoAheadAct.Where"/> reads it: an environment's one name, or the system's words.</param>
/// <param name="Act">What it touches, in the words of the session that first asked it.</param>
public sealed record GoAhead(int Number, string Kind, string On, string Act)
{
    /// <summary>Each session's request for it, oldest first: the first asked it, and each later one joined it.</summary>
    public IReadOnlyList<GoAheadRequest> Asked { get; init; } = [];

    /// <summary>The person's answer, the latest where they answered twice; null while it waits on them.</summary>
    public GoAheadAnswer? Answer { get; init; }

    /// <summary>
    /// The go-ahead whose words this one's shared without the words telling whether it was the same act, where that is
    /// why it was asked once more; null for an act asked on its own.
    /// </summary>
    public int? Near { get; init; }

    public GoAheadState State => Answer is null ? GoAheadState.Asked : Answer.Approved ? GoAheadState.Approved : GoAheadState.Refused;

    /// <summary>A state's spelling, on the wire.</summary>
    public static string Spell(GoAheadState state) => state.ToString().ToLowerInvariant();

    /// <summary>The act in one line, as every door says it: <c>write on production: "dashboard configuration"</c>.</summary>
    public string Named => $"{Kind} on {On}: \"{Act}\"";
}

/// <summary>How a request reads against a go-ahead already asked (KNOWUSE1a).</summary>
public enum GoAheadMatch
{
    /// <summary>Another act: another kind, another place, or no word of what it touches shared.</summary>
    Other,

    /// <summary>The same act: its kind and place, and every word of what the earlier one touches.</summary>
    Same,

    /// <summary>Its kind and place, and some of its words but not all: the words cannot tell.</summary>
    Unclear,
}

/// <summary>
/// How an act is named, so two wordings of one act join (KNOWUSE1a, D135 §2) — with no model, by words only (D24).
/// </summary>
/// <remarks>
/// <para><b>Three parts, from the evidence's thirteen asks</b> (`docs/2026-10-03-knowledge-use-evidence.md` §4): one
/// production configuration write asked three times, display writes asked four times, and one report's production entries
/// and release asked six. Each named a kind of act (a write, a release, a sign-in, a push, a run against a system's data),
/// where it lands (production, every time), and what it touches (the configuration, the display, the menu entries). The
/// kind and the place are read from their usual names; what it touches is read by its words.</para>
///
/// <para><b>A later request joins when it names every word of the earlier act</b>, which it may make more precise: "menu
/// entry" is joined by "the comparison report's menu entries". The reverse is not joined: a request naming only some of
/// an earlier act's words may be a part of it or another act, and the person's yes to the one they read must not stretch
/// to a broader one. Where the words cannot tell, it is asked once more, naming the near one (<see cref="GoAheadMatch.Unclear"/>).</para>
/// </remarks>
public static class GoAheadAct
{
    /// <summary>The five kinds, as the wire spells them.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["write", "release", "push", "sign-in", "run"];

    /// <summary>Each kind's usual names, read with case, spaces, hyphens and underscores aside.</summary>
    private static readonly IReadOnlyDictionary<string, string> KindNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["write"] = "write", ["put"] = "write", ["change"] = "write", ["update"] = "write", ["edit"] = "write",
        ["release"] = "release", ["deploy"] = "release", ["publish"] = "release", ["ship"] = "release",
        ["push"] = "push", ["pullrequest"] = "push", ["pr"] = "push",
        ["signin"] = "sign-in", ["login"] = "sign-in", ["logon"] = "sign-in",
        ["run"] = "run", ["execute"] = "run",
    };

    /// <summary>The words a kind's own name is said in, left out of what an act touches where they say the act's kind again.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> KindWords = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["write"] = ["write", "put", "change", "update", "edit"],
        ["release"] = ["release", "deploy", "publish", "ship"],
        ["push"] = ["push"],
        ["sign-in"] = ["sign", "signin", "login", "logon", "log"],
        ["run"] = ["run", "execute"],
    };

    /// <summary>An environment's usual names, each read as its one name.</summary>
    private static readonly IReadOnlyDictionary<string, string> Environments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["production"] = "production", ["prod"] = "production", ["prd"] = "production", ["live"] = "production",
        ["development"] = "development", ["dev"] = "development",
        ["staging"] = "staging", ["stage"] = "staging",
        ["test"] = "test", ["testing"] = "test", ["qa"] = "test", ["uat"] = "test",
        ["local"] = "local", ["localhost"] = "local",
    };

    /// <summary>The small words that name nothing an act touches.</summary>
    private static readonly HashSet<string> Small = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "of", "to", "for", "in", "on", "at", "by", "with", "and", "or", "its", "it", "this", "that",
        "these", "those", "from", "into", "as", "be", "is", "are", "my", "our", "your", "their",
    };

    /// <summary>A kind as the five spell it, from its usual names; null for one that is none of them.</summary>
    public static string? Kind(string? said)
    {
        var squeezed = new string((said ?? "").ToLowerInvariant().Where(c => !char.IsWhiteSpace(c) && c is not '-' and not '_').ToArray());
        return KindNames.GetValueOrDefault(squeezed);
    }

    /// <summary>
    /// Where an act lands: the one name of the first environment it names, else its own words without the small ones,
    /// lower-case and one space apart. Empty where it names nothing.
    /// </summary>
    public static string Where(string? said)
    {
        var words = Split(said);
        foreach (var word in words)
        {
            if (Environments.TryGetValue(word, out var environment)) return environment;
        }

        return string.Join(" ", words.Where(word => !Small.Contains(word)));
    }

    /// <summary>
    /// What an act touches, by its words: lower-case, split at anything that is not a letter or a digit, without the small
    /// words, single letters (a possessive's <c>s</c>), a plural's ending, and the words that say its own kind or place again.
    /// </summary>
    public static IReadOnlySet<string> Words(string? act, string kind, string on)
    {
        var again = new HashSet<string>(KindWords.GetValueOrDefault(kind) ?? [], StringComparer.Ordinal);
        foreach (var (name, environment) in Environments)
        {
            if (environment == on) again.Add(name);
        }

        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in Split(act))
        {
            if (Small.Contains(word) || again.Contains(word)) continue;
            if (word.Length == 1 && !char.IsDigit(word[0])) continue;
            words.Add(Singular(word));
        }

        return words;
    }

    /// <summary>How a request reads against a go-ahead already asked: the same act, another, or one the words cannot tell.</summary>
    public static GoAheadMatch Match(GoAhead earlier, string kind, string on, string act)
    {
        var laterKind = Kind(kind) ?? kind;
        var laterOn = Where(on);
        var earlierKind = Kind(earlier.Kind) ?? earlier.Kind;
        var earlierOn = Where(earlier.On);
        if (laterKind != earlierKind || laterOn != earlierOn) return GoAheadMatch.Other;

        var named = Words(earlier.Act, earlierKind, earlierOn);
        var later = Words(act, laterKind, laterOn);
        if (named.Count == 0) return GoAheadMatch.Other;
        if (named.IsSubsetOf(later)) return GoAheadMatch.Same;
        return named.Overlaps(later) ? GoAheadMatch.Unclear : GoAheadMatch.Other;
    }

    private static List<string> Split(string? said)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in (said ?? "").ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
                continue;
            }

            if (word.Length > 0) words.Add(word.ToString());
            word.Clear();
        }

        if (word.Length > 0) words.Add(word.ToString());
        return words;
    }

    /// <summary>A plural's ending, read off where it plainly is one: <c>entries</c> is <c>entry</c>, <c>tiles</c> <c>tile</c>.</summary>
    private static string Singular(string word) =>
        word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal) ? word[..^3] + "y"
        : word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1]
        : word;
}

/// <summary>Why a session's request for a go-ahead was not kept (KNOWUSE1a) — or <see cref="None"/> when it was.</summary>
public enum GoAheadRefusal
{
    None,

    /// <summary>No place, nothing it touches, or no reason.</summary>
    Empty,

    /// <summary>A kind none of the five name.</summary>
    BadKind,

    /// <summary>Past a bound: a part's length, or as many go-aheads as one ask holds.</summary>
    TooMuch,

    /// <summary>No session under that id of this machine's.</summary>
    NotFound,

    /// <summary>The session is on no ask held here, so there is no ask to hold the go-ahead on.</summary>
    NoAsk,
}

/// <summary>What became of a request (KNOWUSE1a).</summary>
public enum GoAheadJoin
{
    /// <summary>An act not asked before on the ask: asked now.</summary>
    New,

    /// <summary>An act already asked: the request joined it, and nothing new was asked of the person.</summary>
    Joined,

    /// <summary>Its words shared some of an earlier act's without telling whether it was that act: asked once more.</summary>
    Unmatched,
}

/// <param name="Refusal"><see cref="GoAheadRefusal.None"/> when the request was kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask it was held on, when it was.</param>
/// <param name="GoAhead">The go-ahead it was kept on, as it now stands, when it was.</param>
/// <param name="Join">Whether it was asked anew, joined one, or asked once more.</param>
public sealed record GoAheadOutcome(
    GoAheadRefusal Refusal, string Message, string? Ask = null, GoAhead? GoAhead = null, GoAheadJoin Join = GoAheadJoin.New);

/// <summary>Why the person's answer to a go-ahead was not kept (KNOWUSE1a) — or <see cref="None"/> when it was.</summary>
public enum GoAheadAnswerRefusal
{
    None,

    /// <summary>No ask under that id.</summary>
    NotFound,

    /// <summary>The ask holds no go-ahead by that number.</summary>
    NoGoAhead,

    /// <summary>Their words past the bound.</summary>
    TooLong,
}

/// <param name="Refusal"><see cref="GoAheadAnswerRefusal.None"/> when the answer was kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask as it now stands, when there is one.</param>
public sealed record GoAheadAnswerOutcome(GoAheadAnswerRefusal Refusal, string Message, Ask? Ask);

/// <summary>The bounds and the stored shape of go-aheads (KNOWUSE1a).</summary>
public static class GoAheads
{
    /// <summary>The most characters of a session's reason, or of the person's words on an answer.</summary>
    public const int WordsLimit = 2_000;

    /// <summary>The most characters of where an act lands, or of what it touches: a name, not a paragraph.</summary>
    public const int ActLimit = 300;

    /// <summary>The most go-aheads one ask holds.</summary>
    public const int Most = 50;

    /// <summary>A moment as a door says it: to the minute, in UTC, the same on every machine.</summary>
    internal static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    /// <summary>
    /// The go-aheads an ask's column holds, oldest first. One this build cannot read — a newer kind, a half-written entry —
    /// is passed over, never a failed read of the ask.
    /// </summary>
    internal static IReadOnlyList<GoAhead> Read(string json) => [.. Entries(json).Select(entry => entry.Read).OfType<GoAhead>()];

    /// <summary>Every entry as stored, each with what this build reads of it, so a write keeps what it cannot read.</summary>
    internal static IReadOnlyList<(JsonElement Raw, GoAhead? Read)> Entries(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        return [.. document.RootElement.EnumerateArray().Select(element => (element.Clone(), ReadOne(element)))];
    }

    private static GoAhead? ReadOne(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (JsonFields.Number(element, "number") is not { } number || number < 1 || number > int.MaxValue) return null;
        if (JsonFields.Text(element, "kind") is not { } kind || !GoAheadAct.Kinds.Contains(kind)) return null;
        if (JsonFields.Text(element, "on") is not { Length: > 0 } on || JsonFields.Text(element, "act") is not { Length: > 0 } act) return null;

        var asked = new List<GoAheadRequest>();
        foreach (var request in JsonFields.Items(element, "asked"))
        {
            if (JsonFields.Text(request, "session") is not { Length: > 0 } session || Moment(request, "at") is not { } at) continue;
            asked.Add(new GoAheadRequest(session, JsonFields.Text(request, "quest"), at, JsonFields.Text(request, "why") ?? ""));
        }

        GoAheadAnswer? answer = null;
        if (element.TryGetProperty("answer", out var answered) && answered.ValueKind == JsonValueKind.Object
            && answered.TryGetProperty("approved", out var approved) && approved.ValueKind is JsonValueKind.True or JsonValueKind.False
            && Moment(answered, "at") is { } when)
        {
            answer = new GoAheadAnswer(approved.GetBoolean(), JsonFields.Text(answered, "words"), when);
        }

        return new GoAhead((int)number, kind, on, act)
        {
            Asked = asked,
            Answer = answer,
            Near = JsonFields.Number(element, "near") is { } near and > 0 and <= int.MaxValue ? (int)near : null,
        };
    }

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        DateTimeOffset.TryParse(JsonFields.Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    /// <summary>One go-ahead as stored: hand-written, as every store's lists are, so nothing stops working under AOT.</summary>
    internal static void Write(Utf8JsonWriter writer, GoAhead goAhead)
    {
        writer.WriteStartObject();
        writer.WriteNumber("number", goAhead.Number);
        writer.WriteString("kind", goAhead.Kind);
        writer.WriteString("on", goAhead.On);
        writer.WriteString("act", goAhead.Act);
        writer.WriteStartArray("asked");
        foreach (var request in goAhead.Asked)
        {
            writer.WriteStartObject();
            writer.WriteString("session", request.Session);
            if (request.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteString("at", request.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("why", request.Why);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (goAhead.Near is { } near) writer.WriteNumber("near", near);
        if (goAhead.Answer is { } answer)
        {
            writer.WriteStartObject("answer");
            writer.WriteBoolean("approved", answer.Approved);
            if (answer.Words is { } words) writer.WriteString("words", words);
            writer.WriteString("at", answer.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>The entries written back: each read one as it now stands, and each this build could not read as it was.</summary>
    internal static string Written(IEnumerable<(JsonElement Raw, GoAhead? Read)> entries) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var (raw, read) in entries)
        {
            if (read is null) raw.WriteTo(writer);
            else Write(writer, read);
        }

        writer.WriteEndArray();
    });

    /// <summary>The highest number any entry holds, a read one or not, so a new go-ahead never takes a number in use.</summary>
    internal static int Highest(IEnumerable<(JsonElement Raw, GoAhead? Read)> entries) =>
        entries.Select(entry => entry.Read?.Number
                ?? (entry.Raw.ValueKind == JsonValueKind.Object && JsonFields.Number(entry.Raw, "number") is { } n and > 0 and <= int.MaxValue
                    ? (int)n
                    : 0))
            .DefaultIfEmpty(0)
            .Max();
}
