namespace Daoris.Driver;

/// <summary>The review facts and words the room reads from the review subdomain.</summary>
internal interface IKnowledgeReviews
{
    ResolvedReview? Review(DriverConfig config, string repository, string? workspace);
    ResolvedOpinion? Opinion(DriverConfig config, string repository, string? workspace);
    string ReviewCell(ResolvedReview? review);
    string OpinionCell(ResolvedOpinion? opinion);
    string GateWords { get; }
}

internal static class KnowledgeReviews
{
    private static IKnowledgeReviews? _current;

    internal static IKnowledgeReviews Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of review facts is installed");
        set => _current = value;
    }
}
