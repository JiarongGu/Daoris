namespace Daoris.Driver;

/// <summary>
/// A content's key (<see cref="FileStamp.Hash"/>): a checkout's file and a commit's blob of the same bytes share it, and so a parse; it is
/// never compared with a <see cref="BlobId"/>, since a checkout's bytes may differ from their blob's by line endings or a filter.
/// </summary>
internal readonly record struct ContentKey(string Hex)
{
    internal static ContentKey Of(ReadOnlySpan<byte> bytes) => new(FileStamp.Hash(bytes));

    public override string ToString() => Hex;
}
