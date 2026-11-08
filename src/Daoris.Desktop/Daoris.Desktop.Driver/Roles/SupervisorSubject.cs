using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The typed reference a supervisor's decision names its subject by, which does not change while the subject waits: a question's
/// <c>{ session, question }</c>, a referred point's <c>{ session, point }</c>. Every form, decision file, keeping, release and closed mark
/// carries it whole, and the proof is over it, so a word about one subject is never read as another's.
/// </summary>
/// <param name="Id">The subject's own id within its session: the recorded question's, or the referred point's.</param>
public sealed record SupervisorSubject(string Kind, string Session, string Id)
{
    /// <summary>The longest session or id a subject names.</summary>
    public const int Most = 128;

    // The kinds the decision door takes in this build, each with the name its id goes by on the wire.
    private static readonly IReadOnlyDictionary<string, string> Named = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SupervisorKinds.Question] = "question",
        [SupervisorKinds.Course] = "point",
        [SupervisorKinds.Limit] = "point",
        [SupervisorKinds.Landing] = "landing",
        [SupervisorKinds.Allow] = "request",
        [SupervisorKinds.Files] = "text",
    };

    /// <summary>The kinds whose subject this build reads, in the kinds' order.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [.. SupervisorKinds.All.Where(Named.ContainsKey)];

    public static SupervisorSubject Question(string session, string question) => new(SupervisorKinds.Question, session, question);

    public static SupervisorSubject Point(string session, string point, string kind = SupervisorKinds.Course) => new(kind, session, point);

    public static SupervisorSubject Landing(string session, string run, string node, int attempt) =>
        new(SupervisorKinds.Landing, session, $"{run}/{node}/{attempt}");

    /// <summary>The run, node and attempt of a landing's id. A node id may hold <c>/</c>, so the run is read up to the first one and the attempt after the last.</summary>
    public (string Run, string Node, int Attempt)? LandingParts =>
        Kind == SupervisorKinds.Landing && Id.IndexOf('/') is > 0 and var first && Id.LastIndexOf('/') is var last && last > first + 1
        && int.TryParse(Id[(last + 1)..], out var number)
            ? (Id[..first], Id[(first + 1)..last], number)
            : null;

    /// <summary>The moment a held request began, so a request id a later process reuses is another subject.</summary>
    public string? Held { get; init; }

    /// <summary>A held permission request <c>{ session, request, held }</c>, the allow kind's first subtype.</summary>
    public static SupervisorSubject Request(string session, string request, string held) => new(SupervisorKinds.Allow, session, request) { Held = held };

    /// <summary>A widening a session proposed, <c>{ proposal }</c>, the allow kind's second subtype: it names no session.</summary>
    public static SupervisorSubject Proposal(string proposal) => new(SupervisorKinds.Allow, "", proposal);

    /// <summary>A steward's act outside its grants, <c>{ ask }</c>, the allow kind's third subtype: it names no session.</summary>
    public static SupervisorSubject StewardAsk(string ask) => new(SupervisorKinds.Allow, "", ask) { IsAsk = true };

    public bool IsAsk { get; init; }

    /// <summary>The start that met a rule file's ask, and which act it is (<c>run</c>, <c>copy</c> or <c>share</c>): a <c>files</c> subject only.</summary>
    public (string Start, string What)? Act { get; init; }

    /// <summary>A rule file's ask <c>{ repository, start, what, text }</c>: the repository is its <see cref="Session"/>, the held act's own text its id.</summary>
    public static SupervisorSubject Ask(string repository, string start, string what, string text) =>
        new(SupervisorKinds.Files, repository, text) { Act = (start, what) };

    /// <summary>Whether this is an allow's widening, which names a proposal and no session.</summary>
    public bool IsProposal => Kind == SupervisorKinds.Allow && Session.Length == 0 && !IsAsk;

    /// <summary>The name the subject's id goes by: <c>question</c>, <c>point</c>, <c>landing</c>, <c>request</c>, <c>proposal</c> or <c>ask</c>.</summary>
    public string IdName => IsAsk ? "ask" : IsProposal ? "proposal" : Named[Kind];

    /// <summary>The subject as the wire carries it, its fields in their order.</summary>
    public IReadOnlyDictionary<string, string> Wire()
    {
        var wire = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Act is { } met)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = Session, ["start"] = met.Start, ["what"] = met.What, ["text"] = Id };
        }

        if (!IsProposal && !IsAsk) wire["session"] = Session;
        wire[IdName] = Id;
        if (Held is not null) wire["held"] = Held;
        return wire;
    }

    /// <summary>The subject of <paramref name="kind"/> that <paramref name="value"/> is, exactly its fields; null for anything more or less.</summary>
    public static SupervisorSubject? Read(string kind, JsonElement value)
    {
        if (!Named.TryGetValue(kind, out var idName) || value.ValueKind != JsonValueKind.Object) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        if (kind == SupervisorKinds.Files)
        {
            return fields.Count == 4 && Text(fields, "repository") is { } repository && Text(fields, "start") is { } start
                && Text(fields, "what") is { } what && Text(fields, "text") is { } text
                ? Ask(repository, start, what, text)
                : null;
        }

        if (kind == SupervisorKinds.Allow)
        {
            if (fields.Count == 1)
            {
                return Text(fields, "proposal") is { } proposal ? Proposal(proposal) : Text(fields, "ask") is { } ask ? StewardAsk(ask) : null;
            }

            return fields.Count == 3 && Text(fields, "session") is { } held && Text(fields, "request") is { } request && Text(fields, "held") is { } since
                ? Request(held, request, since)
                : null;
        }

        return fields.Count == 2 && Text(fields, "session") is { } session && Text(fields, idName) is { } id ? new(kind, session, id) : null;
    }

    private static string? Text(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= Most } text
        && text.Trim().Length > 0 ? text : null;
}
