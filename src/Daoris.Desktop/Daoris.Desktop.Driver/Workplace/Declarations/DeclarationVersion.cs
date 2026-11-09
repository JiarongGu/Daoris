namespace Daoris.Driver;

/// <summary>Why a declaration did not read. Each is a failure of the read, a function of nothing, so none is kept.</summary>
internal enum DeclarationUnread
{
    /// <summary>A git start failed, or gave no answer git gives.</summary>
    Failed,

    /// <summary>Git holds no such commit or blob, after the one start allowed to fetch.</summary>
    Missing,

    /// <summary>Its size passes the reader's limit.</summary>
    TooLarge,

    /// <summary>The read ended short of its size.</summary>
    Short,

    /// <summary>The file did not open or read.</summary>
    Io,

    /// <summary>The folder it stands in is gone.</summary>
    FolderGone,

    /// <summary>A folder, or another thing that is no file, stands at its path.</summary>
    NotAFile,

    /// <summary>Asked by its blob id, git answered bytes that do not hash to it: a replacement stands.</summary>
    Replaced,
}

/// <summary>An unread declaration's version asked for its text: it has none, so no fingerprint or key is made from a failure.</summary>
internal sealed class DeclarationUnreadException(DeclarationUnread cause)
    : IOException($"the declaration was not read ({cause}), so it has no version")
{
    internal DeclarationUnread Cause { get; } = cause;
}

/// <summary>
/// A declaration's version: its content's key (with its mode where it is no regular file), its verified absence, or <i>unread</i>.
/// </summary>
/// <remarks>
/// Versions are compared by <see cref="Same"/> alone, false where either side is unread, so an unread is the same as no version, another
/// unread included. The type is no record and declares no equality, so nothing compares two unread as equal, and an unread's
/// <see cref="Text"/> throws, so no string a stamp or a key holds is made from one.
/// </remarks>
internal sealed class DeclarationVersion
{
    private readonly ContentKey? _key;
    private readonly string? _mode;
    private readonly DeclarationUnread? _unread;

    private DeclarationVersion(ContentKey? key, string? mode, DeclarationUnread? unread)
    {
        _key = key;
        _mode = mode;
        _unread = unread;
    }

    /// <summary>A verified absence: no file in a folder that stands, or no entry in a hashed tree.</summary>
    internal static readonly DeclarationVersion Absent = new(null, null, null);

    /// <summary>A content's version; <paramref name="mode"/> names an entry that is no regular file, so a link is never its target's bytes.</summary>
    internal static DeclarationVersion Of(ContentKey key, string? mode = null) => new(key, mode, null);

    /// <summary>A read that failed: a new one at each ask, the same as nothing.</summary>
    internal static DeclarationVersion Unread(DeclarationUnread cause) => new(null, null, cause);

    internal bool IsUnread => _unread is not null;

    /// <summary>Whether two versions are one: false where either is missing or unread.</summary>
    internal static bool Same(DeclarationVersion? one, DeclarationVersion? other) =>
        one is { _unread: null } && other is { _unread: null } && one._key == other._key && one._mode == other._mode;

    /// <summary>The version as a stamp or a key holds it; an unread has none and throws <see cref="DeclarationUnreadException"/>.</summary>
    internal string Text => _unread is { } cause
        ? throw new DeclarationUnreadException(cause)
        : _key is { } key ? _mode is null ? key.Hex : $"{_mode}:{key.Hex}" : "absent";
}
