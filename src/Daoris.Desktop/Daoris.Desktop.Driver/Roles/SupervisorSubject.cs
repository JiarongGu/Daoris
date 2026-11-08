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
    };

    /// <summary>The kinds whose subject this build reads, in the kinds' order.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [.. SupervisorKinds.All.Where(Named.ContainsKey)];

    public static SupervisorSubject Question(string session, string question) => new(SupervisorKinds.Question, session, question);

    public static SupervisorSubject Point(string session, string point, string kind = SupervisorKinds.Course) => new(kind, session, point);

    /// <summary>The name the subject's id goes by: <c>question</c> or <c>point</c>.</summary>
    public string IdName => Named[Kind];

    /// <summary>The subject as the wire carries it, its fields in their order.</summary>
    public IReadOnlyDictionary<string, string> Wire() => new Dictionary<string, string>(StringComparer.Ordinal) { ["session"] = Session, [IdName] = Id };

    /// <summary>The subject of <paramref name="kind"/> that <paramref name="value"/> is, exactly its two fields; null for anything more or less.</summary>
    public static SupervisorSubject? Read(string kind, JsonElement value)
    {
        if (!Named.TryGetValue(kind, out var idName) || value.ValueKind != JsonValueKind.Object) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        return fields.Count == 2 && Text(fields, "session") is { } session && Text(fields, idName) is { } id ? new(kind, session, id) : null;
    }

    private static string? Text(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= Most } text
        && text.Trim().Length > 0 ? text : null;
}
