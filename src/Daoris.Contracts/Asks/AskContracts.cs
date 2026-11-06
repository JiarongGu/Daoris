using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Contracts.Quests;

namespace Daoris.Contracts.Asks;

// The ask doors: a sentence entered at a workspace, answered by the tier this machine has. Local hosts only: the intake is this
// machine's, like a quest file's bytes, and a shared deployment has no door here.

/// <summary>The asks this machine holds; absent <c>includeClosed</c> is the open ones.</summary>
public sealed record ListAsks(string? Workspace = null, bool? IncludeClosed = null) : IRoute<AskResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/asks";
}

/// <summary>
/// An ask: a sentence at a workspace. Files arrive whole, since the door is a local host's; <c>To</c> is the asker naming the receiver,
/// which publishes at once.
/// </summary>
/// <param name="Review">The person's review choice from the composer, and <paramref name="ReviewWords"/> theirs with it.</param>
/// <param name="Kind">The person's kind for its work, with <paramref name="Workflow"/> and <paramref name="WorkflowWords"/>.</param>
/// <param name="Short">The asker's short title, at most 40 characters, judged as a quest's is.</param>
/// <param name="Paths">The paths the asker declares the work touches, judged as a quest's are; each quest it publishes carries them.</param>
public sealed record PostAsk(
    string? Workspace, string? Sentence, IReadOnlyList<string>? Links,
    IReadOnlyList<QuestAttachmentRequest>? Attachments, string? To, string? Review = null, string? ReviewWords = null,
    string? Kind = null, string? Workflow = null, string? WorkflowWords = null, string? Short = null,
    IReadOnlyList<string>? Paths = null) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks";
}

/// <summary>The person's review choice on an ask's page, or its intake's proposal applied: the latest stands.</summary>
public sealed record ChooseAskReview(string Id, string? Choice, string? Words = null) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/review";
}

/// <summary>
/// The person's kind and workflow on an ask, or naming neither to clear them: the latest stands. A person's door: an agent proposes a
/// kind, the intake's, and never sets one.
/// </summary>
public sealed record ChooseAskWorkflow(string Id, string? Kind = null, string? Workflow = null, string? Words = null) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/workflow";
}

/// <summary>One ask, whole: how the driver observes what its intake made of it.</summary>
public sealed record ReadAsk(string Id) : IRoute<AskResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/asks/{id}";
}

/// <summary>
/// An ask's channel: what the ask's sessions and the person said, and Daoris's own coded lines read from the ask's quests, in time
/// order. A read moves no mark: <c>entry</c> (the person's by default) says whose unread count it carries. An ask held elsewhere reads
/// from the messages the sync mirrored, read-only; no machine path is in it.
/// </summary>
/// <param name="Since">A moment, ISO 8601: the tail of a long channel after it.</param>
public sealed record ReadAskChannel(string Id, string? Entry = null, string? Since = null) : IRoute<ChannelResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/asks/{id}/channel";
}

/// <summary>The asks the mirrored messages name that this machine does not hold, for a list that opens each one's channel.</summary>
public sealed record ListElsewhereAsks : IRoute<ElsewhereAsksResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/elsewhere";
}

/// <summary>
/// The person's message on the channel, kept on the ask as a <c>channel</c> word. A refusal is a code.
/// </summary>
/// <param name="To">One of the ask's repositories or <c>everyone</c>.</param>
/// <param name="Re">The id of a message it answers.</param>
/// <param name="Urgent">False keeps the person's message ordinary; absent, it is their default, urgent.</param>
public sealed record PostAskChannelMessage(string Id, string? Text, string? To = null, string? Re = null, bool? Urgent = null)
    : IRoute<ChannelPostResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/channel";
}

/// <summary>
/// An entry's acknowledgment of messages by their ids: <c>taking</c> claims each for <c>session</c>'s part, one part's only;
/// <c>released</c> lets a claim go; <c>sent</c>, <c>taken</c>, <c>read</c> and <c>partly</c> raise what the entry holds, never lowering
/// it. Answers the ids it changed. An unknown state is the caller's mistake; an unknown ask is not found.
/// </summary>
public sealed record AcknowledgeAskChannel(string Id, string? Entry, string? Session, string? State, IReadOnlyList<string>? Messages)
    : IRoute<ChannelAckResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/channel/ack";
}

/// <summary>
/// An entry's read mark, moved: the person's, or a part's that a driver handed the channel to, to <c>through</c> or the newest item.
/// Answers the channel as it then reads. The person's is kept on a teammate's ask the sync mirrored too, on this machine alone; a
/// session's entry there, and an ask nowhere, are not found.
/// </summary>
public sealed record MarkAskChannelRead(string Id, string? Entry = null, DateTimeOffset? Through = null) : IRoute<ChannelResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/channel/read";
}

/// <summary>
/// A publish is a person's <c>To</c> alone; an intake adds its own words, carry and chain, and names its <c>Session</c>, which moves
/// the ask's tier to <c>intake</c> only when it is the ask's own. Its <c>Requirements</c> quote the person, judged against the ask's
/// words. <c>Review</c> is the chain's choice, an intake's only on the person's quoted words; <c>ReviewProposal</c> an intake's
/// proposal with its reason, kept on the ask.
/// </summary>
/// <param name="Paths">The paths an intake declares, added to the ask's own and never in place of them.</param>
public sealed record PublishAsk(
    string Id, string? To, string? Title = null, string? Body = null, IReadOnlyList<string>? Links = null,
    IReadOnlyList<QuestAttachmentRequest>? Attachments = null, IReadOnlyList<QuestStepWire?>? Then = null,
    string? Session = null, IReadOnlyList<QuestRequirementWire?>? Requirements = null, string? Short = null,
    QuestReviewWire? Review = null, ReviewProposalWire? ReviewProposal = null, WorkflowProposalWire? WorkflowProposal = null,
    IReadOnlyList<string>? Paths = null) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/publish";
}

/// <summary>An intake's review proposal: a choice and its reason, at most 300 characters.</summary>
public sealed record ReviewProposalWire(string? Choice, string? Reason);

/// <summary>An intake's kind proposal: a kind and its reason, the person's own words it quotes, at most 300 characters.</summary>
public sealed record WorkflowProposalWire(string? Kind, string? Reason);

/// <summary>A person closing their own ask, with the reason.</summary>
public sealed record CloseAsk(string Id, string? Reason) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/close";
}

/// <summary>
/// The person's yes or no to a go-ahead a session asked on their ask. The answer that leaves none of a parked session's go-aheads
/// waiting is that park's answer, unless the caller answers the park itself.
/// </summary>
/// <param name="Number">The go-ahead's number on its ask, from 1.</param>
/// <param name="Answer"><c>approved</c> or <c>refused</c>.</param>
/// <param name="Words">Theirs, where they give any.</param>
/// <param name="GoesOn">False leaves a parked session that waited on it to its caller; absent or true, the answer that leaves none waiting goes on.</param>
public sealed record AnswerGoAhead(string Id, string Number, string? Answer, string? Words = null, bool? GoesOn = null) : IRoute<AskActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/asks/{id}/go-aheads/{number}";
}

/// <summary>Deleting an ask made by mistake, with every quest asked by it, or none of it; its channel goes with it.</summary>
public sealed record DeleteAsk(string Id) : IRoute<DeletedResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/asks/{id}";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ListAsks))]
[JsonSerializable(typeof(AskResponse[]))]
[JsonSerializable(typeof(PostAsk))]
[JsonSerializable(typeof(ChooseAskReview))]
[JsonSerializable(typeof(ChooseAskWorkflow))]
[JsonSerializable(typeof(ReadAsk))]
[JsonSerializable(typeof(AskResponse))]
[JsonSerializable(typeof(ReadAskChannel))]
[JsonSerializable(typeof(ChannelResponse))]
[JsonSerializable(typeof(ListElsewhereAsks))]
[JsonSerializable(typeof(ElsewhereAsksResponse))]
[JsonSerializable(typeof(PostAskChannelMessage))]
[JsonSerializable(typeof(ChannelPostResponse))]
[JsonSerializable(typeof(AcknowledgeAskChannel))]
[JsonSerializable(typeof(ChannelAckResponse))]
[JsonSerializable(typeof(MarkAskChannelRead))]
[JsonSerializable(typeof(PostLineChannelMessage))]
[JsonSerializable(typeof(ReadAddressed))]
[JsonSerializable(typeof(AddressedResponse))]
[JsonSerializable(typeof(AcknowledgeAddressed))]
[JsonSerializable(typeof(ReadLineChannel))]
[JsonSerializable(typeof(LineChannelResponse))]
[JsonSerializable(typeof(PublishAsk))]
[JsonSerializable(typeof(CloseAsk))]
[JsonSerializable(typeof(AnswerGoAhead))]
[JsonSerializable(typeof(AskActionResponse))]
[JsonSerializable(typeof(DeleteAsk))]
internal sealed partial class AskJson : JsonSerializerContext;
