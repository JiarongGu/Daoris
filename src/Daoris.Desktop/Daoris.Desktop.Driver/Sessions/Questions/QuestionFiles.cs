using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A driven session's question to the person (PARKVIEW1, the park's ask design §2): one file per session under the home's sessions
/// folder, <c>&lt;session&gt;.question.json</c>, read as the connector's <c>question_ask</c> writes it (the service's
/// <c>QuestionBox</c>). The file is the contract; the two share no code, and both read the one table
/// <c>fixtures/park-question.json</c> (the page's reader of the route's answer too).
/// </summary>
/// <remarks>
/// <para><b>Gone as each run of its record opens</b> (<see cref="Clear"/>), so while a resumed run works no question of the park
/// before it stands, and a press another window made on it is refused as changed; <b>bound to its park as the run concludes</b>
/// (<see cref="SettleRun"/>): kept only where the run parks and the question was asked since the run last handed its agent words, or
/// does not read (HELPERWAIT1b), deleted otherwise. So a question on file while its record is parked is its last turn's. The connector writes during the run, and
/// these clear before it and settle after it, so two never write at once.</para>
///
/// <para><b>Read sharing the file's delete</b>, so a page reading it never holds a settle's delete back; a delete the disk still
/// refuses is tried again briefly, and one that stays is answered <see cref="QuestionSettle.Stayed"/> for its caller to log.</para>
///
/// <para><b>Held to the writer's bounds on read</b>: a file past any of them is no question, as the connector would never have kept
/// it. Machine-local, as the hand-over ask's files are: no wire carries it, and it goes with the session's history
/// (<see cref="SessionHomeFiles"/>).</para>
/// </remarks>
public sealed class QuestionFiles(string home)
{
    public const string Suffix = ".question.json";

    /// <summary>The writer's bounds (<c>QuestionBox</c>), held on read too: the question, its answers, each label and each <c>does</c>, and by when.</summary>
    public const int MaxQuestion = 1000, MaxChoices = 6, MaxLabel = 80, MaxDoes = 300, MaxBy = 200, MaxWhy = 300;

    /// <summary>How many times a refused delete is tried, each a little later: about a second in all, as a busy rename is (AtomicFile).</summary>
    private const int DeleteTries = 20;

    private string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>The session's question, or null for none, a file that does not read or is past a bound, or an id that is not one.</summary>
    public ParkQuestion? Read(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;
        var path = PathOf(session);
        try
        {
            if (!File.Exists(path)) return null;
            string text;
            // Shared for a delete, so a settle or a clear never waits on a page reading it.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            return Parsed(text, session, () => File.GetLastWriteTimeUtc(path));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the session asked the person a question at or after <paramref name="since"/> (HELPERWAIT1; Codex's round-2 #3 on COWORKUX1):
    /// false where no file is on disk or its question was asked before, true where it was asked since, and null where a file is there and
    /// does not read (busy, broken, past a bound), which a caller deciding that nobody waits on the person must read as a question.
    /// </summary>
    public bool? AskedSince(string session, DateTimeOffset since)
    {
        if (!SessionEventText.IsLocalId(session) || !File.Exists(PathOf(session))) return false;
        return Read(session) is { } asked ? asked.At >= since : null;
    }

    /// <summary>The question a file's text holds, held to the writer's bounds; null where it holds none.</summary>
    internal static ParkQuestion? Parsed(string text, string session, Func<DateTimeOffset> written)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "id") is not { Length: > 0 } id
            || Text(root, "question") is not { Length: <= MaxQuestion } question || string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        // An answer with no label is no answer to press; one with no words for what it does says none.
        List<ParkQuestionChoice> choices = root.TryGetProperty("choices", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray()
                .Select(choice => choice.ValueKind == JsonValueKind.Object && Text(choice, "label") is { } label && !string.IsNullOrWhiteSpace(label)
                    ? new ParkQuestionChoice(label, Text(choice, "does") ?? "")
                    : null)
                .OfType<ParkQuestionChoice>()]
            : [];
        var by = Text(root, "by") is { Length: > 0 } said ? said : null;
        if (choices.Count > MaxChoices || choices.Any(choice => choice.Label.Length > MaxLabel || choice.Does.Length > MaxDoes)
            || choices.Select(choice => choice.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() < choices.Count
            || by is { Length: > MaxBy } || Text(root, "why") is { Length: > MaxWhy })
        {
            return null;
        }

        int? recommend = root.TryGetProperty("recommend", out var number) && number.ValueKind == JsonValueKind.Number
            && number.TryGetInt32(out var chosen) && chosen >= 1 && chosen <= choices.Count ? chosen : null;
        // Its reason explains the recommendation (QUESTIONUI1a): with none recommended, there is nothing for it to explain.
        var why = recommend is not null && Text(root, "why") is { } reason && !string.IsNullOrWhiteSpace(reason) ? reason : null;
        // The agent's mark that the question is the owner's alone (SUPERVISOR1b), any words at all: the supervisor answers none of it.
        var owner = Text(root, "owner") is { } mark && !string.IsNullOrWhiteSpace(mark) ? mark : null;
        return new ParkQuestion(id, session, question, choices, recommend, by, Moments.Read(root, "at") ?? written(), why, owner);
    }

    /// <summary>
    /// Bind the session's question to the park its run ended in (the design §2): kept where <paramref name="state"/> parks and the
    /// question was asked at or after <paramref name="since"/>; deleted otherwise, and said where the disk would not let it go.
    /// </summary>
    /// <remarks>
    /// 🔴 HELPERWAIT1b (Codex's round-3 #1 on COWORKUX1): a park keeps a file that does not read (busy, broken, past a bound) too. Neither its
    /// question nor when it was asked can be read, so nothing proves it was not this turn's; it is what made the turn park rather than wait
    /// on its helper (<see cref="AskedSince"/> null), and a park never shows it, since a read of it is none. The next run's opening clears it.
    /// </remarks>
    public QuestionSettle Settle(string session, string state, DateTimeOffset since)
    {
        if (!SessionEventText.IsLocalId(session)) return QuestionSettle.None;
        var path = PathOf(session);
        if (!File.Exists(path)) return QuestionSettle.None;
        if (SessionStates.IsParked(state) && (Read(session) is not { } asked || asked.At >= since)) return QuestionSettle.Kept;
        return Delete(path);
    }

    /// <summary>
    /// <see cref="Settle"/> at a run's conclusion: since the run's last handing of words, the later of its opening and the newest
    /// moment its record shows words handed to its agent (<see cref="SessionEvents.LastHandedAt"/>). A run that went on in the same
    /// record with the person's words (MSG1b, on either door) asked its last turn's question after them; one asked before them may
    /// have been answered by them, and goes.
    /// </summary>
    public QuestionSettle SettleRun(string session, string state, DateTimeOffset openedAt, SessionEvents events) =>
        Settle(session, state, events.LastHandedAt(session) is { } handed && handed > openedAt ? handed : openedAt);

    /// <summary>
    /// The session's question gone as a run of its record opens (Fable's read, Q2): the park it asked in is over, so a press another
    /// window still shows it with is refused as changed at once rather than taken for the run's own question.
    /// </summary>
    /// <summary>
    /// Write <paramref name="question"/> back as its session's question, with the same id (SUPERVISOR1a-i, D208; the supervisor design §6.3):
    /// a park restored after its run's opening cleared the file gets its question again, from the decision's snapshot, so the owner's card
    /// has its choices to press. False where the id is not a session's or the disk would not take it.
    /// </summary>
    public bool Restore(ParkQuestion question)
    {
        if (!SessionEventText.IsLocalId(question.Session)) return false;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", question.Id);
            writer.WriteString("session", question.Session);
            writer.WriteString("question", question.Question);
            writer.WriteStartArray("choices");
            foreach (var choice in question.Choices)
            {
                writer.WriteStartObject();
                writer.WriteString("label", choice.Label);
                writer.WriteString("does", choice.Does);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (question.Recommend is { } recommend) writer.WriteNumber("recommend", recommend);
            if (question.By is not null) writer.WriteString("by", question.By);
            if (question.Why is not null) writer.WriteString("why", question.Why);
            writer.WriteString("at", Moments.Write(question.At));
            writer.WriteEndObject();
        }

        try
        {
            AtomicFile.WriteText(PathOf(question.Session), Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public QuestionSettle Clear(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return QuestionSettle.None;
        var path = PathOf(session);
        return File.Exists(path) ? Delete(path) : QuestionSettle.None;
    }

    /// <summary>The file gone, a refusal tried again briefly; <see cref="QuestionSettle.Stayed"/> where the disk would still not let it go.</summary>
    private static QuestionSettle Delete(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return QuestionSettle.Gone;
            }
            catch (Exception busy) when (busy is IOException or UnauthorizedAccessException)
            {
                if (attempt >= DeleteTries - 1) return File.Exists(path) ? QuestionSettle.Stayed : QuestionSettle.Gone;
                Thread.Sleep(5 + attempt * 5);
            }
        }
    }
}

/// <summary>What a settle or a clear did with a session's question: there was none, it is kept for the park, it went, or it stayed.</summary>
public enum QuestionSettle
{
    None,
    Kept,
    Gone,

    /// <summary>The disk would not let it go: its caller logs it, since a park could show it (the machine log's <c>question.stayed</c>).</summary>
    Stayed,
}

/// <summary>
/// How a pressed answer of a park's question rides the person's words (PARKVIEW1, the design §4; Fable's read, Q4): the page sends the
/// label, then the person's own words after a blank line, naming the question; the words are marked with the label, and the session is
/// handed them as <c>chose: &lt;label&gt;</c> before the person's own.
/// </summary>
public static class QuestionWords
{
    /// <summary>What a pressed answer is handed as, before its label.</summary>
    public const string Chose = PressedWords.Chose;

    /// <summary>
    /// The answer of <paramref name="question"/> that <paramref name="text"/> begins with, as the page composes a press (the label alone,
    /// or the label, a blank line and the person's words), the longest first; null where the words begin with none.
    /// </summary>
    public static string? Pressed(ParkQuestion question, string text) =>
        question.Choices.Select(choice => choice.Label).OrderByDescending(label => label.Length)
            .FirstOrDefault(label => Begins(text, label));

    /// <summary>What the session is handed of words a press marked with <paramref name="choice"/>; words with none, or not so composed, as they are.</summary>
    public static string Handed(string text, string? choice) =>
        PressedWords.Handed(text, choice);

    private static bool Begins(string text, string label) => PressedWords.Begins(text, label);
}

/// <summary>
/// A session's question as <c>question_ask</c> wrote it: its id, the session, the agent's words, the answers it offers, the one it
/// recommends by number from 1 (null for none, or one that names no answer), by when as it said, when it asked, and why it recommends
/// that answer (QUESTIONUI1a; null with none recommended), and the agent's mark that the question is the owner's alone (SUPERVISOR1b;
/// null for none), read as written, a mark this build does not know included: the supervisor answers no marked question.
/// </summary>
public sealed record ParkQuestion(
    string Id, string Session, string Question, IReadOnlyList<ParkQuestionChoice> Choices, int? Recommend, string? By, DateTimeOffset At,
    string? Why = null, string? Owner = null);

/// <summary>One answer a question offers: the words on its press, and what the agent does on it.</summary>
public sealed record ParkQuestionChoice(string Label, string Does);
