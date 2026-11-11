namespace Daoris.Desktop;

/// <summary>A run's review by its <c>Run</c>, or by a <c>Session</c> of the run's chain; at its <c>Attempt</c>, or its newest record where 0.</summary>
public sealed record RunReviewRead(string? Run, string? Session, int Attempt);

/// <summary>One file of a run's review record at <c>Attempt</c>, on its <c>Side</c>: <c>before</c> or <c>after</c>.</summary>
public sealed record RunReviewFileRead(string Run, int Attempt, string Path, string Side);
