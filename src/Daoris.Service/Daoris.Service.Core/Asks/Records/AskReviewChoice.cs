namespace Daoris.Knowledge;

/// <summary>A review choice the person set on an ask (design §1.5): <c>off</c>, <c>on</c> or an environment's name.</summary>
/// <param name="Choice">What they chose.</param>
/// <param name="At">When they chose it.</param>
/// <param name="Words">Their words with it, where they gave any.</param>
public sealed record AskReviewChoice(string Choice, DateTimeOffset At, string? Words = null);
