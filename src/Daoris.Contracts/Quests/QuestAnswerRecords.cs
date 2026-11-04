namespace Daoris.Contracts.Quests;

// The parts of a quest as every door answers it. The request side's records of the same parts (`…Wire`) allow every value to be
// missing, since the exchange refuses what a caller left out naming which; an answer always carries its values, so a reader of one
// is told which may be absent.

/// <summary>A chain's step.</summary>
/// <param name="SetUpIn">The environment a set-up step shows the chain's work in; absent on every other step.</param>
public sealed record QuestStepResponse(string To, string Title, string Body, string? SetUpIn = null);

/// <summary>One evidence item: exactly one of a path or a gate.</summary>
public sealed record QuestEvidenceResponse(string? Path, string? Gate = null);

/// <summary>What the person requires: their words, quoted, and the check that proves them.</summary>
/// <param name="Evidence">What Daoris reads itself; empty for none.</param>
public sealed record QuestRequirementResponse(string Quote, string Check, IReadOnlyList<QuestEvidenceResponse> Evidence);

/// <summary>How a done answered one requirement: <c>Met</c> with how, or <c>Departed</c> with the reason and the words it turns on.</summary>
public sealed record QuestAnswerResponse(int Requirement, string? Met, string? Departed = null, string? Quote = null);

/// <summary>A chain's review choice, <c>off</c>, <c>on</c> or an environment's name, and the person's words it was set on.</summary>
public sealed record QuestReviewChoiceResponse(string Choice, string? Words = null);

/// <summary>A submission by the machine and sequence that made it.</summary>
public sealed record QuestSubmissionRefResponse(string Machine, long Sequence);

/// <summary>Which set-up a verdict answers, by the machine and sequence that name it everywhere.</summary>
public sealed record QuestSetUpRefResponse(string Machine, long Sequence);

/// <summary>One item of what Daoris read of a done's evidence, by the requirement's number.</summary>
public sealed record QuestEvidenceReadResponse(
    int Requirement, string? Path, string? Gate, string Result, string? Object = null, bool? Changed = null, string? Spelled = null);

/// <summary>What Daoris read of a done's evidence: the commit, how it was chosen, the session whose end was read, each item.</summary>
/// <param name="At">When it was read, as is <paramref name="Machine"/>.</param>
/// <param name="Submission">The submission it was read for; a done's verdict names none.</param>
public sealed record QuestEvidenceVerdictResponse(
    string Commit, string How, string? Session, IReadOnlyList<QuestEvidenceReadResponse> Items,
    DateTimeOffset? At, string? Machine, QuestSubmissionRefResponse? Submission);
