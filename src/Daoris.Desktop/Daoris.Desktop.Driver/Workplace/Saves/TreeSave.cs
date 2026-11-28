namespace Daoris.Driver;

/// <summary>One file a save holds, as the tree had it: its path under the tree, the SHA-256 and length of its bytes and the git blob holding
/// them as written (no filter applied); a file the tree deleted carries no hash.</summary>
public sealed record SavedFile(string Path, string? Sha256, long Bytes, string? Blob)
{
    /// <summary>The tree deleted the file its head holds.</summary>
    public bool Deleted => Sha256 is null;
}

/// <summary>
/// A save's record: the kept ref it is under, the head it pinned, the save commit on that ref, the commit of the tree's index
/// and the commit of every listed file's bytes as written, the ignored paths it took as work, each file with its hash, and when and how
/// long. The record is the store's (<see cref="TreeSaveRecords"/>); the commits are the repository's.
/// </summary>
/// <param name="Id">The kept ref, <c>refs/daoris/saves/&lt;branch&gt;/&lt;n&gt;</c>.</param>
/// <param name="Root">The repository's main checkout, where the ref and the objects are.</param>
/// <param name="Branch">The tree's branch; null for a detached head.</param>
/// <param name="Commit">The save commit: the head's tree with every listed file as git reads it, its parents the head, <paramref name="Staged"/> and <paramref name="Raw"/>.</param>
/// <param name="Staged">The commit of the tree's index, the staged work.</param>
/// <param name="Raw">The commit of every listed file's bytes as written, which a restore writes back.</param>
/// <param name="Work">The ignored paths the save took as work.</param>
public sealed record TreeSave(
    string Id, string Workspace, string Repository, string Root, string Tree, string? Branch, string Head, string Commit, string Staged,
    string Raw, IReadOnlyList<string> Work, IReadOnlyList<SavedFile> Files, long Bytes, long TookMs, DateTimeOffset At)
{
    /// <summary>How the landed test by content found its work on the line (<see cref="TreeSaveLanded"/>); null while it is not.</summary>
    public string? Landed { get; init; }
}

/// <summary>Why no save is made; the tree is held as it is.</summary>
public enum SaveRefusal
{
    /// <summary>The folder is no top folder of a git working tree.</summary>
    NoTree,

    /// <summary>git could not say what the tree holds, or could not write a save's objects or ref.</summary>
    Unread,

    /// <summary>The index holds an unresolved file, which no commit can hold.</summary>
    Conflicted,

    /// <summary>The tree holds something a save cannot hold as a file: a link, a nested repository, a submodule.</summary>
    Unsavable,

    /// <summary>A file changed during its save.</summary>
    Moving,

    /// <summary>The ref and its commits were made but the record could not be written.</summary>
    Unrecorded,
}

/// <summary>A save made, or why none is, in a sentence.</summary>
public sealed record SaveOutcome(TreeSave? Save, SaveRefusal? Refused, string Said)
{
    public static SaveOutcome Of(TreeSave save) => new(save, null,
        $"saved the tree at {save.Tree} under `{save.Id}`: head {save.Head[..Math.Min(12, save.Head.Length)]}, {save.Files.Count} file(s), "
        + $"{SpaceCommand.Size(save.Bytes)}, in {save.TookMs} ms.");

    public static SaveOutcome No(SaveRefusal why, string said) => new(null, why, $"the tree stays as it is, unsaved: {said}");
}

/// <summary>What a restore proved: whether every listed file reads identical by hash, and what does not.</summary>
public sealed record RestoreProof(bool Whole, IReadOnlyList<string> Differ, string Said);
