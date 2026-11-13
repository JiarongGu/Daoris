namespace Daoris.Knowledge;

/// <summary>A fault or a fix the plan's judge found. A plan with flags is saved with them; the supervisor reads them, nothing refuses.</summary>
/// <param name="Kind">One of the <see cref="TeamFlags"/> constants.</param>
/// <param name="Part">The part it is about, or null for the plan.</param>
/// <param name="Text">What was found, in a sentence.</param>
public sealed record TeamFlag(string Kind, string? Part, string Text);
