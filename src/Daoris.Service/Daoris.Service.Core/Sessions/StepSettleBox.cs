using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a working session's agent settles a run's step it holds: <c>&lt;session&gt;.step-settle.json</c> under the home's
/// <c>sessions</c> folder, written by the connector's <c>step_settle</c> (replaced only once the driver answered the one before), and
/// <c>&lt;session&gt;.step-settle-state.json</c> beside it, the driver's alone, naming the settle it answered by its id.
/// </summary>
/// <remarks>
/// This judges what only the record can say: that the connector speaks for a working session of this machine's, not a helper, a reviewer, an
/// intake or Ask Daoris's own conversation, and that no settle of its own waits already. Who holds the step, its epoch, its outcomes and its
/// evidence are the driver's, which settles it at the settle door as the session's agent and answers through the state file and the
/// session's next turn. The driver's <c>StepSettleAsks</c> and this share no code; each side's tests hold the same shape.
/// </remarks>
/// <param name="home">The home the files go under, or null when there is none: every settle is then refused.</param>
public sealed class StepSettleBox(string? home)
{
    public const string AskSuffix = ".step-settle.json";

    public const string StateSuffix = ".step-settle-state.json";

    /// <summary>The longest words kept, in characters, and the most evidence named.</summary>
    public const int MaxWords = 2000;

    public const int MaxEvidence = 12;

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's.</summary>
    public static StepSettleBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one session's file is: <c>&lt;home&gt;/sessions/&lt;session&gt;&lt;suffix&gt;</c>.</summary>
    public static string PathOf(string home, string session, string suffix) => DaorisHome.Entry(home, HomeEntry.Sessions, session + suffix);

    /// <summary>Write the settle for the session the connector names, or refuse it: answered as the sentence the agent reads.</summary>
    public (string? Id, string Message) Ask(
        Session? asker, string? named, string? run, string? node, string? outcome, string? words, IReadOnlyDictionary<string, string>? evidence, int? epoch,
        DateTimeOffset at)
    {
        const string Nothing = " Nothing was settled.";
        if (string.IsNullOrWhiteSpace(named)) return (null, "This connector speaks for no session the driver started here, so it holds no step to settle." + Nothing);
        if (asker is not { Origin: null }) return (null, $"No session `{named.Trim()}` of this machine's is held to settle a step for.{Nothing}");
        if (asker.Opinion is not null || asker.Serves is not null || asker.Ask is not null || SessionLedger.IsHelp(asker))
        {
            return (null, $"Session `{asker.Id}` is no run's working session, and only the working session holds a run's step.{Nothing}");
        }

        var (ran, step) = (run?.Trim().TrimStart('#') ?? "", node?.Trim() ?? "");
        if (ran.Length == 0 || step.Length == 0) return (null, "A settle names the run and the step Daoris handed you, as its words named them." + Nothing);
        var said = words?.Trim() ?? "";
        if (said.Length > MaxWords) return (null, $"Words are at most {MaxWords} characters, and these are {said.Length}.{Nothing}");
        var given = evidence?.Where(each => !string.IsNullOrWhiteSpace(each.Key)).ToList() ?? [];
        if (given.Count > MaxEvidence || given.Any(each => each.Key.Length > 40 || (each.Value?.Length ?? 0) > MaxWords))
        {
            return (null, $"Evidence is at most {MaxEvidence} names of 40 characters, each value at most {MaxWords}.{Nothing}");
        }

        if (Home is null) return (null, $"{char.ToUpperInvariant(DaorisHome.Sentence[0])}{DaorisHome.Sentence[1..]}{Nothing}");
        if (ReadAsk(Home, asker.Id) is { } before && ReadStateId(Home, asker.Id) != before)
        {
            return (null, $"Your settle `#{before}` still waits for Daoris's next look: one at a time.{Nothing}");
        }

        var id = Guid.NewGuid().ToString("N")[..8];
        Write(PathOf(Home, asker.Id, AskSuffix), writer =>
        {
            writer.WriteString("id", id);
            writer.WriteString("session", asker.Id);
            writer.WriteString("run", ran);
            writer.WriteString("node", step);
            if (!string.IsNullOrWhiteSpace(outcome)) writer.WriteString("outcome", outcome.Trim());
            if (said.Length > 0) writer.WriteString("words", said);
            if (epoch is { } held) writer.WriteNumber("epoch", held);
            writer.WriteStartObject("evidence");
            foreach (var (name, value) in given) writer.WriteString(name.Trim(), value?.Trim() ?? "");
            writer.WriteEndObject();
            writer.WriteString("at", Moments.Write(at));
        });

        return (id,
            $"Asked `#{id}`: Daoris settles step `{step}` of run #{ran} at its next look, at the settle door as your agent, and refuses it where you do not "
            + "hold it, its epoch moved, or its outcome or evidence is not what the step names. Commit what you have and end your turn: what it came to, "
            + "and the next step it hands you, is your next turn.");
    }

    /// <summary>The id of the settle kept for a session, or null where none is or it does not read.</summary>
    public static string? ReadAsk(string home, string session) => IdOf(PathOf(home, session, AskSuffix));

    /// <summary>The id of the settle the driver answered for a session, or null where none is or it does not read.</summary>
    public static string? ReadStateId(string home, string session) => IdOf(PathOf(home, session, StateSuffix));

    private static string? IdOf(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    // Hand-rolled and atomic, as the hand-over's box writes: nothing may stop working under AOT, and a driver reading the folder must never see
    // half a file.
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
}
