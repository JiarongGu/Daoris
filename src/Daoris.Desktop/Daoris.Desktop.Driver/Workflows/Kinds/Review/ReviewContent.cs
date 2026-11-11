using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// One file of a folder's or a copy's review record, drawn at the record's revision: the content its row hashed, from the place's content
/// store, or from the place itself only while the file there still hashes as the row says.
/// </summary>
public static class ReviewContent
{
    public const string Before = "before";

    public const string After = "after";

    /// <summary>The file has moved since this revision, and its content then is not kept.</summary>
    public const string Moved = "moved";

    /// <summary>The record lists no such file, or its side has no content (an added file's before, a removed one's after).</summary>
    public const string NotRow = "not-row";

    /// <summary>The record's kind keeps no file content here: a branch's and a checkout's are read from their commits.</summary>
    public const string NotKept = "not-kept";

    /// <summary>The text of <paramref name="path"/> on <paramref name="side"/> of <paramref name="record"/>, or why it is not drawn.</summary>
    public static (string? Text, string? Refused) Read(string home, ReviewRecord record, string path, string side)
    {
        if (record.Kind is not (ReviewKinds.Folder or ReviewKinds.Copy) || record.Reference is not { Workspace: { } workspace, Department: { } department } reference)
        {
            return (null, NotKept);
        }

        var row = record.Rows.FirstOrDefault(each => string.Equals(each.Path, path, StringComparison.Ordinal));
        if ((side == Before ? row?.Before : row?.After) is not { } hash) return (null, NotRow);
        var store = new ManifestStore(home, workspace, department);
        if (Hashed(store.Content.PathOf(hash), hash) is { } kept) return (kept, null);
        var root = side == After && reference.End is { } end ? store.Read(end, ManifestMoment.End)?.Root : null;
        return root is not null && Hashed(Path.Combine(root, path), hash) is { } live ? (live, null) : (null, Moved);
    }

    /// <summary>The file's text where it is there and hashes as <paramref name="hash"/>; null otherwise.</summary>
    private static string? Hashed(string file, string hash)
    {
        if (!File.Exists(file)) return null;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), hash, StringComparison.OrdinalIgnoreCase) ? Encoding.UTF8.GetString(bytes) : null;
    }
}
