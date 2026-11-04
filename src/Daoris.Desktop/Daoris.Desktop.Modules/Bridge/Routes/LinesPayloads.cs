namespace Daoris.Desktop;

// The payloads of LinesRoutes' choices that are fields; the rule edits (review, opinion, helpers and the rest) are read by their
// shared tables from the request's own shape.

/// <summary>A repository or a workspace, one of them; no language clears it, and a code the table does not hold is the driver's refusal.</summary>
public sealed record SetLanguage(string? Language, string? Repository, string? Workspace);
