using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// An object's id as git makes it: the hash of its header (<c>&lt;type&gt; &lt;size&gt;\0</c>) and its bytes, in the repository's hash.
/// So bytes git answered for an id are proven that object's, or not, since a replacement answers an id with another object's bytes.
/// </summary>
internal static class GitObjectHash
{
    /// <summary>The id of an object of <paramref name="type"/> holding <paramref name="body"/>: SHA-1's, or SHA-256's with <paramref name="sha256"/>.</summary>
    internal static string Of(string type, ReadOnlySpan<byte> body, bool sha256)
    {
        using var hash = IncrementalHash.CreateHash(sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{type} {body.Length}\0")));
        hash.AppendData(body);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Whether <paramref name="body"/> is the object <paramref name="id"/> names, in the hash the id's length says.</summary>
    internal static bool Proves(string type, ReadOnlySpan<byte> body, string id) =>
        IsId(id) && string.Equals(Of(type, body, id.Length == 64), id, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="text"/> is a full object id: 40 hex digits, or 64 in a SHA-256 repository.</summary>
    internal static bool IsId(string text) => text.Length is 40 or 64 && text.All(char.IsAsciiHexDigit);
}
