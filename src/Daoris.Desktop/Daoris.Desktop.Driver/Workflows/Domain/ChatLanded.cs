namespace Daoris.Driver;

/// <summary>A landing kept for the tip a chat's tree stands at: the record it names, how, when, and what it kept.</summary>
internal sealed record ChatLanded(string Session, string Detail, DateTimeOffset At, LandedBranch? Entry, LandingOpinion? Opinion, string? AcceptedBy);
