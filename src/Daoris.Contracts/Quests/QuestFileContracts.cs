using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Quests;

// A local host's alone: what a person reads off a quest that is not a record of the ledger.

/// <summary>
/// One quest's handback whole, by the quest's id or the id of the session that served it, in any state: what
/// <c>daoris session handback</c> prints. Any id is answered, one naming nothing as not found, so a client tells this door from an older host's 404.
/// </summary>
public sealed record ReadHandback(string Id, [property: FromQuery] string? Workspace = null) : IRoute<HandbackResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/handbacks/{id}";
}

/// <summary>
/// A quest's file, whole, so a person reading the drawer can open the screenshot the quest carries. To a caller on this machine only,
/// found by hash and never by a path a caller names. The answer is the file's bytes, not JSON.
/// </summary>
public sealed record ReadQuestAttachment(string Id, string Sha256) : IRoute<QuestFileBytes>
{
    public static string Method => "GET";
    public static string Path => "/api/quests/{id}/attachments/{sha256}";
}

/// <summary>The answer of a route that sends a file: its bytes, with the type the host judged it by.</summary>
public sealed record QuestFileBytes;

/// <summary>
/// One quest's handback as <c>GET /api/handbacks/{id}</c> answers it. <c>Found</c>: whether the id named a quest or a session here.
/// <c>Session</c>: the session the id named, absent for a quest's id. <c>Quest</c>: the quest, with its own note as it keeps it.
/// <c>Handback</c>: where a session's close is recorded on it, whole, with the session that wrote it; absent where none is. <c>Said</c>:
/// the read's words, the same the connector's <c>handback_read</c> answers, which the terminal prints.
/// </summary>
public sealed record HandbackResponse(
    string Id, bool Found, string? Session, HandbackQuestResponse? Quest, HandbackRecordResponse? Handback, string Said);

/// <summary>The quest a handback hands back: its name, where it stands, between whom, and its note as the quest keeps it.</summary>
public sealed record HandbackQuestResponse(string Id, string Title, string Status, string From, string To, string Workspace, string? Note);

/// <summary>
/// A handback whole: its act (<c>done</c>, <c>ready</c> or <c>declined</c>), its note, how it answered each requirement (absent for none),
/// when on the machine that made it, and the session that wrote it.
/// </summary>
public sealed record HandbackRecordResponse(
    string Act, string? Note, IReadOnlyList<QuestAnswerWire>? Answers, DateTimeOffset At, string Machine, string Session);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReadHandback))]
[JsonSerializable(typeof(ReadQuestAttachment))]
[JsonSerializable(typeof(HandbackResponse))]
[JsonSerializable(typeof(QuestFileBytes))]
internal sealed partial class QuestFileJson : JsonSerializerContext;
