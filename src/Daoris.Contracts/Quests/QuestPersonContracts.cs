using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Quests;

// The quest doors a local host alone serves: the graph's close under the driver's record lock, the review's set-ups and verdicts,
// the person's workflow choice, a conflict's dismissal and a quest's delete. No connector tool reaches them.

/// <summary>One quest of a close, with the submission the driver judged; a half-named item arrives blank and the exchange refuses it.</summary>
public sealed record QuestCloseItemWire(string? Id, string? Machine, long? Sequence);

/// <summary>What a close did: every quest it named as it now stands, and the sentence.</summary>
public sealed record QuestCloseResponse(IReadOnlyList<QuestResponse> Quests, string Message);

/// <summary>What a delete did: the record it removed, and the service's sentence, verbatim.</summary>
public sealed record DeletedResponse(string Id, string Message);

/// <summary>
/// The graph's close, under the unit's record lock, which the driver holds. A refusal names its check by a code, which the driver
/// keeps beside the sentence.
/// </summary>
/// <param name="Key">The close's key: a second send of the same close repeats it and is answered with its first outcome.</param>
/// <param name="Token">The close token.</param>
/// <param name="Quests">Every quest of the chain it closes.</param>
public sealed record CloseQuest(string Id, string? Key, string? Token, IReadOnlyList<QuestCloseItemWire?>? Quests) : IRoute<QuestCloseResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/close";
}

/// <summary>What became of a close sent with a key, answered from the store's record of it in the close's own shape; a key never taken is withdrawn.</summary>
public sealed record CloseQuestOutcome(string Id, string? Key, string? Token, IReadOnlyList<QuestCloseItemWire?>? Quests) : IRoute<QuestCloseResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/close/outcome";
}

/// <summary>
/// A set-up posted on a set-up step: the commit Daoris read from the step's tree and the environment's kind, with the session
/// whose said set-ups the driver posts, or the person's own look, shows and again. Both forms want the person key where the host
/// holds one.
/// </summary>
/// <param name="Kind">The environment's kind, <c>local</c> or <c>deployed</c>.</param>
public sealed record PostQuestSetUp(
    string Id, string? Commit, string? Kind, string? Session = null, string? Look = null, string? Shows = null, string? Again = null)
    : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/set-up";
}

/// <summary>
/// The person's verdict on a set-up step, or their skip of a review: <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, with their
/// words (a not-yet's always) and the set-up it answers, named whole as the quest answers it: a reviewed's and a not-yet's always,
/// a skip's never.
/// </summary>
public sealed record ReviewQuest(string Id, string? Verdict, string? Words = null, QuestSetUpRefWire? SetUp = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/review";
}

/// <summary>The person's <i>Set it up in environment</i>: a set-up step published following a done quest, in Daoris's words.</summary>
public sealed record PublishSetUpStep(string Id, string? Environment) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/set-up-step";
}

/// <summary>
/// The person's kind and workflow on a quest published with no ask, before it starts; naming neither clears them. A shape is a bad
/// request, and a quest an ask published, or one already started or closed, the lock's own 409.
/// </summary>
public sealed record ChooseQuestWorkflow(string Id, string? Kind = null, string? Workflow = null, string? Words = null) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/workflow";
}

/// <summary>A person dismissing a conflict: the one named, or, naming none, every one the quest carries. It moves no status.</summary>
public sealed record DismissConflict(string Id, string? Machine, long? Sequence) : IRoute<QuestActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/quests/{id}/conflicts/dismiss";
}

/// <summary>Deleting a quest made by mistake: the exchange's judgement and sentence, verbatim. Something standing on it is a state conflict.</summary>
public sealed record DeleteQuest(string Id) : IRoute<DeletedResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/quests/{id}";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CloseQuest))]
[JsonSerializable(typeof(CloseQuestOutcome))]
[JsonSerializable(typeof(QuestCloseResponse))]
[JsonSerializable(typeof(PostQuestSetUp))]
[JsonSerializable(typeof(ReviewQuest))]
[JsonSerializable(typeof(PublishSetUpStep))]
[JsonSerializable(typeof(ChooseQuestWorkflow))]
[JsonSerializable(typeof(DismissConflict))]
[JsonSerializable(typeof(DeleteQuest))]
[JsonSerializable(typeof(DeletedResponse))]
internal sealed partial class QuestPersonJson : JsonSerializerContext;
