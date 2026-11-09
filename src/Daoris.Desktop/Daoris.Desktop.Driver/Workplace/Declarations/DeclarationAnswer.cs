using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A declaration's bytes by their content key, and the blob id they hashed to where an ask at a commit proved one. What the store keeps
/// by content key carries no id, so a checkout's file is never taken for a commit's blob.
/// </summary>
internal sealed record DeclarationBytes(ContentKey Key, ImmutableArray<byte> Bytes, BlobId? Id)
{
    private const long Held = 160;

    internal int Size => Bytes.Length;

    /// <summary>What it holds in memory: its bytes, its key's text and the record.</summary>
    internal long Weight => Bytes.Length + Held + DeclarationBound.Text(Key.Hex);

    /// <summary>Bytes the store takes as its own, keyed by their content.</summary>
    internal static DeclarationBytes Of(byte[] bytes, BlobId? id) => new(ContentKey.Of(bytes), ImmutableCollectionsMarshal.AsImmutableArray(bytes), id);

    /// <summary>The bytes as text, read as git's text output is read: UTF-8, a leading byte-order mark taken as the mark it is.</summary>
    internal string Text()
    {
        using var reader = new StreamReader(new MemoryStream(ImmutableCollectionsMarshal.AsArray(Bytes)!, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

/// <summary>An entry of a hashed tree: its mode, its object's type and its object's id.</summary>
internal readonly record struct DeclarationEntry(string Mode, string Type, string Id)
{
    /// <summary>Whether it is a regular file's blob, the one entry a kind parses: a link, a folder or a submodule is said as what it is.</summary>
    internal bool Regular => Type == "blob" && Mode is "100644" or "100755";
}

/// <summary>
/// What one ask of the store answers: <see cref="Present"/>, <see cref="Absent"/> verified, or <see cref="Unread"/> with its cause, which
/// is kept for none.
/// </summary>
internal abstract record DeclarationAnswer
{
    private DeclarationAnswer()
    {
    }

    /// <summary>
    /// The declaration stands: its content, with the blob id its listing proved at a commit; and at a commit its entry. An entry that is no
    /// regular file has no content parsed; bytes git answered by path while a replacement could stand carry no id and no entry.
    /// </summary>
    internal sealed record Present(DeclarationBytes? Content, DeclarationEntry? Entry) : DeclarationAnswer;

    /// <summary>No file in a folder that stands, or no entry by the name in a hashed tree.</summary>
    internal sealed record Absent : DeclarationAnswer
    {
        internal static readonly Absent One = new();
    }

    /// <summary>The read failed, said in its words.</summary>
    internal sealed record Unread(DeclarationUnread Cause, string Why) : DeclarationAnswer;

    /// <summary>The answer's version: a new unread at each unread answer, the same as no version.</summary>
    internal DeclarationVersion Version => this switch
    {
        Present { Entry: null or { Regular: true }, Content: { } content } => DeclarationVersion.Of(content.Key),
        Present { Entry: { } entry } present => DeclarationVersion.Of(present.Content?.Key ?? new ContentKey(entry.Id), entry.Mode),
        Unread unread => DeclarationVersion.Unread(unread.Cause),
        _ => DeclarationVersion.Absent,
    };
}
