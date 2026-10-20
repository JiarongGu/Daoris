using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a working session's agent asks to hand its session over to another agent (MIXAGENT1c, D163 point 3; the mixed agents
/// design §2 and §5): two files under the home's <c>sessions</c> folder, named by the session, one writer each, the file the
/// contract: <c>&lt;session&gt;.hand-over-ask.json</c> (the connector's <c>handover_propose</c>, replaced only once the ask before is
/// settled) and <c>&lt;session&gt;.hand-over-state.json</c> (the driver's alone, naming the ask it settled by its id).
/// </summary>
/// <remarks>
/// <para><b>This judges what only the record can say</b>: that a session speaks, that it is a working session and not a helper, a
/// reviewer, an intake or Ask Daoris's own conversation, and that no ask of its own waits already. The person's <c>handOver</c> rule,
/// the next agent in their order and whether the session had a turn since it was handed over are the driver's, which alone reads
/// <c>driver.json</c> and the session's turns; its verdict is the state file's.</para>
///
/// <para><b>Not the hand-over's box.</b> <c>&lt;session&gt;.hand-over.json</c> is taken by whoever runs the session at once; this ask
/// becomes one only where the rule or the person's yes says so. The driver's <c>HandOverAskFiles</c> and this share no code; each side's
/// tests hold the same shapes (<c>McpHandOverToolTests</c>, <c>HandOverAskFilesTests</c>).</para>
/// </remarks>
/// <param name="home">The home the files go under, or null when there is none: every ask is then refused.</param>
public sealed class HandOverAskBox(string? home)
{
    /// <summary>The longest note kept, in characters: where the work stands, not a transcript.</summary>
    public const int MaxNote = 4000;

    public const string AskSuffix = ".hand-over-ask.json";

    public const string StateSuffix = ".hand-over-state.json";

    /// <summary>The states the driver writes: <c>waiting</c> on the person, <c>approved</c>, <c>taken</c>, or <c>refused</c> with a code.</summary>
    public static readonly IReadOnlyList<string> States = ["waiting", "approved", "taken", "refused"];

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no session of yours to hand over.";

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the session's files live.</summary>
    public static HandOverAskBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one session's file is: <c>&lt;home&gt;/sessions/&lt;session&gt;&lt;suffix&gt;</c>, the home's <see cref="HomeEntry.Sessions"/>.</summary>
    public static string PathOf(string home, string session, string suffix) => DaorisHome.Entry(home, HomeEntry.Sessions, session + suffix);

    /// <summary>Write the ask for the session the connector names, or refuse it: answered as the sentence the agent reads.</summary>
    /// <param name="asker">The record of the session that asks, or null when the connector names none or it is not held.</param>
    /// <param name="named">The session the connector names, said back when no record is held for it.</param>
    public (string? Id, string Message) Ask(Session? asker, string? named, string? note, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(named)) return (null, $"{NoSession} Nothing was asked.");
        if (asker is not { Origin: null }) return (null, $"No session `{named.Trim()}` of this machine's is held to hand over. Nothing was asked.");
        if (Unfit(asker) is { } unfit) return (null, $"{unfit} Nothing was asked.");

        var words = note?.Trim() ?? "";
        if (words.Length == 0)
        {
            return (null, "A hand-over needs your note: where the work stands, what is left and what the next agent should read first. Nothing was asked.");
        }

        if (words.Length > MaxNote) return (null, $"A note is at most {MaxNote} characters, and this is {words.Length}. Nothing was asked.");
        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was asked.");

        if (ReadAsk(Home, asker.Id) is { } before)
        {
            var settled = ReadState(Home, asker.Id) is { } state && state.Id == before.Id ? state : null;
            if (settled is { State: "taken" })
            {
                return (null, $"Session `{asker.Id}` is being handed over by your ask `#{before.Id}` already. Nothing was asked.");
            }

            if (settled is not { State: "refused" })
            {
                return (null,
                    $"Your ask `#{before.Id}` to hand this session over already waits for the person's rule or their word: one ask at a time. "
                    + "Nothing was asked.");
            }
        }

        var id = Guid.NewGuid().ToString("N")[..8];
        Write(PathOf(Home, asker.Id, AskSuffix), writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("session", asker.Id);
            writer.WriteString("note", words);
            writer.WriteString("askedBy", "agent");
            writer.WriteString("at", Moments.Write(at));
        });

        return (id,
            $"Asked `#{id}`, your note kept as your own claim. Daoris settles it at its next look by the person's rule for hand-overs: "
            + "under `ask` it waits for their yes or no on this session's page, under `on` this session goes on with the next agent in "
            + "their order once your turn ends, and under `off` it is refused. Whichever it is, this session's timeline says so. Finish "
            + "your turn as you would: commit what you have, keep your quest taken if you hold one, say where you are in your last "
            + "message, and end your turn.");
    }

    /// <summary>
    /// Whether an asker's record is no working session that may hand over: a helper, a reviewer, an intake or Ask Daoris's own
    /// conversation never goes on with another agent. The sentence says which, or null when it may.
    /// </summary>
    private static string? Unfit(Session asker) =>
        asker.Serves is { } serves
            ? $"Session `{asker.Id}` is a helper for session `{serves}`, and a helper cannot hand over: it answers one question."
        : asker.Opinion is { } opinion
            ? $"Session `{asker.Id}` reads another session's work for second opinion `{opinion}`, and a reviewer cannot hand over."
        : asker.Ask is { } ask
            ? $"Session `{asker.Id}` is the intake for ask `#{ask}`, which does no work in a repository, and an intake cannot hand over."
        : asker.HelpMode is not null || string.Equals(asker.Repository, SessionLedger.HelpRepository, StringComparison.Ordinal)
            ? $"Session `{asker.Id}` is Ask Daoris's own conversation, which stays on its own agent."
        : null;

    /// <summary>The ask kept for a session, or null when there is none or it does not read.</summary>
    public static HandOverAskKept? ReadAsk(string home, string session)
    {
        try
        {
            var path = PathOf(home, session, AskSuffix);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return Text("id") is { Length: > 0 } id && Text("note") is { } note
                ? new HandOverAskKept(id, Text("session") ?? session, note, Text("askedBy") ?? "agent", Text("at"))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>A session's ask's state as the driver wrote it, or null when there is none or it is unreadable.</summary>
    public static HandOverAskState? ReadState(string home, string session)
    {
        try
        {
            var path = PathOf(home, session, StateSuffix);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return Text("id") is { Length: > 0 } id && Text("state") is { } state && States.Contains(state)
                ? new HandOverAskState(id, state, Text("code"), Text("agent"), Text("at"))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    // Hand-rolled and atomic, as the helpers' box writes: nothing may stop working under AOT, and a driver reading the folder must
    // never see half a file.
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

/// <summary>A session's ask to hand over as <c>handover_propose</c> wrote it: its id, the session, the agent's note, and when.</summary>
public sealed record HandOverAskKept(string Id, string Session, string Note, string AskedBy, string? At);

/// <summary>
/// A session's ask's state, written by the driver alone: <c>waiting</c> on the person's word, <c>approved</c>, <c>taken</c> (the
/// hand-over kept or done, with the agent it goes on with), or <c>refused</c> with a code.
/// </summary>
public sealed record HandOverAskState(string Id, string State, string? Code, string? Agent, string? At);
