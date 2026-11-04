namespace Daoris.Knowledge;

/// <summary>A session the record names (§2.2's <c>work_record_sessions</c>), and the commits its base attributes to it, or null where none can be read.</summary>
public sealed record WorkRecordSession(string Session, IReadOnlyList<string>? Commits);
