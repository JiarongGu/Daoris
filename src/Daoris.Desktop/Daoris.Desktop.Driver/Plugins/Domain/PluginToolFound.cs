namespace Daoris.Driver;

/// <summary>What one readiness check answered: whether it passed, and its sentence.</summary>
public sealed record ReadyAnswer(ReadyCheck Check, bool Ready, string Sentence);

/// <summary>What a declared tool's check found (PLUGTOOL1a).</summary>
/// <param name="Ok">Found, and its version in its range or not one the range needs; each check is its own answer.</param>
/// <param name="File">The file found, or null where none was.</param>
/// <param name="Way">How it is run: Daoris's way for a known tool, the system's for one found on the PATH.</param>
/// <param name="Version">The version it said, its first dotted number; null where it was not asked or said none.</param>
/// <param name="Sentence">The tool's own sentence: where it was found, its version against its range, or its problem.</param>
/// <param name="Ready">Each check's answer, in the manifest's order; none where the tool was not found.</param>
public sealed record PluginToolFound(
    PluginTool Tool, bool Ok, string? File, ToolWay? Way, string? Version, string Sentence, IReadOnlyList<ReadyAnswer> Ready);
