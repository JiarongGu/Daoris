using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Quests;

// The quest doors. What a repository owes is held by the service rather than written into anyone's files: a quest is
// published and pulled, never pushed into a sibling's tree. The doors a person or the driver alone may use are a local host's;
// a shared host serves the reads, the publish and the respond, so a remote deployment is a transfer of work.

/// <summary>The quests the host holds; absent <c>includeClosed</c> is the open ones.</summary>
public sealed record ListQuests(string? Repository = null, bool? IncludeClosed = null, string? Workspace = null) : IRoute<QuestResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/quests";
}

/// <summary>One quest's moves, on both hosts; an id with no history has none, so a client tells this door from an older host's 404.</summary>
public sealed record ReadQuestMoves(string Id) : IRoute<QuestMovesResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/quests/{id}/moves";
}

/// <summary>A publish, judged by the same exchange the connector's tool uses, so the two doors cannot drift.</summary>
/// <param name="Attachments">A file's content to a local host, its name and hash to a shared one, which refuses content.</param>
/// <param name="Request">A workflow request's key: the same key sent again answers the quest it made.</param>
public sealed record PublishQuest(
    string From, string To, string Title, string Body,
    IReadOnlyList<string>? Links = null, IReadOnlyList<QuestAttachmentRequest>? Attachments = null,
    IReadOnlyList<QuestStepWire?>? Then = null, IReadOnlyList<QuestRequirementWire?>? Requirements = null,
    string? Short = null, QuestReviewWire? Review = null, string? Weight = null, IReadOnlyList<string>? Paths = null,
    string? Request = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests";
}

/// <summary>A take, a done, a wait or a decline. A refused move is a state conflict, 409, which the losing side of a race reads as
/// someone got there first.</summary>
/// <param name="On">The question a wait waits on.</param>
/// <param name="Answers">How a done answers each requirement.</param>
/// <param name="WhileOpen">A decline that applies only while the quest is open: an abandon's, which is the person's form of the door.</param>
public sealed record RespondQuest(
    string Id, string? Action, string? Reason, string? On = null, IReadOnlyList<QuestAnswerWire?>? Answers = null,
    bool? WhileOpen = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/respond";
}

/// <summary>The person's yes to a done's departure from what they required: what it held goes on.</summary>
public sealed record AcceptQuest(string Id) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/accept";
}

/// <summary>The person's own done, which answers none of their requirements and says it was theirs, with their words.</summary>
[OptionalBody]
public sealed record MarkQuestDone(string Id, string? Note = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/done";
}

/// <summary>The person's reopen of a sealed submission, named whole as the quest answers it.</summary>
public sealed record ReopenQuest(string Id, QuestSubmissionRefWire? Submission, string? Note = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/reopen";
}

/// <summary>The person's word to go on with a step its source's reopen held.</summary>
public sealed record ContinueQuest(string Id) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/continue";
}

/// <summary>The person's bring-forward of their machine's lost take, by the conflict's machine and sequence.</summary>
public sealed record ForwardConflict(string Id, string? Machine, long? Sequence) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/conflicts/forward";
}

/// <summary>
/// What the driver read of a done's evidence, the verdict's own shape. The session a requirement judges never writes its verdict,
/// so no connector tool reaches this door.
/// </summary>
public sealed record PostQuestEvidence(
    string Id, string? Commit, string? How, string? Session = null, IReadOnlyList<QuestEvidenceReadWire?>? Items = null,
    DateTimeOffset? At = null, string? Machine = null, QuestSubmissionRefWire? Submission = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/evidence";
}

/// <summary>The driver's bind of a taken quest to the graph that closes it, at the take.</summary>
/// <param name="AtTake">An open quest binds at its take; a host before it refuses the open quest's bind as not taken.</param>
/// <param name="Session">The session the graph is named for, whose take alone binds it.</param>
public sealed record BindQuest(string Id, string? Graph, bool? AtTake = null, string? Session = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/bind";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ListQuests))]
[JsonSerializable(typeof(QuestResponse))]
[JsonSerializable(typeof(QuestResponse[]))]
[JsonSerializable(typeof(ReadQuestMoves))]
[JsonSerializable(typeof(QuestMovesResponse))]
[JsonSerializable(typeof(PublishQuest))]
[JsonSerializable(typeof(RespondQuest))]
[JsonSerializable(typeof(AcceptQuest))]
[JsonSerializable(typeof(MarkQuestDone))]
[JsonSerializable(typeof(ReopenQuest))]
[JsonSerializable(typeof(ContinueQuest))]
[JsonSerializable(typeof(ForwardConflict))]
[JsonSerializable(typeof(PostQuestEvidence))]
[JsonSerializable(typeof(BindQuest))]
[JsonSerializable(typeof(QuestActionResponse))]
[JsonSerializable(typeof(QuestEvidenceVerdictResponse))]
internal sealed partial class QuestJson : JsonSerializerContext;
