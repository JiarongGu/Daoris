namespace Daoris.Knowledge;

/// <summary>
/// What a record's door reported spent over all the turns it ran so far, as a turn's end hands it. The turn's own figure is this less
/// what the turns before it kept; a count its door never said stays null.
/// </summary>
/// <param name="Input">Input tokens, or null where its door reports none.</param>
/// <param name="Output">Output tokens, or null where its door reports none.</param>
public sealed record TurnSpent(long? Input, long? Output);
