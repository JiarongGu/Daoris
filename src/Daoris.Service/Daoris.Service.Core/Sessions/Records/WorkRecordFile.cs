namespace Daoris.Knowledge;

/// <summary>A file a finished tree's commit changed, by name, with the lines added and removed; -1 for each where git counts none (a binary).</summary>
public sealed record WorkRecordFile(string Path, int Added, int Removed);
