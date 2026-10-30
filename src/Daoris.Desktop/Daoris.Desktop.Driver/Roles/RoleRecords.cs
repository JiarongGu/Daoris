using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The work vocabulary a role takes (§2.7), spelled as the participant's work codes spell them, since Roles sits below Participants (as
/// <c>StartPlan.RoleOf</c> does); <c>RoleCatalogTests</c> holds the two alike.
/// </summary>
public static class RoleWork
{
    public const string Build = "build";
    public const string Design = "design";
    public const string Chat = "chat";
    public const string AskDaoris = "ask-daoris";
    public const string Intake = "intake";
    public const string Help = "help";
    public const string Partner = "partner";
    public const string Review = "review";
    public const string Brainstorm = "brainstorm";

    /// <summary>Every work, the closed set, in the participant's order.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Build, Design, Chat, AskDaoris, Intake, Help, Partner, Review, Brainstorm];
}

/// <summary>Where a role works (§2.7): the machine, a workspace or a repository, the most specific scope's role taking a work there.</summary>
/// <param name="Kind"><c>machine</c>, <c>workspace</c> or <c>repository</c>.</param>
/// <param name="Id">The workspace's or repository's id; none for the machine.</param>
public sealed record RoleScope(string Kind, string? Id = null)
{
    public const string Machine = "machine";
    public const string Workspace = "workspace";
    public const string Repository = "repository";

    /// <summary>Every kind, the closed set.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Machine, Workspace, Repository];

    /// <summary>The machine-wide scope, the seed's.</summary>
    public static RoleScope Everywhere { get; } = new(Machine);

    /// <summary>How specific the scope is: the machine 0, a workspace 1, a repository 2.</summary>
    public int Rank => Kind switch { Repository => 2, Workspace => 1, _ => 0 };

    /// <summary>Whether a start at <paramref name="place"/> is inside this scope.</summary>
    public bool Covers(RolePlace place) => Kind switch
    {
        Repository => place.Repository is { } repository && string.Equals(repository, Id, StringComparison.Ordinal),
        Workspace => place.Workspace is { } workspace && string.Equals(workspace, Id, StringComparison.Ordinal),
        _ => true,
    };
}

/// <summary>Where a start happens: the workspace and the repository it is for; both none is the machine.</summary>
public readonly record struct RolePlace(string? Workspace = null, string? Repository = null);

/// <summary>
/// A role of the catalogue (the foundation design §2.7): <c>filled</c> is <c>always</c> for a working role, which <c>takes</c> work and
/// holds no authority, or <c>appointed</c>, which this build does not configure. A record holding both <c>Takes</c> and
/// <c>Authority</c> is a mixed shape, which no door writes and no read accepts.
/// </summary>
public sealed record RoleRecord(
    string Id, string NameEn, string NameZh, string Filled, IReadOnlyList<string> Takes, RoleScope Scope, IReadOnlyList<string>? Authority = null)
{
    public const string Always = "always";
    public const string Appointed = "appointed";

    private static readonly JsonWriterOptions Compacted = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The record as the store keeps it: compact JSON.</summary>
    public string Json()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted))
        {
            writer.WriteStartObject();
            writer.WriteString("id", Id);
            writer.WriteStartObject("name");
            writer.WriteString("en", NameEn);
            writer.WriteString("zh", NameZh);
            writer.WriteEndObject();
            writer.WriteString("filled", Filled);
            if (Takes.Count > 0)
            {
                writer.WriteStartArray("takes");
                foreach (var work in Takes) writer.WriteStringValue(work);
                writer.WriteEndArray();
            }

            if (Authority is not null)
            {
                writer.WriteStartArray("authority");
                foreach (var act in Authority) writer.WriteStringValue(act);
                writer.WriteEndArray();
            }

            writer.WriteStartObject("scope");
            writer.WriteString("kind", Scope.Kind);
            if (Scope.Id is { } id) writer.WriteString("id", id);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>
    /// The record <paramref name="json"/> holds for <paramref name="id"/>, or why it does not read: a mixed record, an appointed one and an
    /// unknown work each read as unreadable, never as either shape (O3-16).
    /// </summary>
    public static (RoleRecord? Record, string? Why) Parse(string id, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, "is not a role record");
            if (Text(root, "id") != id) return (null, "names another id than its row");
            if (!root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.Object
                || Text(name, "en") is not { } en || Text(name, "zh") is not { } zh)
            {
                return (null, "has no name in English and in Chinese");
            }

            var filled = Text(root, "filled");
            if (filled is not (Always or Appointed)) return (null, "has no `filled` of `always` or `appointed`");

            var hasTakes = root.TryGetProperty("takes", out var takesElement);
            var hasAuthority = root.TryGetProperty("authority", out var authorityElement);
            if ((hasTakes && hasAuthority) || (filled == Appointed && hasTakes) || (filled == Always && hasAuthority))
            {
                return (null, "mixes the working and the appointed shape");
            }

            if (filled == Appointed) return (null, "is an appointed role, which this build does not configure");

            var takes = Strings(hasTakes ? takesElement : default);
            if (takes is null || takes.Count == 0) return (null, "`takes` is not a list of work");
            if (takes.FirstOrDefault(work => !RoleWork.Codes.Contains(work, StringComparer.Ordinal)) is { } unknown)
            {
                return (null, $"takes `{unknown}`, a work this build does not know");
            }

            if (ReadScope(root) is not { } scope) return (null, "has no scope of `machine`, a `workspace` or a `repository` with its id");
            return (new RoleRecord(id, en, zh, filled, takes, scope), null);
        }
        catch (JsonException error)
        {
            return (null, $"does not read as JSON: {error.Message.TrimEnd('.', ' ')}");
        }
    }

    /// <summary>The scope a record that does not read still shows, or null where it shows none: the hold reaches no further than it.</summary>
    public static RoleScope? ScopeOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadScope(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RoleScope? ReadScope(JsonElement root)
    {
        if (!root.TryGetProperty("scope", out var element) || element.ValueKind != JsonValueKind.Object) return null;
        var kind = Text(element, "kind");
        var id = Text(element, "id");
        if (kind is null || !RoleScope.Kinds.Contains(kind, StringComparer.Ordinal)) return null;
        return kind == RoleScope.Machine ? id is null ? RoleScope.Everywhere : null : string.IsNullOrWhiteSpace(id) ? null : new RoleScope(kind, id);
    }

    private static List<string>? Strings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())) return null;
            list.Add(item.GetString()!);
        }

        return list;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
