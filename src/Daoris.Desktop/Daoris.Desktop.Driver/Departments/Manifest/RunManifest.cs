namespace Daoris.Driver;

/// <summary>
/// What a place held at one moment of a turn: for a git place, <c>HEAD</c>'s commit and branch and each path git's status lists, with
/// its working content's SHA-256 (null where it is gone), every path not listed being <c>HEAD</c>'s; for a folder, every file with its
/// size, time and hash, a path not listed being absent.
/// </summary>
/// <param name="Kind">The place's kind (<see cref="PlaceKind"/>).</param>
/// <param name="Head">The commit <c>HEAD</c> named, or null in a repository with none.</param>
/// <param name="Branch">The branch checked out, or null where detached.</param>
/// <param name="At">The moment of its read, UTC.</param>
/// <param name="Entries">The listed paths, relative to the root with <c>/</c>, sorted.</param>
public sealed record RunManifest(string Kind, string? Head, string? Branch, DateTimeOffset At, IReadOnlyList<ManifestEntry> Entries)
{
    /// <summary>The place's root the turn worked in, so a turn's end is read where its start read, a sweep's after a restart included.</summary>
    public string? Root { get; init; }

    /// <summary>
    /// The line's own uncommitted paths: at a start, those it carried in from its last end; at an end, every one it keeps the place
    /// with. Every other listed path at a start is the person's or another line's.
    /// </summary>
    public IReadOnlyList<string>? Own { get; init; }

    /// <summary>A folder past the manifest's bounds: it lists nothing, and what changed there is not followed.</summary>
    public bool TooLarge { get; init; }

    /// <summary>The entry listed for <paramref name="path"/>, or null where the path is <c>HEAD</c>'s.</summary>
    public ManifestEntry? Listed(string path) => Entries.FirstOrDefault(entry => string.Equals(entry.Path, path, StringComparison.Ordinal));
}

/// <summary>One listed path of a manifest.</summary>
/// <param name="Path">Relative to the place's root, with <c>/</c>.</param>
/// <param name="Status">git's two-letter status (<c>.M</c>, <c>A.</c>, <c>??</c> and so on); empty in a folder.</param>
/// <param name="Sha256">Its working content's hash, lowercase; null where the file is gone, or in a folder could not be read.</param>
/// <param name="Size">Its size in bytes; null where it is gone.</param>
public sealed record ManifestEntry(string Path, string Status, string? Sha256, long? Size)
{
    /// <summary>Its last write time, UTC, in a folder, whose next manifest hashes it again only where this or its size moved.</summary>
    public DateTimeOffset? Time { get; init; }
}

/// <summary>The end of a turn a manifest reads.</summary>
public enum ManifestMoment
{
    Start,
    End,
}
