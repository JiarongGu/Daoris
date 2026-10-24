using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a driven session's agent records what only the person can decide before it ends its turn on it (PARKVIEW1, the park's ask
/// design §2): one file under the home's <c>sessions</c> folder, named by the session, <c>&lt;session&gt;.question.json</c>, written
/// whole by the connector's <c>question_ask</c> and replaced by a later one. The driver's <c>QuestionFiles</c> reads it and binds it to
/// the park its run ends in, or deletes it; the two share no code, and each side's tests hold the same shape
/// (<c>McpQuestionToolTests</c>, <c>QuestionFilesTests</c>).
/// </summary>
/// <remarks>
/// <para><b>The agent's words, recorded, never read out of its prose</b> (D135's KNOWUSE1c: the closing note's headings are words,
/// not a format): the question, the answers it offers with what it does on each, the one it recommends and by when, each kept as
/// written once trimmed. A go-ahead is not asked here: an act's yes is <c>go_ahead_ask</c>'s, held on the ask (KNOWUSE1a).</para>
///
/// <para><b>Machine-local</b>: no wire carries it, so a teammate's mirror never shows a question its machine cannot answer.</para>
/// </remarks>
/// <param name="home">The home the file goes under, or null when there is none: every question is then refused.</param>
public sealed class QuestionBox(string? home)
{
    /// <summary>The longest question kept, in characters: the decision, not the report.</summary>
    public const int MaxQuestion = 1000;

    /// <summary>The most answers offered: each is a press beside the box, and more is a list to read, not a choice to make.</summary>
    public const int MaxChoices = 6;

    /// <summary>The longest answer's label, in characters: the words on its press.</summary>
    public const int MaxLabel = 80;

    /// <summary>The longest account of what the agent does on an answer, in characters.</summary>
    public const int MaxDoes = 300;

    /// <summary>The longest "by when", in characters: a window or a moment, as the agent knows it.</summary>
    public const int MaxBy = 200;

    /// <summary>The longest reason for the recommendation, in characters (QUESTIONUI1a): drawn under the recommended answer.</summary>
    public const int MaxWhy = 300;

    public const string Suffix = ".question.json";

    /// <summary>
    /// The marks an agent sets on a question it knows is the owner's alone (SUPERVISOR1b, D208; the supervisor design §4.1): a sign-in,
    /// widening access, spending, deleting, a repository document that asks the owner, and the owner's own taste. A marked question is
    /// never answered by the supervisor; the mark only narrows, so an unmarked question is still judged by its holder. The driver's
    /// <c>SupervisorBounds.All</c> and the page's <c>OWNER_MARKS</c> name the same six; both readers take any mark as one.
    /// </summary>
    public static readonly IReadOnlyList<string> OwnerMarks = ["sign-in", "access", "spending", "delete", "repository-asks", "preference"];

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no park of yours to ask the person in: say exactly "
        + "what you need from them in your last message, and end your turn.";

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the session's files live.</summary>
    public static QuestionBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one session's question is: <c>&lt;home&gt;/sessions/&lt;session&gt;.question.json</c>, the home's <see cref="HomeEntry.Sessions"/>.</summary>
    public static string PathOf(string home, string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Keep the question of the session the connector names, or refuse it: answered as the sentence the agent reads.</summary>
    /// <param name="asker">The record of the session that asks, or null when the connector names none or it is not held.</param>
    /// <param name="named">The session the connector names, said back when no record is held for it.</param>
    /// <param name="choices">The answers it offers, each its label and what it does on it, in its order; null or empty for none.</param>
    /// <param name="recommend">The answer it recommends, by its number from 1, or null.</param>
    /// <param name="why">Why it recommends that answer (QUESTIONUI1a), or null: said only with a recommendation.</param>
    /// <param name="owner">One of <see cref="OwnerMarks"/> where the agent knows the question is the owner's alone (SUPERVISOR1b), or null.</param>
    public (string? Id, string Message) Ask(
        Session? asker, string? named, string? question, IReadOnlyList<(string? Label, string? Does)>? choices, int? recommend, string? by,
        DateTimeOffset at, string? why = null, string? owner = null)
    {
        if (string.IsNullOrWhiteSpace(named)) return (null, $"{NoSession} Nothing was asked.");
        if (asker is not { Origin: null }) return (null, $"No session `{named.Trim()}` of this machine's is held to ask in. Nothing was asked.");
        if (Unfit(asker) is { } unfit) return (null, $"{unfit} Nothing was asked.");

        var words = question?.Trim() ?? "";
        if (words.Length == 0) return (null, "A question needs the question: what only the person can decide, in a sentence or two. Nothing was asked.");
        if (words.Length > MaxQuestion)
        {
            return (null, $"A question is at most {MaxQuestion} characters, and this is {words.Length}: ask the decision, and leave the report "
                          + "to your last message. Nothing was asked.");
        }

        var offered = choices ?? [];
        if (offered.Count > MaxChoices) return (null, $"Offer at most {MaxChoices} answers, and this offers {offered.Count}. Nothing was asked.");
        var kept = new List<QuestionChoice>();
        for (var index = 0; index < offered.Count; index++)
        {
            var (label, does) = (offered[index].Label?.Trim() ?? "", offered[index].Does?.Trim() ?? "");
            var n = index + 1;
            if (label.Length == 0) return (null, $"Your answer {n} needs its label: the words on its press. Nothing was asked.");
            if (label.Length > MaxLabel) return (null, $"Answer {n}'s label is at most {MaxLabel} characters, and this is {label.Length}. Nothing was asked.");
            if (does.Length == 0) return (null, $"Your answer {n} needs what you will do on it. Nothing was asked.");
            if (does.Length > MaxDoes) return (null, $"What answer {n} does is at most {MaxDoes} characters, and this is {does.Length}. Nothing was asked.");
            if (kept.Any(other => string.Equals(other.Label, label, StringComparison.OrdinalIgnoreCase)))
            {
                return (null, $"Answer {n} offers the same answer twice (`{label}`): give each answer its own label. Nothing was asked.");
            }

            kept.Add(new QuestionChoice(label, does));
        }

        if (recommend is { } pick && (pick < 1 || pick > kept.Count))
        {
            return (null, kept.Count == 0
                ? $"Your recommendation `{pick}` names no answer: this question offers none. Nothing was asked."
                : $"Your recommendation `{pick}` names no answer: name one by its number, from 1 to {kept.Count}, or none. Nothing was asked.");
        }

        var reason = why?.Trim() is { Length: > 0 } given ? given : null;
        if (reason is not null && recommend is null)
        {
            return (null, "A reason goes with a recommendation: name the answer you recommend by its number, or leave the reason out. "
                          + "Nothing was asked.");
        }

        if (reason is { Length: > MaxWhy })
        {
            return (null, $"Why you recommend it is at most {MaxWhy} characters, and this is {reason.Length}. Nothing was asked.");
        }

        var when = by?.Trim() is { Length: > 0 } said ? said : null;
        if (when is { Length: > MaxBy }) return (null, $"By when is at most {MaxBy} characters, and this is {when.Length}. Nothing was asked.");
        var mark = owner?.Trim() is { Length: > 0 } marked ? marked : null;
        if (mark is not null && !OwnerMarks.Contains(mark, StringComparer.Ordinal))
        {
            return (null, $"`{mark}` is not one of the owner's marks ({string.Join(", ", OwnerMarks.Select(each => $"`{each}`"))}): set one, "
                          + "or leave it out. Nothing was asked.");
        }

        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was asked.");

        var id = Guid.NewGuid().ToString("N")[..8];
        Write(PathOf(Home, asker.Id), writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("session", asker.Id);
            writer.WriteString("question", words);
            writer.WriteStartArray("choices");
            foreach (var choice in kept)
            {
                writer.WriteStartObject();
                writer.WriteString("label", choice.Label);
                writer.WriteString("does", choice.Does);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (recommend is { } chosen) writer.WriteNumber("recommend", chosen); else writer.WriteNull("recommend");
            if (reason is null) writer.WriteNull("why"); else writer.WriteString("why", reason);
            if (when is null) writer.WriteNull("by"); else writer.WriteString("by", when);
            if (mark is null) writer.WriteNull("owner"); else writer.WriteString("owner", mark);
            writer.WriteString("at", Moments.Write(at));
        });

        return (id,
            $"Asked `#{id}`. The person sees your question at the end of this session's conversation, each answer you offered a press "
            + "beside their own words, with what you said each does. A go-ahead for an act is asked with `go_ahead_ask`, never here. Now "
            + "say in your last message that the work waits on this question, commit what you have, and end your turn with your quest "
            + "still taken. Their answer is your next prompt: an answer they pressed as `chose: ` and its label, as you wrote it, then any "
            + "words of theirs after a blank line; or their words alone.");
    }

    /// <summary>
    /// Whether an asker's record is no driven session running a turn on a quest of its own: a helper, a reviewer, an intake or Ask
    /// Daoris's own conversation asks the person nothing, a conversation's person reads its words as they come, a record on no quest
    /// has none to hold while it waits, and one parked or ended runs no turn to end on it. The sentence says which, or null when it
    /// may ask. Judged here whole: the hand-over ask's <c>Unfit</c> lets a conversation and a record on no quest through.
    /// </summary>
    private static string? Unfit(Session asker) =>
        asker.Serves is { } serves
            ? $"Session `{asker.Id}` is a helper for session `{serves}`: a helper answers one question and asks the person nothing."
        : asker.Opinion is { } opinion
            ? $"Session `{asker.Id}` reads another session's work for second opinion `{opinion}`: a reviewer asks the person nothing."
        : asker.Ask is { } ask
            ? $"Session `{asker.Id}` is the intake for ask `#{ask}`: an intake asks the person through its ask."
        : asker.HelpMode is not null || string.Equals(asker.Repository, SessionLedger.HelpRepository, StringComparison.Ordinal)
            ? $"Session `{asker.Id}` is Ask Daoris's own conversation, whose person reads your words as they come."
        : asker.Kind == SessionKind.Chat
            ? $"Session `{asker.Id}` is a conversation: its person reads your words as they come, so ask in your reply."
        : asker.Quest is null
            ? $"Session `{asker.Id}` holds no quest, so it has no park to ask the person in."
        : asker.State is not (SessionState.Queued or SessionState.Starting or SessionState.Working)
            ? $"Session `{asker.Id}` is not running a turn, so it has no turn to end on a question."
        : null;

    /// <summary>The question kept for a session, or null when there is none or it does not read.</summary>
    public static QuestionKept? Read(string home, string session)
    {
        try
        {
            var path = PathOf(home, session);
            if (!File.Exists(path)) return null;
            string text;
            // Shared for a delete, as the driver reads it, so its settle is never held back by a read here.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (Text(root, "id") is not { Length: > 0 } id || Text(root, "question") is not { Length: > 0 } question) return null;
            var choices = root.TryGetProperty("choices", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray()
                    .Select(choice => choice.ValueKind == JsonValueKind.Object && Text(choice, "label") is { Length: > 0 } label
                        ? new QuestionChoice(label, Text(choice, "does") ?? "")
                        : null)
                    .OfType<QuestionChoice>()
                    .ToList()
                : [];
            int? recommend = root.TryGetProperty("recommend", out var number) && number.ValueKind == JsonValueKind.Number
                && number.TryGetInt32(out var chosen) ? chosen : null;
            return new QuestionKept(id, Text(root, "session") ?? session, question, choices, recommend, Text(root, "by"), Text(root, "at"))
            {
                Why = Text(root, "why"),
                Owner = Text(root, "owner") is { Length: > 0 } owner ? owner : null,
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Hand-rolled and atomic, as the hand-over ask's box writes: nothing may stop working under AOT, and a driver reading the folder
    // must never see half a file.
    private static void Write(string path, Action<Utf8JsonWriter> body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}

/// <summary>A session's question as <c>question_ask</c> wrote it: its id, the session, its words, its answers, and when.</summary>
public sealed record QuestionKept(
    string Id, string Session, string Question, IReadOnlyList<QuestionChoice> Choices, int? Recommend, string? By, string? At)
{
    /// <summary>Why it recommends its answer (QUESTIONUI1a), or null where it gave no reason.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// The agent's mark that the question is the owner's alone (SUPERVISOR1b), as the file says it, or null: any mark at all, one this
    /// build does not know included, keeps the supervisor from answering it, the safe side.
    /// </summary>
    public string? Owner { get; init; }
}

/// <summary>One answer a question offers: the words on its press, and what the agent does on it.</summary>
public sealed record QuestionChoice(string Label, string Does);
