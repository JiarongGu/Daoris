using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fields = Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>A problem the editor holds open on the draft, as its judgement said it: the code, the step or way, and the words.</summary>
/// <param name="Way">The way it is about, by its id: the file's <c>wire</c>.</param>
public sealed record WorkflowDraftProblem(string Code, string? Node, string? Way, string Words);

/// <summary>
/// The palette a draft is composed from, compact (the kinds, their options, outcomes and outputs installed here) under its digest: the
/// room has no reader of it, so a conversation is handed it with its first draft and again when the digest moves.
/// </summary>
/// <param name="Text">The compact palette, or null where this conversation was told it under this digest already.</param>
public sealed record WorkflowDraftPalette(string Digest, string? Text);

/// <summary>
/// The workflow draft open in the person's editor, told to Ask Daoris beside their words (FLOWCHART2d, D223; the flowchart design §4
/// and §8.3): its graph exactly as the editor holds it, the fingerprint the editor's judgement keys on, the place it is for, where it came
/// from and what it would save to, its route, its pipeline, the step selected and the problems open. Bounded as a whole, and refused
/// past the bound, never cut. Agent-facing, so its words are Daoris's one copy (D142 point 6).
/// </summary>
public sealed partial record WorkflowDraftTold(string Fingerprint, string Place, JsonElement Graph)
{
    /// <summary>
    /// The most a draft carries, in characters of its JSON: under the record's own bound on a note's text
    /// (<see cref="SessionEventText.TextLimit"/>), so the note that keeps it as told keeps it whole.
    /// </summary>
    public const int Limit = 48 * 1024;

    /// <summary>The saved workflow it adds a version to, or the id it would save as new; null where it names none yet.</summary>
    public string? Workflow { get; init; }

    /// <summary>The version it was opened from; null for a draft from a preset or from nothing.</summary>
    public int? Base { get; init; }

    /// <summary>Where it came from, as the editor names it: <c>preset:&lt;id&gt;</c>, <c>&lt;workflow&gt;@v&lt;n&gt;</c> or <c>new</c>.</summary>
    public string? Source { get; init; }

    /// <summary>The route the place chooses it for, where the editor was opened on one: <c>default</c>, <c>landed</c> or <c>kind:&lt;id&gt;</c>.</summary>
    public string? Route { get; init; }

    /// <summary>The pipeline its save would keep, <c>development</c> or <c>verification</c>; null where the editor said none.</summary>
    public string? Pipeline { get; init; }

    /// <summary>The step the person has selected, by its id; null where none is.</summary>
    public string? Selected { get; init; }

    /// <summary>The problems the editor's judgement holds open on it.</summary>
    public IReadOnlyList<WorkflowDraftProblem> Problems { get; init; } = [];

    /// <summary>The palette it is composed from, handed beside it by the host; null where none was read.</summary>
    public WorkflowDraftPalette? Palette { get; init; }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintShape();

    /// <summary>Whether <paramref name="fingerprint"/> is one a draft may carry: letters, digits, <c>.</c>, <c>_</c>, <c>:</c> and <c>-</c>, at most 128.</summary>
    public static bool IsFingerprint(string? fingerprint) => fingerprint is not null && FingerprintShape().IsMatch(fingerprint);

    /// <summary>A draft as the page sends it; the palette is the host's to add.</summary>
    /// <exception cref="DriverException">A draft past <see cref="Limit"/>, or one missing what it names, in the sentence the person reads.</exception>
    public static WorkflowDraftTold Read(JsonElement draft)
    {
        if (draft.ValueKind != JsonValueKind.Object) throw new DriverException("the open workflow draft is a JSON object: its fingerprint, its place and its graph.");
        var size = draft.GetRawText().Length;
        if (size > Limit)
        {
            throw new DriverException(string.Create(CultureInfo.InvariantCulture,
                $"the open workflow draft is {size} characters, more than the {Limit} a message carries to Ask Daoris, so neither the draft nor your words were sent: the draft is never cut to fit."));
        }

        var fingerprint = Fields.Text(draft, "fingerprint");
        if (!IsFingerprint(fingerprint))
        {
            throw new DriverException("the open workflow draft names its fingerprint: letters, digits, `.`, `_`, `:` and `-`, at most 128.");
        }

        if (Fields.NonEmpty(draft, "place")?.Trim() is not { Length: > 0 } place)
        {
            throw new DriverException("the open workflow draft names its place: the repository or workspace its workflow is for.");
        }

        if (!draft.TryGetProperty("graph", out var graph) || graph.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException("the open workflow draft carries its graph, the editor's JSON object of its steps and ways.");
        }

        return new WorkflowDraftTold(fingerprint!, place, graph.Clone())
        {
            Workflow = Fields.NonEmpty(draft, "workflow"),
            Base = draft.TryGetProperty("base", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole) && whole > 0 ? whole : null,
            Source = Fields.NonEmpty(draft, "source"),
            Route = Fields.NonEmpty(draft, "route"),
            Pipeline = Fields.NonEmpty(draft, "pipeline"),
            Selected = Fields.NonEmpty(draft, "selected"),
            Problems = [.. Fields.Items(draft, "problems").Select(ProblemOf).OfType<WorkflowDraftProblem>()],
            // Kept beside a draft as told, so one read back from the record names the palette it was composed from.
            Palette = Fields.NonEmpty(draft, "palette") is { } digest ? new WorkflowDraftPalette(digest, null) : null,
        };
    }

    private static WorkflowDraftProblem? ProblemOf(JsonElement problem) =>
        problem.ValueKind == JsonValueKind.Object && Fields.NonEmpty(problem, "code") is { } code && Fields.NonEmpty(problem, "words") is { } words
            ? new WorkflowDraftProblem(code, Fields.NonEmpty(problem, "node"), Fields.NonEmpty(problem, "wire"), words)
            : null;

    private static readonly JsonWriterOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The draft as it is told and kept, one compact JSON object, the palette left out: what <see cref="Read"/> reads back.</summary>
    public string Json
    {
        get
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Compact))
            {
                writer.WriteStartObject();
                writer.WriteString("fingerprint", Fingerprint);
                writer.WriteString("place", Place);
                if (Workflow is { } workflow) writer.WriteString("workflow", workflow);
                if (Base is { } version) writer.WriteNumber("base", version);
                if (Source is { } source) writer.WriteString("source", source);
                if (Route is { } route) writer.WriteString("route", route);
                if (Pipeline is { } pipeline) writer.WriteString("pipeline", pipeline);
                if (Selected is { } selected) writer.WriteString("selected", selected);
                if (Palette is { } palette) writer.WriteString("palette", palette.Digest);
                writer.WriteStartArray("problems");
                foreach (var problem in Problems)
                {
                    writer.WriteStartObject();
                    writer.WriteString("code", problem.Code);
                    if (problem.Node is { } node) writer.WriteString("node", node);
                    if (problem.Way is { } way) writer.WriteString("wire", way);
                    writer.WriteString("words", problem.Words);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WritePropertyName("graph");
                Graph.WriteTo(writer);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }
    }

    /// <summary>What leads the draft to the agent: what it is, and what it proposes against it with.</summary>
    public string Lead =>
        "The person has a workflow draft open in its editor, not saved, and their words are about it. To change it, call `workflow_propose` "
        + "with `draft` set to its fingerprint and `graph` the whole changed draft: nothing is saved or chosen, and the person takes the "
        + "changes into the draft and saves it themselves."
        + (Palette is { Text: { Length: > 0 } text } ? $"\nWhat a step graph may hold here (palette `{Palette.Digest}`):\n{text}" : "")
        + "\nThe draft:";

    /// <summary>What the agent is handed: <see cref="Lead"/>, then the draft's <see cref="Json"/>.</summary>
    public string Words => $"{Lead}\n{Json}";

    /// <summary>
    /// The drafts a conversation's record says it was told, each by its fingerprint, the newest kept where one was told twice: what the
    /// draft door judges a proposal against. A note that does not read is no draft told.
    /// </summary>
    public static IReadOnlyDictionary<string, WorkflowDraftTold> Told(IEnumerable<SessionEvent> events)
    {
        var told = new Dictionary<string, WorkflowDraftTold>(StringComparer.Ordinal);
        foreach (var each in events)
        {
            if (each is not { Kind: SessionEventKind.Note, Parts: [{ Code: var code }, .., { Code: null, Words: { } json }] } || code != NoteCodes.ChatToldDraft.Code) continue;
            try
            {
                using var document = JsonDocument.Parse(json);
                var draft = Read(document.RootElement);
                told[draft.Fingerprint] = draft;
            }
            catch (Exception error) when (error is JsonException or DriverException)
            {
                // Not a draft as told: nothing a proposal is judged against.
            }
        }

        return told;
    }
}
