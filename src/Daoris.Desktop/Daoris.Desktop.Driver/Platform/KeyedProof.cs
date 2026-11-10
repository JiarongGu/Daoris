using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// The keys and proofs the holder and a session's delegate share: a 32-byte key in hex, its SHA-256 as what Daoris keeps, and an
/// HMAC-SHA256 keyed by that hash over a connection's hello nonce and a form's canonical bytes, checked in constant time. The key itself
/// never crosses the pipe.
/// </summary>
public static class KeyedProof
{
    /// <summary>The key's length in bytes, and so its hex text's in characters twice that.</summary>
    public const int KeyBytes = 32;

    /// <summary>The nonce's length in bytes, drawn for each connection's hello.</summary>
    public const int NonceBytes = 32;

    /// <summary>A new key, in hex.</summary>
    public static string Draw() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KeyBytes));

    /// <summary>A fresh nonce for one connection's hello, in hex.</summary>
    public static string Nonce() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceBytes));

    /// <summary>The key's SHA-256, which the instance keeps in memory and the proof is keyed by; null for a text that is no key.</summary>
    public static byte[]? Hash(string? key) => Read(key) is { } bytes ? SHA256.HashData(bytes) : null;

    /// <summary>The key's bytes, or null where <paramref name="key"/> is not 64 hex characters.</summary>
    public static byte[]? Read(string? key)
    {
        if (key is not { Length: KeyBytes * 2 } || !key.All(Uri.IsHexDigit)) return null;
        return Convert.FromHexString(key);
    }

    /// <summary>The proof, in hex, from the key's hash, the nonce (hex) and the canonical form; null where the nonce is not hex.</summary>
    public static string? Prove(byte[] hash, string nonce, byte[] form)
    {
        if (Bytes(nonce) is not { } drawn) return null;
        byte[] message = [.. drawn, .. form];
        return Convert.ToHexStringLower(HMACSHA256.HashData(hash, message));
    }

    /// <summary>The proof the verb sends, from the key itself; null where the key or the nonce is not what it should be.</summary>
    public static string? Prove(string key, string nonce, byte[] form) =>
        Hash(key) is { } hash ? Prove(hash, nonce, form) : null;

    /// <summary>Whether <paramref name="proof"/> is the proof over <paramref name="nonce"/> and <paramref name="form"/> by the key whose hash is <paramref name="hash"/>, compared in constant time.</summary>
    public static bool Verify(byte[] hash, string nonce, byte[] form, string? proof)
    {
        if (Prove(hash, nonce, form) is not { } expected || proof is not { Length: 64 } || !proof.All(Uri.IsHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(proof));
    }

    private static byte[]? Bytes(string? hex) =>
        hex is { Length: NonceBytes * 2 } && hex.All(Uri.IsHexDigit) ? Convert.FromHexString(hex) : null;
}
