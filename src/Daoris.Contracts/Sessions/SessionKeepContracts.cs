namespace Daoris.Contracts.Sessions;

// The doors a local host serves the driver that runs a session's process: the records it opens for the conversations it starts itself,
// and what it learned about a start that the service cannot look at (the service has no binaries or checkouts to observe).

/// <summary>
/// An intake: the record of the conversation the driver opens for an ask, in a room under its home. The circle comes from the ask, never
/// from the caller; the adapter is the harness, never a model, and silence takes the supported one. Answered not found for an unknown
/// ask, a conflict for an ask already answered or a repository busy.
/// </summary>
/// <param name="Room">Where the intake runs, the driver's to name.</param>
public sealed record OpenIntake(
    string? Ask, string? Door, string? Room, string? DoorVersion = null, string? Profile = null, string? Actor = null)
    : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/intake";
}

/// <summary>
/// Ask Daoris's conversation: the record of the conversation about Daoris itself, in the room the driver keeps for it.
/// </summary>
/// <param name="Workspace">The one its window is on at the press, which the record keeps; none is <c>default</c>. One this machine does not hold is not found.</param>
/// <param name="Mode">The access mode its start got, <c>restricted</c> or <c>full</c>; none is <c>restricted</c>. One the ledger does not know is the caller's mistake.</param>
public sealed record OpenHelp(
    string? Door, string? Room, string? DoorVersion = null, string? Profile = null, string? Workspace = null,
    string? Mode = null, string? Actor = null) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/help";
}

/// <summary>Release a parked line's uncommitted paths: all at the person's press, or the <paramref name="Paths"/> a discard took out.</summary>
public sealed record ReleasePlace(string Id, IReadOnlyList<string>? Paths = null) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/release-place";
}

/// <summary>The access mode an Ask Daoris conversation going on runs under, where the person's mode changed since it last ran.</summary>
public sealed record KeepHelpMode(string Id, string? Mode) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/help-mode";
}

/// <summary>The access setup a resumed driven session now runs under, each pause with its value, as its driver resolved it.</summary>
public sealed record KeepAccess(string Id, IReadOnlyDictionary<string, string>? Access) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/access";
}

/// <summary>
/// The actor provenance a start of a record is handed, any record of this machine's. Answered not found, with no body, to a caller off
/// this machine.
/// </summary>
public sealed record KeepActor(
    string Id, string? Choice = null, System.Text.Json.JsonElement? Asked = null, System.Text.Json.JsonElement? Override = null,
    System.Text.Json.JsonElement? Ran = null, System.Text.Json.JsonElement? Chosen = null) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/actor";
}

/// <summary>
/// The downloaded versions a start of the record is handed, <c>agent|tool/&lt;name&gt;/&lt;version&gt;</c>, which the record adds to those
/// of its earlier starts; the downloads' rule keeps each while the session runs.
/// </summary>
public sealed record KeepResolved(string Id, List<string>? Resolved) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/resolved";
}
