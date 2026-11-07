using Daoris.Contracts.Sessions;

namespace Daoris.Contracts.Review;

// A second opinion as this machine keeps it. Answered only to a caller on this machine: the candidate is this machine's commits until
// they land, and the reviewer names an account.

/// <summary>The candidate: exactly what a pass reads, by full commit ids.</summary>
public sealed record OpinionCandidateWire(string? Repository, string? Base, string? Tip, IReadOnlyList<string?>? Commits);

/// <summary>
/// The reviewer as the driver's walk chose it: its door, its label (<c>another-maker</c>, <c>same-agent</c>,
/// <c>maker-not-declared</c>), its declared product and maker, and the account it runs as.
/// </summary>
public sealed record OpinionReviewerWire(
    string? Door, string? Label, string? Product = null, string? Maker = null, string? Account = null);

/// <summary>
/// An opinion with where its pass stands, derived when read: <c>State</c> is <c>reading</c>, <c>given</c> or <c>failed</c>, and
/// <c>Why</c> then <c>ended</c> or <c>out-of-time</c>.
/// </summary>
public sealed record OpinionResponse(
    string Id, string Occasion, string Pass, string Working, string Session, string? Rechecks,
    OpinionCandidateWire Candidate, OpinionReviewerWire Reviewer, IReadOnlyList<string> Families, string? Posture, int Minutes,
    string Tier, DateTimeOffset Asked, DateTimeOffset Due, string State, string? Why,
    OpinionGivenResponse? Given, OpinionHandedResponse? Handed, IReadOnlyList<OpinionAnswerResponse> Answers);

/// <summary>What the reviewer said, once: its numbered findings, what it read, its limits, and a recheck's word on each first-pass finding.</summary>
public sealed record OpinionGivenResponse(
    IReadOnlyList<OpinionFindingResponse> Findings, string Read, string? Limits, IReadOnlyList<OpinionRecheckResponse>? Rechecked,
    DateTimeOffset At);

public sealed record OpinionFindingResponse(
    int Number, string Weight, string Where, string Claim, string Consequence, string Reproduce, string Sure, string? Proposal);

public sealed record OpinionRecheckResponse(int Finding, string Says);

/// <summary>Where the findings went: the working session, the word they wait in on its record, and when.</summary>
public sealed record OpinionHandedResponse(string Session, string Word, DateTimeOffset At);

/// <summary>
/// The working session's latest answer to one finding: <c>fixed</c> with its commit, <c>rejected</c> with its evidence, or
/// <c>unresolved</c> with why. A finding with none reads as unresolved.
/// </summary>
public sealed record OpinionAnswerResponse(int Finding, string Said, string? Commit, string? Evidence, string? Why, DateTimeOffset At);

/// <summary>
/// A door's answer: the opinion as it now stands and the desk's sentence, with the reviewer's record a pass opened or the working
/// session the findings went to, and the word they wait in. <c>Waiting</c>: the findings were waiting already.
/// </summary>
public sealed record OpinionActionResponse(
    OpinionResponse Opinion, string Message, SessionResponse? Session = null, SaidWordResponse? Said = null, string? Settled = null, bool? Waiting = null);
