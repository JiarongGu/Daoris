namespace Daoris.Driver;

/// <summary>One file in a session's landed work, and the patch that changed it.</summary>
/// <param name="Path">Repository-relative, as git spells it.</param>
/// <param name="Status">`added`, `modified`, `deleted`, `renamed` — git's letter, said in words.</param>
/// <param name="Added">Lines added, or null where git counted none because the file is binary.</param>
/// <param name="Removed">Lines removed, same.</param>
/// <param name="Patch">The unified diff for this file, or null when it was dropped by the bound.</param>
public sealed record DiffFile(
    string Path, string Status, int? Added, int? Removed, string? Patch);

