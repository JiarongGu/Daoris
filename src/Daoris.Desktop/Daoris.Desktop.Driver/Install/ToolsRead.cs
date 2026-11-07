namespace Daoris.Driver;

/// <summary><c>tools.json</c> as it stands.</summary>
/// <param name="Entries">Every declared tool, in the declared order.</param>
/// <param name="Unknown">The ids the file names that this build does not declare: kept, never applied.</param>
/// <param name="Git">The settings Daoris's git carries.</param>
/// <param name="Locations">The resource locations, in order.</param>
/// <param name="Notes">What the file keeps without applying, or skips, and why.</param>
/// <param name="Problem">Why the whole file does not read; every tool is then refused.</param>
public sealed record ToolsRead(
    string Path, bool Exists, IReadOnlyDictionary<string, ToolEntry> Entries, IReadOnlyList<string> Unknown,
    IReadOnlyDictionary<string, string> Git, IReadOnlyList<string> Locations, IReadOnlyList<string> Notes, string? Problem);

/// <summary>Which file a tool is, or why none.</summary>
/// <param name="File">The file that starts; null for a refusal, and for a system tool <c>PATH</c> does not find.</param>
/// <param name="Refused">
/// True when the way set cannot run: it never falls back to <c>PATH</c>. False with a problem is a system tool
/// <c>PATH</c> does not find, which is said, and whose callers keep today's behaviour.
/// </param>
public sealed record ToolResolution(string Tool, ToolWay? Way, string? Version, string? File, bool Refused, string? Problem);
