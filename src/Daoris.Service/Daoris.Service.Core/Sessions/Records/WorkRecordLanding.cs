namespace Daoris.Knowledge;

/// <summary>
/// The landing that carried a finished tree's work, captured by the driver before the move (Codex's read of the design, finding 6): the
/// branch it made, the line, when it landed, and its pull request where one was opened.
/// </summary>
public sealed record WorkRecordLanding(string Branch, string? Line, DateTimeOffset At, string? PullRequest);
