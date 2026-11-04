using Daoris.Contracts.Review;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>An opinion as this machine's doors answer it, with where its pass stands, derived when read.</summary>
public static class OpinionAnswers
{
    public static OpinionResponse ToOpinion(OpinionStanding standing)
    {
        var opinion = standing.Opinion;
        return new(
            opinion.Id, opinion.Occasion, opinion.Pass, opinion.Working, opinion.Session, opinion.Rechecks,
            new OpinionCandidateWire(opinion.Candidate.Repository, opinion.Candidate.Base, opinion.Candidate.Tip, [.. opinion.Candidate.Commits]),
            new OpinionReviewerWire(opinion.Reviewer.Adapter, opinion.Reviewer.Label, opinion.Reviewer.Product, opinion.Reviewer.Maker, opinion.Reviewer.Account),
            opinion.Families, opinion.Posture, opinion.Minutes, opinion.Tier, opinion.Asked, opinion.Due, standing.State, standing.Why,
            opinion.Given is { } given
                ? new OpinionGivenResponse(
                    [.. given.Findings.Select(finding => new OpinionFindingResponse(
                        finding.Number, finding.Weight, finding.Where, finding.Claim, finding.Consequence, finding.Reproduce, finding.Sure,
                        finding.Proposal))],
                    given.Read, given.Limits,
                    given.Rechecked.Count == 0 ? null : [.. given.Rechecked.Select(recheck => new OpinionRecheckResponse(recheck.Finding, recheck.Says))],
                    given.At)
                : null,
            opinion.Handed is { } handed ? new OpinionHandedResponse(handed.Session, handed.Word, handed.At) : null,
            [.. opinion.Answers.Select(answer => new OpinionAnswerResponse(answer.Finding, answer.Said, answer.Commit, answer.Evidence, answer.Why, answer.At))]);
    }
}
