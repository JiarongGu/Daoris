namespace Daoris.Driver;

/// <summary>
/// What git says at a commit of what the line and the person's branches hold of a branch's work, as a landing door reads it.
/// </summary>
/// <param name="Carried">The head's sentence where the line or a branch of the person's holds the work by content; else null.</param>
/// <param name="Unlanded">The unlanded-commit proof: how many of its commits no branch of the person's holds; null where git could not say.</param>
/// <param name="Ahead">How many of its commits the line lacks; null where git could not say.</param>
public sealed record HoldContent(string? Carried, int? Unlanded, int? Ahead);
