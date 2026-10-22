using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The service's reading of a piece's box under the home's <c>partners</c> entry (COAGENT1b, D199 point 2; the co-agent design §7.1):
/// <c>&lt;id&gt;.ask.json</c> (written once), <c>&lt;id&gt;.state.json</c> (the driver's alone) and <c>&lt;id&gt;.answer.json</c>, which
/// the partner's <c>partner_answer</c> writes here. The driver's <c>PartnerFiles</c> and this share no code and hold the same shapes
/// (<c>twins.md</c>); each side's tests carry them.
/// </summary>
/// <remarks>Never fed to a remote or served over HTTP, as <see cref="HelperBox"/>'s files are.</remarks>
/// <param name="home">The home the files go under, or null when there is none: every answer is then refused.</param>
public sealed class PartnerBox(string? home)
{
    /// <summary>The longest summary kept, in characters (the design §2.3 step 8).</summary>
    public const int MaxSummary = 20000;

    /// <summary>The longest account of what it could not do.</summary>
    public const int MaxLeft = 4000;

    /// <summary>The states the driver writes (the driver's <c>PartnerStates.All</c>, in the same order).</summary>
    public static readonly IReadOnlyList<string> States =
    [
        "asked", "waiting", "approved", "preparing", "started", "ending", "held", "imported", "answered", "handed", "taken-in",
        "not-taken-in", "discarded", "refused", "uncertain",
    ];

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's (D63).</summary>
    public static PartnerBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    public static string PathOf(string home, string id, string kind) => DaorisHome.Entry(home, HomeEntry.Partners, $"{id}.{kind}.json");

    /// <summary>One piece's ask, the fields this side reads of it; null where none is on file or it is unreadable.</summary>
    public PartnerAskRead? ReadAsk(string id)
    {
        if (Home is null || !Safe(id)) return null;
        return Parse(PathOf(Home, id, "ask"), root =>
            Text(root, "asker") is { Length: > 0 } asker && Text(root, "piece") is { Length: > 0 } piece && Text(root, "base") is { Length: > 0 } @base
                ? new PartnerAskRead(Text(root, "id") ?? id, asker, piece, @base)
                : null);
    }

    /// <summary>One piece's state as the driver wrote it, or null when there is none or it is unreadable.</summary>
    public PartnerStateRead? ReadState(string id)
    {
        if (Home is null || !Safe(id)) return null;
        return Parse(PathOf(Home, id, "state"), root =>
            Text(root, "state") is { } state && States.Contains(state)
                ? new PartnerStateRead(Text(root, "id") ?? id, state, Text(root, "code"), Text(root, "partner"))
                : null);
    }

    /// <summary>
    /// Write a partner's answer for the piece the driver started it for, or refuse it. The piece is the one whose state names this
    /// session its partner while it is <c>started</c>; and a piece is answered once.
    /// </summary>
    public (bool Kept, string Message) Answer(Session? partner, string? named, string? summary, string? left, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(named)) return (false, "This connector speaks for no session the driver started here, so there is no piece for it to answer.");
        if (partner is not { Origin: null, Serves: not null, Piece: { } piece }) return (false, $"Session `{named.Trim()}` is no partner: only a session the driver started to write a piece answers one.");
        if (Home is null) return (false, $"{Capital(DaorisHome.Sentence)} Nothing was kept.");

        if (ReadState(piece) is not { State: "started" } state || !string.Equals(state.Partner, partner.Id, StringComparison.Ordinal))
        {
            return (false, $"No piece waits for session `{partner.Id}`'s answer: the driver names the piece it started you for, and none is named.");
        }

        if (File.Exists(PathOf(Home, piece, "answer"))) return (false, $"Piece `#{piece}` is already answered, and an answer is kept once.");

        var text = summary?.Trim() ?? "";
        if (text.Length == 0) return (false, "A partner's answer needs your summary in words: what you did, what you did not, and what you would need. Nothing was kept.");
        if (text.Length > MaxSummary) return (false, $"A summary is at most {MaxSummary} characters, and this is {text.Length}. Say it shorter. Nothing was kept.");
        var rest = left?.Trim() ?? "";
        if (rest.Length > MaxLeft) return (false, $"What you could not do is at most {MaxLeft} characters, and this is {rest.Length}. Say it shorter. Nothing was kept.");

        Write(PathOf(Home, piece, "answer"), writer =>
        {
            writer.WriteString("id", piece);
            writer.WriteString("partner", partner.Id);
            writer.WriteString("summary", text);
            if (rest.Length == 0) writer.WriteNull("left");
            else writer.WriteString("left", rest);
            writer.WriteString("at", Moments.Write(at));
        });
        return (true, $"Your answer is kept for piece `#{piece}`. Daoris takes the files you left in your copy as your work, and hands both to the session that asked. You can end now.");
    }

    // An id is the driver's own, eight hex characters; anything that could leave the folder is never read.
    private static bool Safe(string id) => id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static T? Parse<T>(string path, Func<JsonElement, T?> read) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return read(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Hand-rolled and atomic, as HelperBox's: nothing may stop working under AOT, and a driver reading the folder never sees half a file.
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

/// <summary>A piece's ask, the fields the service reads of it.</summary>
public sealed record PartnerAskRead(string Id, string Asker, string Piece, string Base);

/// <summary>A piece's state, the fields the service reads of it: where it stands, its code, and the partner's record it names.</summary>
public sealed record PartnerStateRead(string Id, string State, string? Code, string? Partner);
