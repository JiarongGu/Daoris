namespace Daoris.Knowledge;

/// <summary>
/// A choice about an ask's team and why, kept as the decision files are: its subject, the plan revision it reads, the decision, the reason, its
/// author and the act that followed. A revision of the plan is the lead's decision.
/// </summary>
/// <param name="Ask">The ask.</param>
/// <param name="Scope">The part whose split the decision is of; null for the ask's own plan.</param>
/// <param name="Revision">The plan revision the decision reads.</param>
/// <param name="Subject">What it is about: <see cref="Plan"/>.</param>
/// <param name="Decision">The choice: <c>planned</c> for a first revision, <c>revised</c> for a later one.</param>
/// <param name="Reason">Why, in the writer's words.</param>
/// <param name="Author">The session that decided, or <c>person</c> for a hand.</param>
/// <param name="Act">What followed: the quests the revision published.</param>
/// <param name="At">When.</param>
public sealed record TeamDecision(
    string Ask, string? Scope, int Revision, string Subject, string Decision, string Reason, string Author, string Act, DateTimeOffset At)
{
    /// <summary>The subject of a plan revision.</summary>
    public const string Plan = "plan";
}
