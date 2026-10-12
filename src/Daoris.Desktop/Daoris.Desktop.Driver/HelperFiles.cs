using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where one ask for a helper stands, one of <see cref="HelperStates.All"/> (MIXAGENT2a-i's shapes, D163 point 2).</summary>
public static class HelperStates
{
    public const string Waiting = "waiting";
    public const string Approved = "approved";
    public const string Started = "started";
    public const string Answered = "answered";
    public const string Refused = "refused";

    public static readonly IReadOnlyList<string> All = [Waiting, Approved, Started, Answered, Refused];
}

/// <summary>
/// Why no helper answered a question, by code (MIXAGENT2a-ii): what the state file's <c>code</c> says and the note's <c>why</c>
/// carries. The page words each as <c>note.helper.why.*</c>; <see cref="Said"/> is the terminal's one English.
/// </summary>
public static class HelperWhy
{
    /// <summary>The person's rule for helpers is off for the asker's repository.</summary>
    public const string Off = "off";

    /// <summary>The asking session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The asking session is a helper, a reviewer or an intake (the files may be old).</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The asker already has two helpers at work.</summary>
    public const string TooMany = "too-many";

    /// <summary>The asker's work could not be copied for the helper to read.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>The helper could not be started.</summary>
    public const string NotStarted = "not-started";

    /// <summary>The helper ended with no answer and no last words.</summary>
    public const string NoAnswer = "no-answer";

    /// <summary>The asking session is a conversation, which takes words only from the person.</summary>
    public const string Conversation = "conversation";

    /// <summary>The asking session cannot take the answer: a teammate's, stood down, or gone.</summary>
    public const string CannotTake = "cannot-take";

    /// <summary>The reason in one English sentence's clause, for the terminal and the transcript.</summary>
    public static string Said(string why) => why switch
    {
        Off => "the rule for helpers is off for this repository",
        AskerGone => "the session that asked is not on this machine",
        AskerUnfit => "the session that asked is itself a helper, a reviewer or an intake",
        TooMany => "that session already has two helpers at work",
        Unreadable => "its work could not be copied for a helper to read",
        NotStarted => "the helper could not be started",
        NoAnswer => "the helper ended without an answer",
        Conversation => "that session is a conversation, which takes words only from the person",
        CannotTake => "that session cannot take the answer",
        _ => why,
    };
}

/// <summary>One ask for a helper as <c>&lt;askId&gt;.ask.json</c> keeps it: written by the connector's <c>helper_ask</c> or the person's door, never changed.</summary>
/// <param name="Agent">The agent the asker or the person named, one word; null where none was.</param>
/// <param name="AskedBy"><c>agent</c> or <c>person</c>.</param>
public sealed record HelperAsk(string Id, string Asker, string Question, string? Agent, string AskedBy, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByPerson = "person";
}

/// <summary>
/// One ask's state as the driver keeps it, <c>&lt;askId&gt;.state.json</c> (the driver alone writes it; the service reads it as
/// <c>HelperState</c> and finds an ask by the one <c>started</c> naming its helper): the shape MIXAGENT2a-i settled, with three
/// driver-only marks it ignores: <see cref="LastWords"/>, <see cref="Handed"/> and <see cref="Agent"/>.
/// </summary>
/// <param name="Code">A refusal's code (<see cref="HelperWhy"/>), or why a waiting ask waits; null otherwise.</param>
/// <param name="Helper">The helper's session once started.</param>
public sealed record HelperState(string Id, string State, string? Code, string? Helper, DateTimeOffset At)
{
    /// <summary>The answer is the helper's last words, bounded, since it never used its tool.</summary>
    public bool LastWords { get; init; }

    /// <summary>The word id the answer went to the asker as, once handed.</summary>
    public string? Handed { get; init; }

    /// <summary>The agent that was started, for the notes.</summary>
    public string? Agent { get; init; }

    /// <summary>The helper's last words, kept here for the delivery where it never used its tool (<see cref="LastWords"/>); no answer file holds them.</summary>
    public string? Words { get; init; }
}

/// <summary>A helper's answer as <c>&lt;askId&gt;.answer.json</c> keeps it, written by <c>helper_answer</c> once.</summary>
public sealed record HelperAnswer(string Id, string Helper, string Answer, DateTimeOffset At);

/// <summary>
/// The helpers' three files per ask under <c>&lt;home&gt;/helpers</c> (MIXAGENT2a-i, D163 point 2): read as the service writes
/// them and written as the service reads them. The file is the contract; the service's <c>HelperBox</c> and this share no code,
/// and each side's tests hold the same shapes.
/// </summary>
public sealed class HelperFiles(string home)
{
    public const string Folder = "helpers";

    // Indented, and a plus sign and a quote's neighbours left as written: the files are read by people and by the service's box.
    private static readonly JsonWriterOptions Options = new() { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string PathOf(string id, string kind) => Path.Combine(home, Folder, $"{id}.{kind}.json");

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<HelperAsk> Asks()
    {
        var folder = Path.Combine(home, Folder);
        if (!Directory.Exists(folder)) return [];
        var asks = new List<HelperAsk>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.ask.json"))
        {
            if (Parse(path) is not { } root) continue;
            using (root)
            {
                var element = root.RootElement;
                var name = Path.GetFileName(path);
                var id = Text(element, "id") ?? name[..^".ask.json".Length];
                if (Text(element, "asker") is not { Length: > 0 } asker || Text(element, "question") is not { Length: > 0 } question) continue;
                var by = Text(element, "askedBy") == HelperAsk.ByPerson ? HelperAsk.ByPerson : HelperAsk.ByAgent;
                var at = DateTimeOffset.TryParse(Text(element, "at"), out var when) ? when : File.GetLastWriteTimeUtc(path);
                asks.Add(new HelperAsk(id, asker, question, Text(element, "agent") is { Length: > 0 } agent ? agent : null, by, at));
            }
        }

        return [.. asks.OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal)];
    }

    /// <summary>One ask's state, or null where the driver wrote none or it is unreadable.</summary>
    public HelperState? ReadState(string id)
    {
        if (Parse(PathOf(id, "state")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "state") is not { } state || !HelperStates.All.Contains(state)) return null;
            return new HelperState(
                Text(element, "id") ?? id, state, Text(element, "code"), Text(element, "helper"),
                DateTimeOffset.TryParse(Text(element, "at"), out var at) ? at : DateTimeOffset.UtcNow)
            {
                LastWords = element.TryGetProperty("lastWords", out var last) && last.ValueKind == JsonValueKind.True,
                Handed = Text(element, "handed"),
                Agent = Text(element, "agent"),
                Words = Text(element, "words"),
            };
        }
    }

    /// <summary>Keep one ask's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(HelperState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            if (state.Helper is null) writer.WriteNull("helper");
            else writer.WriteString("helper", state.Helper);
            writer.WriteString("at", state.At.ToString("O"));
            if (state.LastWords) writer.WriteBoolean("lastWords", true);
            if (state.Handed is not null) writer.WriteString("handed", state.Handed);
            if (state.Agent is not null) writer.WriteString("agent", state.Agent);
            if (state.Words is not null) writer.WriteString("words", state.Words);
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(Path.Combine(home, Folder));
        AtomicFile.WriteText(PathOf(state.Id, "state"), Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The helper's answer to an ask, or null where it said none or the file is unreadable.</summary>
    public HelperAnswer? ReadAnswer(string id)
    {
        if (Parse(PathOf(id, "answer")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "answer") is { Length: > 0 } answer && Text(element, "helper") is { } helper
                ? new HelperAnswer(Text(element, "id") ?? id, helper, answer, DateTimeOffset.TryParse(Text(element, "at"), out var at) ? at : DateTimeOffset.UtcNow)
                : null;
        }
    }

    /// <summary>
    /// The ask a person's terminal writes, in the shape <c>helper_ask</c> writes (MIXAGENT2a-ii): with <c>askedBy: person</c>,
    /// atomically. Returns its id.
    /// </summary>
    public string WriteAsk(string asker, string question, string? agent, DateTimeOffset at)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("asker", asker);
            writer.WriteString("question", question);
            if (agent is null) writer.WriteNull("agent");
            else writer.WriteString("agent", agent);
            writer.WriteString("askedBy", HelperAsk.ByPerson);
            writer.WriteString("at", at.ToString("O"));
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(Path.Combine(home, Folder));
        AtomicFile.WriteText(PathOf(id, "ask"), Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
        return id;
    }

    private static JsonDocument? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
