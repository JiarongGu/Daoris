namespace Daoris.Knowledge;

/// <summary>
/// What a session proposal's <c>helper</c> and <c>pass</c> doors carry beside the session: the question, the agent it is
/// for, the ask it asks again after or the answer to pass, and that the answer is the person's. The driver judges each against the machine.
/// </summary>
public sealed record SessionHelperProposal(string? Question, string? Agent, string? Ask, bool ToMe);
