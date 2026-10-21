using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The balloon's icon, named here because these modules are plain <c>net10.0</c> and know no Windows Forms.</summary>
public enum BalloonIcon
{
    /// <summary>News: an ending or an answer.</summary>
    Info,

    /// <summary>Something waits on the person.</summary>
    Warning,
}

/// <summary>
/// What a balloon is about, as the application's notifier reads it off a <c>SESSION_ATTENTION</c> event: its kind, the event's own
/// English (<paramref name="Headline"/>, said only for a kind this build does not word) and what the session wrote (<paramref name="Detail"/>),
/// and the facts each kind's words are filled from.
/// </summary>
public sealed record BalloonFacts(string? Kind, string? Headline = null, string? Detail = null)
{
    /// <summary>The repository a session's news is about; for a wait on accounts, the repositories it holds, as one line.</summary>
    public string? Repository { get; init; }

    /// <summary>The state an ending ended in.</summary>
    public string? State { get; init; }

    /// <summary>The quest a quest's park names.</summary>
    public string? Quest { get; init; }

    /// <summary>How many sessions failed before a quest parked; null where unsaid.</summary>
    public int? Strikes { get; init; }

    /// <summary>A terminal's press's module and route, by which its act is named: never said themselves.</summary>
    public string? Module { get; init; }

    /// <inheritdoc cref="Module"/>
    public string? Route { get; init; }
}

/// <summary>A balloon's words and icon.</summary>
public sealed record BalloonWords(string Title, string Body, BalloonIcon Icon);

/// <summary>
/// What a session's attention balloon says and shows by the driver's kind (NOTIFY1d), worded in the window's language from the words
/// its page handed the shell (NOTIFY2d, D196, the notification design §3.3, §9): the choice, here where it is tested, while the
/// application's notifier shows the balloon.
/// </summary>
/// <remarks>
/// <para><b>Each kind from its own words.</b> A headline and a body per kind, by the catalogue's keys under <see cref="Prefix"/>, filled
/// with the event's facts; a terminal's press says its act by its route's own words (<see cref="PipeWording.Key"/>) and never the
/// route's constant or a value it carries (CONFIRMSEEN1, NOTIFY2a). What a session wrote, a park's question, an ending's conclusion, a
/// held call's title, is its body as written: content is never translated. An ending is said by its state's own words, and a state no
/// words name is an ending, so no state's code is ever in a sentence (§6).</para>
///
/// <para><b>With no words handed, English</b>, the English catalogue's own words (<see cref="English"/>, held to them by a test), so a
/// balloon raised before any page has told its words, or for a key an older page did not hand, still says something whole.</para>
/// </remarks>
public static partial class SessionBalloon
{
    /// <summary>Where the balloon's words are in the catalogue.</summary>
    public const string Prefix = "notifications.system.";

    /// <summary>
    /// Every key the balloon reads, with the English catalogue's words for it: what is said where the page handed none. A twin of
    /// <c>locales/en/notifications.json</c>, held equal to it by <c>SessionBalloonTests</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Prefix + "parked.title"] = "{{repository}}: a session waits on you",
        [Prefix + "parked.body"] = "Open Daoris to answer it.",
        [Prefix + "questParked.title"] = "{{repository}}: quest #{{quest}} parked after its failed sessions",
        [Prefix + "questParked.titleOne"] = "{{repository}}: quest #{{quest}} parked after 1 failed session",
        [Prefix + "questParked.titleCount"] = "{{repository}}: quest #{{quest}} parked after {{count}} failed sessions",
        [Prefix + "questParked.body"] = "Open Daoris to try it again.",
        [Prefix + "ended.completed"] = "{{repository}}: a session completed",
        [Prefix + "ended.failed"] = "{{repository}}: a session failed",
        [Prefix + "ended.stopped"] = "{{repository}}: a session was stopped",
        [Prefix + "ended.declined"] = "{{repository}}: a session declined",
        [Prefix + "ended.stood-down"] = "{{repository}}: a session stood down",
        [Prefix + "ended.other"] = "{{repository}}: a session ended",
        [Prefix + "ended.body"] = "Open Daoris to see what it did.",
        [Prefix + "waiting.title"] = "{{repository}}: a start waits for an account",
        [Prefix + "waiting.body"] = "Open Daoris to see which accounts it waits for.",
        [Prefix + "answered.title"] = "{{repository}}: a conversation answered you",
        [Prefix + "answered.help"] = "Ask Daoris answered you",
        [Prefix + "answered.body"] = "Open Daoris to read it.",
        [Prefix + "confirming.title"] = "A terminal waits for your confirmation",
        [Prefix + "confirming.body"] = "It asks to {{act}}. Open Daoris to confirm or refuse it.",
        [Prefix + "confirming.plain"] = "Open Daoris to confirm or refuse it.",
        [Prefix + "holding.title"] = "{{repository}}: a session holds a tool call for you",
        [Prefix + "holding.help"] = "Ask Daoris holds a tool call for you",
        [Prefix + "holding.body"] = "Open Daoris to answer it before it is refused by itself.",
    };

    /// <summary>
    /// The balloon for <paramref name="facts"/>, in the words <paramref name="handed"/> by the page (null: none handed, English). A kind
    /// this build does not word says the event's own headline, and its detail or the ending's line.
    /// </summary>
    public static BalloonWords Words(BalloonFacts facts, IReadOnlyDictionary<string, string>? handed = null)
    {
        string Word(string key) =>
            handed is not null && handed.TryGetValue(key, out var told) && !string.IsNullOrWhiteSpace(told) ? told : English[key];
        // What a session wrote, as written; else the kind's own line.
        string Written(string key) => string.IsNullOrWhiteSpace(facts.Detail) ? Word(key) : facts.Detail;
        var repository = facts.Repository ?? "";
        var named = new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = repository, ["quest"] = facts.Quest ?? "" };

        var (title, body) = facts.Kind switch
        {
            "Parked" => (Fill(Word(Prefix + "parked.title"), named), Written(Prefix + "parked.body")),
            "QuestParked" => (Fill(Word(Prefix + facts.Strikes switch
            {
                null => "questParked.title",
                1 => "questParked.titleOne",
                _ => "questParked.titleCount",
            }), new Dictionary<string, string>(named, StringComparer.Ordinal)
            {
                ["count"] = facts.Strikes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            }), Written(Prefix + "questParked.body")),
            "Ended" => (Fill(Word(Ending(facts.State)), named), Written(Prefix + "ended.body")),
            // The detail of a wait for accounts is Daoris's own English sentence, not a session's words: its body is the window's.
            "Waiting" => (Fill(Word(Prefix + "waiting.title"), named), Word(Prefix + "waiting.body")),
            // Ask Daoris's room is no repository the person knows by name (NOTIFY1a): its answer is said by the room's.
            "Answered" => (repository == HelpRoom.Repository ? Word(Prefix + "answered.help") : Fill(Word(Prefix + "answered.title"), named),
                Word(Prefix + "answered.body")),
            HeldCallAnnouncements.AttentionKind => (
                repository == HelpRoom.Repository ? Word(Prefix + "holding.help") : Fill(Word(Prefix + "holding.title"), named),
                Written(Prefix + "holding.body")),
            // A terminal's press (CONFIRMSEEN1): its act by its route's words, never the route; the event's English is not said.
            PipeConfirmations.AttentionKind => (Word(Prefix + "confirming.title"), Act(facts, handed) is { } act
                ? Fill(Word(Prefix + "confirming.body"), new Dictionary<string, string>(StringComparer.Ordinal) { ["act"] = act })
                : Word(Prefix + "confirming.plain")),
            _ => (string.IsNullOrWhiteSpace(facts.Headline) ? "Daoris" : facts.Headline, Written(Prefix + "ended.body")),
        };

        return new BalloonWords(title, body, Icon(facts.Kind));
    }

    /// <summary>
    /// Warn for a park, for a terminal's press waiting for the window (CONFIRMSEEN1) and for a held tool call (NOTIFY2d): each is
    /// WAITING on somebody. An ending, an answer or a wait for an account is news.
    /// </summary>
    public static BalloonIcon Icon(string? kind) =>
        kind is "Parked" or "QuestParked" or PipeConfirmations.AttentionKind or HeldCallAnnouncements.AttentionKind
            ? BalloonIcon.Warning
            : BalloonIcon.Info;

    /// <summary>An ending's key by its state: a state no words name is an ending, never its code in a sentence.</summary>
    private static string Ending(string? state) =>
        state is { Length: > 0 } && English.ContainsKey($"{Prefix}ended.{state}") ? $"{Prefix}ended.{state}" : $"{Prefix}ended.other";

    /// <summary>A press's act, in the words handed for its route; none where none were, or its route is not named.</summary>
    private static string? Act(BalloonFacts facts, IReadOnlyDictionary<string, string>? handed) =>
        handed is not null && facts.Route is { Length: > 0 } route
            && handed.TryGetValue(PipeWording.Key(facts.Module ?? DriverModule.Name, route), out var act) && !string.IsNullOrWhiteSpace(act)
            ? act
            : null;

    /// <summary>
    /// Each <c>{{name}}</c> of <paramref name="words"/> filled once with its value; one with no value says nothing. A value is never
    /// filled again, so a repository named like a placeholder is said as it is named.
    /// </summary>
    private static string Fill(string words, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(words, match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : "");

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex Placeholder();
}
