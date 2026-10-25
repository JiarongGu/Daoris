namespace Daoris.Driver;

/// <summary>
/// A standing answer (KNOWUSE1b, D135 §3): what the person has told this machine holds for every session in a repository, in
/// their words, as <c>driver.json</c>'s <c>standing</c> keeps it. Machine-local, like everything in that file, and never written
/// into the repository (D32).
/// </summary>
/// <param name="Says">The person's words, trimmed at the ends only.</param>
/// <param name="At">When they set them, or null where the file does not say in a form ISO 8601 writes.</param>
public sealed record StandingAnswer(string Says, DateTimeOffset? At);
