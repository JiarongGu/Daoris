using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Review;

// The second opinions a local host serves the driver: a pass the driver asks, which opens its reviewer's record beside it, is read back,
// and has its findings handed to the working session. A shared host maps none, and a caller off this machine is answered not found.
// What the reviewer says and what the working session answers come through each one's own connector; no door here answers a dispute.

/// <summary>
/// A pass the driver asks. Every part may be left out, and the desk refuses the blank naming which; <c>Minutes</c> is the rule's bound,
/// said, never defaulted here (the service holds no rule).
/// </summary>
public sealed record AskOpinion(
    string? Occasion, string? Pass, string? Working, OpinionCandidateWire? Candidate, OpinionReviewerWire? Reviewer,
    string? Posture, int? Minutes, string? Tree, string? Rechecks = null, IReadOnlyList<string?>? Families = null,
    string? HarnessVersion = null) : IRoute<OpinionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/opinions";
}

/// <summary>The opinions on one session's work, the one a reviewer's record reads for, or those of a repository, oldest first.</summary>
public sealed record ListOpinions(string? Working = null, string? Session = null, string? Repository = null) : IRoute<OpinionResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/opinions";
}

public sealed record ReadOpinion(string Id) : IRoute<OpinionResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/opinions/{id}";
}

/// <summary>
/// A first pass's findings, handed once at a turn's end to its working session as another agent's words. A chat run's go to the record
/// of its line the driver names, between turns.
/// </summary>
/// <param name="To">The record of the line to hand them to; none is the working session itself.</param>
/// <param name="Settled">True asks to be told a key the line settled, as a say or a helper's hand does.</param>
[OptionalBody]
public sealed record HandOpinion(string Id, string? To = null, bool? Settled = null) : IRoute<OpinionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/opinions/{id}/hand";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AskOpinion))]
[JsonSerializable(typeof(ListOpinions))]
[JsonSerializable(typeof(ReadOpinion))]
[JsonSerializable(typeof(HandOpinion))]
[JsonSerializable(typeof(OpinionResponse[]))]
[JsonSerializable(typeof(OpinionResponse))]
[JsonSerializable(typeof(OpinionActionResponse))]
internal sealed partial class OpinionJson : JsonSerializerContext;
