using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The decisions' files under the home: written once, their state lines appended, and read back only as history, for the card
/// after a restart and the start's sweep; a decision's behaviour is decided from memory (<see cref="SupervisorBook"/>), never from here.
/// </summary>
internal sealed class SupervisorDecisionFiles(string home)
{
    private const string DecisionsSuffix = QuestionSupervisorDecisions.DecisionsSuffix;

    public string FolderOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + DecisionsSuffix);

    public string FileOf(string session, string id) => Path.Combine(FolderOf(session), id + ".json");

    public string StatesOf(string session, string id) => Path.Combine(FolderOf(session), id + ".states");

    public string PointerOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.PointerSuffix);

    /// <summary>The session folders of every decision on disk.</summary>
    public IEnumerable<string> Folders()
    {
        var root = DaorisHome.Entry(home, HomeEntry.Sessions);
        return Directory.Exists(root) ? Directory.EnumerateDirectories(root, "*" + DecisionsSuffix) : [];
    }

    public static string SessionOf(string folder) => Path.GetFileName(folder)[..^DecisionsSuffix.Length];

    public static string StateLine(SupervisorState state, DateTimeOffset at, string? why, string? word) => Written(writer =>
    {
        writer.WriteString("state", SupervisorStates.Spell(state));
        writer.WriteString("at", Moments.Write(at));
        if (why is not null) writer.WriteString("why", why);
        if (word is not null) writer.WriteString("word", word);
    }, indented: false) + "\n";

    public static string PointerJson(string question, string decision) => Written(writer =>
    {
        writer.WriteString("question", question);
        writer.WriteString("decision", decision);
    }) + "\n";

    public static void TryAppend(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line, new UTF8Encoding(false));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The files are history; memory decides. A line that could not be written reads as a difference at the next comparison.
        }
    }

    public static string? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A decision read from its files as history: never live, never handable.</summary>
    public SupervisorDecisionView? Earlier(string session, string id)
    {
        try
        {
            if (Read(FileOf(session, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("question", out var asked)) return null;
            var question = ParkQuestions.Parsed(asked.GetRawText(), session, () => DateTimeOffset.MinValue);
            // An answer names its choice; a defer its bound, and no choice.
            var bound = Json.Text(root, "bound");
            if (question is null || (Json.Text(root, "choice") ?? (bound is null ? null : "")) is not { } choice) return null;
            var reasons = Json.Items(root, "reasons")
                .Select(reason => new SupervisorReason(Json.Text(reason, "says") ?? "", Json.Text(reason, "source") ?? ""))
                .ToList();
            var holder = root.TryGetProperty("holder", out var held) && held.ValueKind == JsonValueKind.Object
                ? new SupervisorHolder(Json.Text(held, "kind") ?? "", Json.Text(held, "parent"))
                : new SupervisorHolder("", null);
            var kept = Moments.Read(root, "keptAt") ?? DateTimeOffset.MinValue;
            var (state, at, why, word) = (SupervisorState.Held, kept, (string?)null, (string?)null);
            foreach (var line in (Read(StatesOf(session, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var each = JsonDocument.Parse(line);
                if (SupervisorStates.Parse(Json.Text(each.RootElement, "state")) is not { } said) continue;
                (state, at, why) = (said, Moments.Read(each.RootElement, "at") ?? at, Json.Text(each.RootElement, "why"));
                word = Json.Text(each.RootElement, "word") ?? word;
            }

            return new SupervisorDecisionView(
                id, session, question, choice, reasons, holder, Json.Text(root, "appointment") ?? "", kept,
                Moments.Read(root, "goesOnAt") ?? kept, state, at, why, word, Earlier: true, bound);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A decision's file: an answer's names its <paramref name="choice"/>, a defer's its <paramref name="bound"/>; the counts
    /// its holder is handed beside the question's snapshot, where the keeping reads them.
    /// </summary>
    public static string DecisionJson(
        string id, string session, string? choice, string? bound, IReadOnlyList<SupervisorReason> reasons, SupervisorHolder holder,
        ParkQuestion question, string generation, DateTimeOffset kept, DateTimeOffset goesOn, IReadOnlyList<CountFact>? counts) => Written(writer =>
    {
        writer.WriteString("decision", id);
        writer.WriteString("session", session);
        writer.WriteStartObject("question");
        writer.WriteString("id", question.Id);
        writer.WriteString("question", question.Question);
        writer.WriteStartArray("choices");
        foreach (var offered in question.Choices)
        {
            writer.WriteStartObject();
            writer.WriteString("label", offered.Label);
            writer.WriteString("does", offered.Does);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (question.Recommend is { } recommend) writer.WriteNumber("recommend", recommend);
        if (question.By is not null) writer.WriteString("by", question.By);
        if (question.Why is not null) writer.WriteString("why", question.Why);
        if (question.Owner is not null) writer.WriteString("owner", question.Owner);
        writer.WriteString("at", Moments.Write(question.At));
        writer.WriteEndObject();
        if (choice is not null) writer.WriteString("choice", choice);
        if (bound is not null) writer.WriteString("bound", bound);
        writer.WriteStartArray("reasons");
        foreach (var reason in reasons)
        {
            writer.WriteStartObject();
            writer.WriteString("says", reason.Says);
            writer.WriteString("source", reason.Source);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (counts is not null)
        {
            writer.WriteStartArray("counts");
            foreach (var count in counts)
            {
                writer.WriteStartObject();
                writer.WriteString("count", count.Count);
                if (count.N is { } n) writer.WriteNumber("n", n);
                writer.WriteString("known", count.Known);
                if (count.Why is not null) writer.WriteString("why", count.Why);
                writer.WriteString("from", count.From);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteStartObject("holder");
        writer.WriteString("kind", holder.Kind);
        if (holder.Parent is not null) writer.WriteString("parent", holder.Parent);
        writer.WriteEndObject();
        writer.WriteString("appointment", generation);
        writer.WriteString("keptAt", Moments.Write(kept));
        writer.WriteString("goesOnAt", Moments.Write(goesOn));
    }) + "\n";

    private static string Written(Action<Utf8JsonWriter> body, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }
}
