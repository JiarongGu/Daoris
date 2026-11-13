namespace Daoris.Knowledge;

/// <summary>The person's answer to one question of a plan's delivery: it settles the question under its key wherever it is asked.</summary>
/// <param name="Key">The question's key.</param>
/// <param name="Words">The person's words; never empty.</param>
/// <param name="At">When.</param>
public sealed record DeliveryAnswer(string Key, string Words, DateTimeOffset At);
