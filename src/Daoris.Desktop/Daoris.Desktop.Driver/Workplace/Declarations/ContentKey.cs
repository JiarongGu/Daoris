using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// A content's key: SHA-256 of its raw bytes. A checkout's file and a commit's blob of the same bytes share it, and so share a parse,
/// since a parse reads the bytes alone; it is never compared with a <see cref="BlobId"/>, since a checkout's bytes may differ from their
/// blob's by line endings or a filter.
/// </summary>
internal readonly record struct ContentKey(string Hex)
{
    internal static ContentKey Of(ReadOnlySpan<byte> bytes) => new(Convert.ToHexStringLower(SHA256.HashData(bytes)));

    public override string ToString() => Hex;
}
