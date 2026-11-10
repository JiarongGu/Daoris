using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Registry;

// The registry and the index a host keeps over it: a repository's description of itself, its retirement and re-wiring, the bootstrap
// scan, the code map and the rescan. A shared host is fed, not scanned, and answers the import and the rescan as a state.

/// <summary>
/// Where <c>daoris repository connect</c> lands: a repository's description of itself, persisted, because for a remote service the pushed
/// registrations are the family. <c>Workspace</c> is null when the client said nothing, which preserves the row; <c>DefaultBranch</c>
/// likewise. The commit and its base are a checkout's, read by the rules of a feed's: a shared host orders declarations by them.
/// <c>Adopted</c> is false only from a door that looked and found no manifest. <c>Lanes</c> are the words of the lanes it declares,
/// never their paths; null is unstated and preserves the row's, as a null <c>Kind</c> (<c>git</c> or <c>folder</c>) does./// </summary>
public sealed record RegisterRepository(
    string Repository, IReadOnlyList<string>? Packs, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge, string? Workspace, string? DefaultBranch,
    string? Commit = null, DateTimeOffset? CommittedAt = null, string? Branch = null, string? Base = null,
    bool? Adopted = null, IReadOnlyList<LaneWire>? Lanes = null, string? Kind = null,
    IReadOnlyList<string>? Excludes = null) : IRoute<RegisteredResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/registry";
}

/// <summary>Ends a registration; nothing on disk is touched.</summary>
public sealed record RetireRepository(string Repository) : IRoute<RetiredResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/registry/{repository}";
}

/// <summary>Re-wiring only: the workspace, and nothing else about the row. A re-wire must name where to.</summary>
public sealed record WireRepository(string Repository, string? Workspace) : IRoute<RegisteredResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/registry/{repository}/workspace";
}

/// <summary>The bootstrap, as a verb a person runs: a folder's immediate subdirectories, registered into <c>Workspace</c> where one is named.</summary>
public sealed record ImportRepositories(string? Folder, string? Workspace = null) : IRoute<ImportedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/registry/import";
}

/// <summary>The registered repositories, every circle's or the one named.</summary>
public sealed record ListRegistry(string? Workspace = null) : IRoute<RegistrationResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/registry";
}

/// <summary>
/// A repository's code map: its modules and how they depend on each other, from its own committed file or what is fed or
/// brought down. The answer is the code map wire (<c>CodeMapWire</c>), the shape a machine's host reads when it brings a map down.
/// </summary>
public sealed record ReadCodeMap(string Repository) : IRoute<JsonObject>
{
    public static string Method => "GET";
    public static string Path => "/api/code-map/{repository}";
}

/// <summary>A rescan of every checkout this host sees.</summary>
public sealed record RefreshIndex : IRoute<RefreshResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/refresh";
}

/// <summary>The retires this machine's checkouts owe a circle, which the driver's pass carries to the circle's deployment.</summary>
public sealed record ReadRetiredPending(string? Workspace = null) : IRoute<RetiredPendingResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/registry/retired";
}

/// <summary>Clears one retire a circle is owed, once its deployment has been told.</summary>
public sealed record ClearRetired(string Repository, string? Workspace = null) : IRoute<RetiredResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/registry/retired/{repository}";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegisterRepository))]
[JsonSerializable(typeof(RetireRepository))]
[JsonSerializable(typeof(WireRepository))]
[JsonSerializable(typeof(ImportRepositories))]
[JsonSerializable(typeof(ListRegistry))]
[JsonSerializable(typeof(ReadCodeMap))]
[JsonSerializable(typeof(RefreshIndex))]
[JsonSerializable(typeof(ReadRetiredPending))]
[JsonSerializable(typeof(ClearRetired))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(RetiredResponse))]
[JsonSerializable(typeof(ImportedResponse))]
[JsonSerializable(typeof(RegistrationResponse[]))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RetiredPendingResponse))]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class RegistryJson : JsonSerializerContext;
