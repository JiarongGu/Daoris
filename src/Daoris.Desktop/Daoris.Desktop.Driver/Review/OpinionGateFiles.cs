using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second opinion's two files under the home (XAGENT1f): the opinions owed, <c>&lt;home&gt;/opinions/due.json</c>
/// (<see cref="OpinionDues"/>), and each chain's gate record, <c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.json</c>
/// (<see cref="OpinionGates"/>), read without trusting their shape and written whole, atomically, as BOM-less UTF-8 with LF.
/// </summary>
/// <remarks>
/// The use cases decide what is kept; this reads and writes it. A file that does
/// not read is nothing kept; a write that fails throws, as it did.
/// </remarks>
internal static class OpinionGateFiles
{
    /// <summary>Every entry <paramref name="path"/> keeps, in the order they became due; none where it is missing or does not read.</summary>
    public static IReadOnlyList<OpinionDue> ReadDues(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("due", out var due) || due.ValueKind != JsonValueKind.Array) return [];
            return [.. due.EnumerateArray().Select(DueOf).OfType<OpinionDue>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Every entry written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteDues(string path, IReadOnlyList<OpinionDue> all)
    {
        JsonFile.Write(path, writer => ComposeDues(writer, all));
    }

    /// <summary>Each repository's part of the chain's record at <paramref name="path"/>, by name in any case; none where nothing reads.</summary>
    public static IReadOnlyDictionary<string, OpinionGateKept> ReadGates(string path)
    {
        var all = new Dictionary<string, OpinionGateKept>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return all;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("repositories", out var repositories) || repositories.ValueKind != JsonValueKind.Object) return all;
            foreach (var repository in repositories.EnumerateObject())
            {
                if (repository.Value.ValueKind != JsonValueKind.Object) continue;
                all[repository.Name] = new OpinionGateKept
                {
                    Asks = [.. Json.Items(repository.Value, "asks").Select(AskOf).OfType<OpinionAskKept>()],
                    Requests = [.. Json.Items(repository.Value, "requests").Select(RequestOf).OfType<OpinionRequest>()],
                    Person = [.. Json.Items(repository.Value, "person").Select(WordOf).OfType<OpinionPersonWord>()],
                };
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A record that does not read is nothing kept: the next ask and the next answer write it again.
        }

        return all;
    }

    /// <summary>The chain's record written whole to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteGates(string path, string chain, IReadOnlyDictionary<string, OpinionGateKept> all)
    {
        JsonFile.Write(path, writer => ComposeGates(writer, chain, all));
    }

    private static void ComposeDues(Utf8JsonWriter writer, IReadOnlyList<OpinionDue> all)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("due");
        foreach (var entry in all)
        {
            writer.WriteStartObject();
            writer.WriteString("session", entry.Session);
            if (entry.Quest is not null) writer.WriteString("quest", entry.Quest);
            // CHATFLOW1d: a chat run's due names its run, the tip its press captured and the person's turn count then.
            if (entry.Run is not null) writer.WriteString("run", entry.Run);
            if (entry.Tip is not null) writer.WriteString("tip", entry.Tip);
            if (entry.Turns is { } turns) writer.WriteNumber("turns", turns);
            writer.WriteString("repository", entry.Repository);
            writer.WriteString("workspace", entry.Workspace);
            writer.WriteString("tree", entry.Tree);
            writer.WriteString("occasion", entry.Occasion);
            writer.WriteString("at", OpinionViews.Moment(entry.At));
            if (entry.Closed is { } closed) writer.WriteString("closed", OpinionViews.Moment(closed));
            if (entry.Why is not null) writer.WriteString("why", entry.Why);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static OpinionDue? DueOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && Json.Text(element, "session") is { } session
        && (Json.Text(element, "quest") is not null || ChatRuns.IsChatRun(Json.Text(element, "run")))
        && Json.Text(element, "repository") is { } repository && Json.Text(element, "tree") is { } tree
        && Json.Text(element, "occasion") is { } occasion && Kept.Moment(element, "at") is { } at
            ? new OpinionDue(session, Json.Text(element, "quest"), repository, Json.Text(element, "workspace") ?? RemoteTarget.DefaultWorkspace, tree, occasion, at)
            {
                Closed = Kept.Moment(element, "closed"),
                Why = Json.Text(element, "why"),
                Run = ChatRuns.IsChatRun(Json.Text(element, "run")) ? Json.Text(element, "run") : null,
                Tip = Json.Text(element, "tip"),
                Turns = element.TryGetProperty("turns", out var turns) && turns.ValueKind == JsonValueKind.Number && turns.TryGetInt32(out var count) ? count : null,
            }
            : null;

    private static void ComposeGates(Utf8JsonWriter writer, string chain, IReadOnlyDictionary<string, OpinionGateKept> all)
    {
        writer.WriteStartObject();
        writer.WriteString("chain", chain);
        writer.WriteStartObject("repositories");
        foreach (var (repository, kept) in all.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(repository);
            writer.WriteStartArray("asks");
            foreach (var ask in kept.Asks)
            {
                writer.WriteStartObject();
                writer.WriteString("at", OpinionViews.Moment(ask.At));
                writer.WriteString("occasion", ask.Occasion);
                writer.WriteString("working", ask.Working);
                writer.WriteString("tip", ask.Tip);
                writer.WriteString("by", ask.By);
                if (ask.Opinion is not null) writer.WriteString("opinion", ask.Opinion);
                if (ask.Code is not null) writer.WriteString("code", ask.Code);
                if (ask.Until is { } until) writer.WriteString("until", OpinionViews.Moment(until));
                if (ask.Reviewer is not null) writer.WriteString("reviewer", ask.Reviewer);
                if (ask.Label is not null) writer.WriteString("label", ask.Label);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("requests");
            foreach (var request in kept.Requests)
            {
                writer.WriteStartObject();
                writer.WriteString("at", OpinionViews.Moment(request.At));
                writer.WriteString("session", request.Session);
                writer.WriteString("occasion", request.Occasion);
                if (request.Reviewer is not null) writer.WriteString("reviewer", request.Reviewer);
                if (request.SameAgent) writer.WriteBoolean("sameAgent", true);
                if (request.Words is not null) writer.WriteString("words", request.Words);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("person");
            foreach (var word in kept.Person)
            {
                writer.WriteStartObject();
                writer.WriteString("said", word.Said);
                writer.WriteString("tip", word.Tip);
                writer.WriteString("at", OpinionViews.Moment(word.At));
                if (word.Words is not null) writer.WriteString("words", word.Words);
                if (word.Door is not null) writer.WriteString("door", word.Door);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static OpinionAskKept? AskOf(JsonElement element) =>
        Kept.Moment(element, "at") is { } at && Json.Text(element, "occasion") is { } occasion && Json.Text(element, "working") is { } working
        && Json.Text(element, "tip") is { } tip
            ? new OpinionAskKept(at, occasion, working, tip)
            {
                Opinion = Json.Text(element, "opinion"),
                Code = Json.Text(element, "code"),
                Until = Kept.Moment(element, "until"),
                By = Json.Text(element, "by") ?? OpinionAskers.Look,
                Reviewer = Json.Text(element, "reviewer"),
                Label = Json.Text(element, "label"),
            }
            : null;

    private static OpinionRequest? RequestOf(JsonElement element) =>
        Kept.Moment(element, "at") is { } at && Json.Text(element, "session") is { } session && Json.Text(element, "occasion") is { } occasion
            ? new OpinionRequest(at, session, occasion)
            {
                Reviewer = Json.Text(element, "reviewer"),
                SameAgent = element.TryGetProperty("sameAgent", out var same) && same.ValueKind == JsonValueKind.True,
                Words = Json.Text(element, "words"),
            }
            : null;

    private static OpinionPersonWord? WordOf(JsonElement element) =>
        Json.Text(element, "said") is { } said && Json.Text(element, "tip") is { } tip && Kept.Moment(element, "at") is { } at
            ? new OpinionPersonWord(said, tip, at) { Words = Json.Text(element, "words"), Door = Json.Text(element, "door") }
            : null;

    /// <summary>
    /// Reading a kept file's fields without trusting its shape: a field of another type is absent. Nested, so its short name is
    /// declared nowhere the structure gate indexes (STRUCT3c): other modules name a <c>Kept</c> of their own.
    /// </summary>
    private static class Kept
    {
        public static DateTimeOffset? Moment(JsonElement element, string name) => Moments.Read(element, name);
    }
}
