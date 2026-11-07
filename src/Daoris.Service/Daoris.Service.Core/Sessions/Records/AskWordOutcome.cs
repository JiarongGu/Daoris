namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="AskWordRefusal.None"/> when the word is kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask it is kept on, when it is.</param>
/// <param name="Word">The word as it is kept, when it is.</param>
public sealed record AskWordOutcome(AskWordRefusal Refusal, string Message, string? Ask = null, AskWord? Word = null);
