namespace Daoris.Knowledge;

/// <summary>The person's answer to one question of a plan's delivery, kept on the plan's next revision: it settles the question under its key everywhere it is asked.</summary>
/// <param name="Key">The key of the question answered.</param>
/// <param name="Words">What the person said, in their own words; never empty.</param>
/// <param name="At">When they said it.</param>
public sealed record DeliveryAnswer(string Key, string Words, DateTimeOffset At);
