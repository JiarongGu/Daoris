using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a working session's ask for a helper, and a helper's answer, are written (MIXAGENT2a-i, D163 point 2; the mixed agents
/// design §3). Three files under the home's <c>helpers</c> folder, one writer each, the file the contract:
/// <c>&lt;askId&gt;.ask.json</c> (the connector's <c>helper_ask</c>, or the person's door, and never changed once written),
/// <c>&lt;askId&gt;.state.json</c> (the driver's alone) and <c>&lt;askId&gt;.answer.json</c> (the helper's <c>helper_answer</c>).
/// </summary>
/// <remarks>
/// <para><b>This judges what only the record can say</b>: that a session speaks, that it is a working session and not a
/// helper, a reviewer or an intake (a helper cannot ask for a helper). Whether the rule allows an ask, the two at once and which
/// agent are the driver's, which alone reads <c>driver.json</c>; it answers a refusal through the state file's code.</para>
///
/// <para><b>Never fed to a remote or served over HTTP</b>, as <see cref="RuleProposalBox"/>'s files are: the driver's helper
/// session (a later row) reads them from the home. The driver's <c>HelperAsks</c> and this share no code; each side's tests hold
/// the same shapes.</para>
/// </remarks>
/// <param name="home">The home the files go under, or null when there is none: every ask is then refused.</param>
public sealed class HelperBox(string? home)
{
    /// <summary>The longest question and answer kept, in characters: a helper's answer is one turn's, not a transcript.</summary>
    public const int MaxQuestion = 4000;

    public const int MaxAnswer = 20000;

    /// <summary>The states the driver writes, in the order an ask goes through them (<c>refused</c> ends it with a code).</summary>
    public static readonly IReadOnlyList<string> States = ["waiting", "approved", "started", "answered", "refused"];

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no one a helper's answer could come back to.";

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the rules live.</summary>
    public static HelperBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one ask's files are: <c>&lt;home&gt;/helpers/&lt;id&gt;.&lt;kind&gt;.json</c>, the home's <see cref="HomeEntry.Helpers"/>.</summary>
    public static string PathOf(string home, string id, string kind) => DaorisHome.Entry(home, HomeEntry.Helpers, $"{id}.{kind}.json");

    /// <summary>
    /// Write an ask for the session the connector names, or refuse it: answered as the sentence an agent reads.
    /// </summary>
    /// <param name="asker">The record of the session that asks, or null when the connector names none or it is not held.</param>
    /// <param name="named">The session the connector names, said back when no record is held for it.</param>
    /// <param name="urgent">
    /// The asker's level (COWORK1d, D188 point 2): the answer at its next step, where its door takes words then; kept as `urgent: true`.
    /// An ordinary ask keeps no field, which reads as today's delivery.
    /// </param>
    public (string? Id, string Message) Ask(Session? asker, string? named, string? question, string? agent, DateTimeOffset at, bool urgent = false)
    {
        if (string.IsNullOrWhiteSpace(named)) return (null, $"{NoSession} Nothing was asked.");
        if (asker is not { Origin: null }) return (null, $"No session `{named.Trim()}` of this machine's is held to bring an answer back to. Nothing was asked.");
        if (Unfit(asker) is { } unfit) return (null, $"{unfit} Nothing was asked.");

        var words = question?.Trim() ?? "";
        if (words.Length == 0) return (null, "A helper needs the question: what you want answered, with what it should read. Nothing was asked.");
        if (words.Length > MaxQuestion) return (null, $"A question is at most {MaxQuestion} characters, and this is {words.Length}. Nothing was asked.");
        var chosen = string.IsNullOrWhiteSpace(agent) ? null : agent.Trim();
        if (chosen is not null && chosen.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return (null, $"An agent is named as one word, as it is listed in Settings, and `{chosen}` is not. Nothing was asked.");
        }

        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was asked.");

        var id = Guid.NewGuid().ToString("N")[..8];
        Write(PathOf(Home, id, "ask"), writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("asker", asker.Id);
            writer.WriteString("question", words);
            if (chosen is null) writer.WriteNull("agent");
            else writer.WriteString("agent", chosen);
            writer.WriteString("askedBy", "agent");
            if (urgent) writer.WriteBoolean("urgent", true);
            writer.WriteString("at", Moments.Write(at));
        });

        return (id,
            $"Asked `#{id}`: the driver takes it at its next look, and starts it once your turn has ended, since it reads your work "
            + "as it stands then, what you have not committed included. The person's rule for helpers decides whether it starts "
            + "or waits for their yes, or is refused; whichever it is, the answer, or the reason there is none, comes back to you "
            + (urgent
                ? "at your next step where a later turn of yours is working when it comes, where your door takes words then, and otherwise as your next turn, marked as another agent's."
                : "as your next turn, marked as another agent's."));
    }

    /// <summary>
    /// Write a helper's answer to the ask the driver started it for, or refuse it. The ask is the one a state file names this
    /// session the helper of, while it is <c>started</c>; and an ask is answered once.
    /// </summary>
    public (bool Kept, string Message) Answer(Session? helper, string? named, string? words, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(named)) return (false, "This connector speaks for no session the driver started here, so there is no question for it to answer.");
        if (helper is not { Origin: null, Serves: not null }) return (false, $"Session `{named.Trim()}` is no helper: only a session the driver started to answer a question answers one.");
        if (Home is null) return (false, $"{Capital(DaorisHome.Sentence)} Nothing was kept.");

        var folder = DaorisHome.Entry(Home, HomeEntry.Helpers);
        var ask = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.state.json")
                .Select(path => ReadState(Home, Path.GetFileName(path)[..^".state.json".Length]))
                .FirstOrDefault(state => state is { State: "started" } && string.Equals(state.Helper, helper.Id, StringComparison.Ordinal))
            : null;
        if (ask is null) return (false, $"No question waits for session `{helper.Id}`'s answer: the driver names the question it started you for, and none is named.");
        if (File.Exists(PathOf(Home, ask.Id, "answer"))) return (false, $"Question `#{ask.Id}` is already answered, and an answer is kept once.");

        var text = words?.Trim() ?? "";
        if (text.Length == 0) return (false, "The helper's answer needs your answer in words: what you found or concluded, and what you did not read. Nothing was kept.");
        if (text.Length > MaxAnswer) return (false, $"An answer is at most {MaxAnswer} characters, and this is {text.Length}. Say it shorter. Nothing was kept.");

        Write(PathOf(Home, ask.Id, "answer"), writer =>
        {
            writer.WriteString("id", ask.Id);
            writer.WriteString("helper", helper.Id);
            writer.WriteString("answer", text);
            writer.WriteString("at", Moments.Write(at));
        });
        return (true, $"Your answer is kept for question `#{ask.Id}`; it goes back to the session that asked, marked as yours. You can end now.");
    }

    /// <summary>
    /// Whether an asker's record is no working session that may ask: a helper, a reviewer or an intake never asks for a helper,
    /// and a teammate's record is no session here. The sentence says which, or null when it may.
    /// </summary>
    private static string? Unfit(Session asker) =>
        asker.Serves is { } serves
            ? $"Session `{asker.Id}` is itself a helper for session `{serves}`, and a helper cannot ask for a helper."
        : asker.Opinion is { } opinion
            ? $"Session `{asker.Id}` reads another session's work for second opinion `{opinion}`, and a reviewer cannot ask for a helper."
        : asker.Ask is { } ask
            ? $"Session `{asker.Id}` is the intake for ask `#{ask}`, which does no work in a repository, and an intake cannot ask for a helper."
        : null;

    /// <summary>One ask's state as the driver wrote it, or null when there is none or it is unreadable.</summary>
    public static HelperState? ReadState(string home, string id)
    {
        try
        {
            var path = PathOf(home, id, "state");
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return Text("state") is { } state && States.Contains(state)
                ? new HelperState(Text("id") ?? id, state, Text("code"), Text("helper"), Text("at"))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    // Hand-rolled and atomic, for the reason every store here is: nothing may stop working under AOT, and a driver reading the
    // folder must never see half a file.
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

/// <summary>
/// One ask's state, written by the driver alone: <c>waiting</c> for the person's yes, <c>approved</c>, <c>started</c> (with
/// the helper's session), <c>answered</c>, or <c>refused</c> with a code the asker reads.
/// </summary>
public sealed record HelperState(string Id, string State, string? Code, string? Helper, string? At);
