using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A place an Ask Daoris conversation is about, its focus (D253 point 3's note): this machine, a workspace, or a repository
/// with its workspace, since one name may stand in two. One shape on every side, <c>{ kind, workspace?, repository? }</c>; the
/// twin is the page's <c>help/focus.ts</c>, and <c>HelpFocusTests</c> and <c>focus.test.ts</c> hold the same spellings.
/// </summary>
/// <remarks>Names compare whatever their letters' case, as workspaces are matched everywhere else.</remarks>
public sealed record HelpFocus
{
    public const string MachineKind = "machine";
    public const string WorkspaceKind = "workspace";
    public const string RepositoryKind = "repository";

    private HelpFocus(string kind, string? workspace, string? repository)
    {
        Kind = kind;
        Workspace = workspace;
        Repository = repository;
    }

    /// <summary><see cref="MachineKind"/>, <see cref="WorkspaceKind"/> or <see cref="RepositoryKind"/>.</summary>
    public string Kind { get; }

    /// <summary>The workspace, or the repository's; null for this machine.</summary>
    public string? Workspace { get; }

    /// <summary>The repository's name; null for this machine or a workspace.</summary>
    public string? Repository { get; }

    public static HelpFocus Machine { get; } = new(MachineKind, null, null);

    public static HelpFocus OfWorkspace(string workspace) => new(WorkspaceKind, workspace.Trim(), null);

    public static HelpFocus OfRepository(string workspace, string repository) => new(RepositoryKind, workspace.Trim(), repository.Trim());

    /// <summary>
    /// The focus a conversation homed in <paramref name="workspace"/> holds until one is chosen: that workspace, or this machine for
    /// <c>default</c>'s, whose room is the whole machine's.
    /// </summary>
    public static HelpFocus Home(string? workspace) =>
        string.IsNullOrWhiteSpace(workspace) || string.Equals(workspace.Trim(), RemoteTarget.DefaultWorkspace, StringComparison.OrdinalIgnoreCase)
            ? Machine
            : OfWorkspace(workspace);

    /// <summary>The terminal's spelling: <c>machine</c>, <c>&lt;workspace&gt;</c> or <c>&lt;workspace&gt;/&lt;repository&gt;</c>.</summary>
    public string Said => Kind switch
    {
        WorkspaceKind => Workspace!,
        RepositoryKind => $"{Workspace}/{Repository}",
        _ => MachineKind,
    };

    /// <summary>The place a terminal's words name, or null for none.</summary>
    public static HelpFocus? FromSaid(string? said)
    {
        var words = said?.Trim();
        if (string.IsNullOrEmpty(words)) return null;
        if (words == MachineKind) return Machine;
        var slash = words.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0) return OfWorkspace(words);
        var workspace = words[..slash].Trim();
        var repository = words[(slash + 1)..].Trim();
        return workspace.Length == 0 || repository.Length == 0 || repository.Contains('/', StringComparison.Ordinal)
            ? null
            : OfRepository(workspace, repository);
    }

    /// <summary>The place a JSON object spells, or null for a shape that is none: an unknown kind, or a name it needs left blank.</summary>
    public static HelpFocus? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var workspace = Json.NonEmpty(element, "workspace")?.Trim();
        var repository = Json.NonEmpty(element, "repository")?.Trim();
        return Json.NonEmpty(element, "kind") switch
        {
            MachineKind => Machine,
            WorkspaceKind when workspace is { Length: > 0 } => OfWorkspace(workspace),
            RepositoryKind when workspace is { Length: > 0 } && repository is { Length: > 0 } => OfRepository(workspace, repository),
            _ => null,
        };
    }

    /// <summary>The place as one JSON object, its absent names left out.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Kind);
        if (Workspace is { } workspace) writer.WriteString("workspace", workspace);
        if (Repository is { } repository) writer.WriteString("repository", repository);
        writer.WriteEndObject();
    }

    /// <summary>The place as a route answers it, its absent names left out.</summary>
    public IReadOnlyDictionary<string, string> Wire()
    {
        var wire = new Dictionary<string, string>(StringComparer.Ordinal) { ["kind"] = Kind };
        if (Workspace is { } workspace) wire["workspace"] = workspace;
        if (Repository is { } repository) wire["repository"] = repository;
        return wire;
    }

    /// <summary>Whether <paramref name="other"/> is this place or inside it: this machine holds every place, a workspace its repositories.</summary>
    public bool Holds(HelpFocus other) => Kind switch
    {
        MachineKind => true,
        WorkspaceKind => Same(Workspace, other.Workspace),
        _ => other.Kind == RepositoryKind && Same(Workspace, other.Workspace) && Same(Repository, other.Repository),
    };

    public bool Equals(HelpFocus? other) =>
        other is not null && Kind == other.Kind && Same(Workspace, other.Workspace) && Same(Repository, other.Repository);

    public override int GetHashCode() => HashCode.Combine(
        Kind, Workspace?.ToUpperInvariant(), Repository?.ToUpperInvariant());

    private static bool Same(string? one, string? other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);
}
