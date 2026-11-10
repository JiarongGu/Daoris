namespace Daoris.Driver;

/// <summary>
/// What a place held at one moment of a turn: for a git place, <c>HEAD</c>'s commit and branch and each path git's status lists, with
/// its working content's SHA-256 (null where it is gone). Every path not listed is <c>HEAD</c>'s.
/// </summary>
/// <param name="Kind">The place's kind; <c>git</c> is the one read today.</param>
/// <param name="Head">The commit <c>HEAD</c> named, or null in a repository with none.</param>
/// <param name="Branch">The branch checked out, or null where detached.</param>
/// <param name="At">The moment of its read, UTC.</param>
/// <param name="Entries">The listed paths, relative to the root with <c>/</c>, sorted.</param>
public sealed record RunManifest(string Kind, string? Head, string? Branch, DateTimeOffset At, IReadOnlyList<ManifestEntry> Entries)
{
    /// <summary>The entry listed for <paramref name="path"/>, or null where the path is <c>HEAD</c>'s.</summary>
    public ManifestEntry? Listed(string path) => Entries.FirstOrDefault(entry => string.Equals(entry.Path, path, StringComparison.Ordinal));
}

/// <summary>One listed path of a manifest.</summary>
/// <param name="Path">Relative to the place's root, with <c>/</c>.</param>
/// <param name="Status">git's two-letter status (<c>.M</c>, <c>A.</c>, <c>??</c> and so on).</param>
/// <param name="Sha256">Its working content's hash, lowercase; null where the file is gone.</param>
/// <param name="Size">Its size in bytes; null where it is gone.</param>
public sealed record ManifestEntry(string Path, string Status, string? Sha256, long? Size);

/// <summary>The end of a turn a manifest reads.</summary>
public enum ManifestMoment
{
    Start,
    End,
}
