namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="AskWordRefusal.None"/> when the word was kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask it was kept on, when it was.</param>
/// <param name="Word">The word as it was kept, when it was.</param>
public sealed record AskWordOutcome(AskWordRefusal Refusal, string Message, string? Ask = null, AskWord? Word = null);
