namespace Daoris.Driver;

/// <summary>
/// A blob's id at a commit, as the store's hashed listing names it. Bytes are taken as its only where they hash to it, since git answers
/// a replaced id with the replacement's bytes.
/// </summary>
internal readonly record struct BlobId(string Hex)
{
    /// <summary>Whether <paramref name="bytes"/> are this blob's.</summary>
    internal bool Proves(ReadOnlySpan<byte> bytes) => GitObjectHash.Proves("blob", bytes, Hex);

    public override string ToString() => Hex;
}
