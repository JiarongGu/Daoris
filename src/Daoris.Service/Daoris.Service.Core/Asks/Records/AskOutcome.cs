namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="AskRefusal.None"/> when it did what was asked.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask as it now stands — present even when its quest was refused.</param>
/// <param name="Quest">The quest it became on this call, when it became one.</param>
public sealed record AskOutcome(AskRefusal Refusal, string Message, Ask? Ask, Quest? Quest = null);
