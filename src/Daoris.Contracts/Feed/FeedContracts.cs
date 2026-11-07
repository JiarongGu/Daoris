using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Feed;

// A shared host is fed by desktops: records and content arrive attributed to the key that carried them, and the quest
// operations they push are ordered here. A local host is fed by nobody, so none of these exist there. The session, channel and
// quest doors speak the wires of Core (<c>SessionWire</c>, <c>ChannelWire</c>, <c>QuestWire</c>), the ones the machines' client writes
// and reads, so their bodies are read by the handler as text and their answers are those wires' JSON.

/// <summary>
/// The session records of a joined repository, keyed by origin and id, upserted whole and never re-judged. The body is the
/// session wire's feed, <c>{"records": [...]}</c>, read as text by the handler.
/// </summary>
public sealed record FeedSessions : IRoute<FeedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/feed/sessions";
}

/// <summary>The team's session records after a revision, in order: every origin but the caller's own. Keyed callers only.</summary>
public sealed record SessionsSince(long? Since = null) : IRoute<JsonObject>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/since";
}

/// <summary>An ask's channel messages, up: the body is the channel wire's feed, <c>{"messages": [...]}</c>.</summary>
public sealed record FeedChannel : IRoute<FeedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/feed/channel";
}

/// <summary>The team's channel messages after a number, every origin but the caller's own. Keyed callers only.</summary>
public sealed record ChannelSince(long? Since = null) : IRoute<JsonObject>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/since";
}

/// <summary>
/// Knowledge content, never vectors. The three provenance fields are the feed's claim about which point in the history it speaks
/// for: nullable on the wire and judged at the door. <c>Base</c> is the held commit the feeding machine asked git about.
/// </summary>
public sealed record FeedEntries(
    string Repository, IReadOnlyList<FeedEntryRecord>? Entries,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null) : IRoute<FeedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/feed/entries";
}

/// <summary>
/// A repository's code map, fed: the file's text as one string, judged whole again at the door, or null when the checkout keeps
/// none at that commit. The provenance is the entries feed's, field for field.
/// </summary>
public sealed record FeedCodeMap(
    string Repository, string? File, string? Map,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null) : IRoute<FeedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/feed/code-map";
}

/// <summary>Which commit each feed of a repository stands on here: what a feeding machine asks git about before it feeds.</summary>
public sealed record ReadFeedHeld(string? Repository = null) : IRoute<FeedHeldResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/feed/held";
}

/// <summary>What the remote accepted after a number, in its order, with the workspace's handshake.</summary>
public sealed record ReadOperations(long? Since = null) : IRoute<JsonObject>
{
    public static string Method => "GET";
    public static string Path => "/api/quests/operations";
}

/// <summary>A push rebased on a base, judged quest by quest through the table every machine uses; the body is the quest wire's push.</summary>
public sealed record PushOperations : IRoute<JsonObject>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/operations";
}

/// <summary>A person's retirement of a machine the handshake counts, asked through their own machine's host (FABRIC-CLOSE1i2).</summary>
public sealed record RetireQuestMachine(string Machine) : IRoute<JsonObject>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/machines/{machine}/retire";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FeedSessions))]
[JsonSerializable(typeof(SessionsSince))]
[JsonSerializable(typeof(FeedChannel))]
[JsonSerializable(typeof(ChannelSince))]
[JsonSerializable(typeof(FeedEntries))]
[JsonSerializable(typeof(FeedCodeMap))]
[JsonSerializable(typeof(ReadFeedHeld))]
[JsonSerializable(typeof(ReadOperations))]
[JsonSerializable(typeof(PushOperations))]
[JsonSerializable(typeof(RetireQuestMachine))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(FeedRefusalResponse))]
[JsonSerializable(typeof(FeedHeldResponse))]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class FeedJson : JsonSerializerContext;
