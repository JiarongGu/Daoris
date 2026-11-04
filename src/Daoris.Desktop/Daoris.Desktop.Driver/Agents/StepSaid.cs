namespace Daoris.Driver;

/// <summary>
/// One step of the walk as a start's record says it (UX7d-1, D152's UX7d-1 note): the clause in English, as
/// <see cref="RotationWords.Clause"/> joins it, and the same step by code, a reason of <see cref="NoteCodes.Opening"/> with its
/// own values, so the page words the line in the reader's language. Machine-local: its values may name an account.
/// </summary>
/// <param name="Text">The clause in English.</param>
/// <param name="Why">Its reason, one of <see cref="OpeningWhy"/>; null where no reason says it, and the line is then kept with no parts.</param>
/// <param name="Values">The reason's own values: an account, a moment, a percent, a window, a workspace.</param>
public sealed record StepSaid(string Text, string? Why, IReadOnlyList<(string Name, object? Value)> Values);
