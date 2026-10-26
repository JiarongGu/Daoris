using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>Review's answers to the room, also used by callers outside Loop.</summary>
internal sealed class KnowledgeReviewReads : IKnowledgeReviews
{
    [ModuleInitializer]
    internal static void Install() => KnowledgeReviews.Current = new KnowledgeReviewReads();

    public ResolvedReview? Review(DriverConfig config, string repository, string? workspace) =>
        ReviewRules.Resolve(config, repository, workspace);

    public ResolvedOpinion? Opinion(DriverConfig config, string repository, string? workspace) =>
        OpinionRules.Resolve(config, repository, workspace);

    public string ReviewCell(ResolvedReview? review) => ReviewRules.RoomCell(review);

    public string OpinionCell(ResolvedOpinion? opinion) => OpinionRules.RoomCell(opinion);

    public string GateWords => $"{OpinionRules.Waiting} {OpinionRules.StepWaiting} {OpinionRules.SafeNotHanded}";
}
