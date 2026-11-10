using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A session's proof that it pressed: an HMAC-SHA256 keyed by its delegate key's SHA-256 over the connection's hello nonce and the
/// press's canonical bytes, as the holder's proof is made (<see cref="SupervisorProof"/>). The verb sends it beside the press as
/// <c>delegate: { session, proof }</c>; the pipe checks it against the hashes Daoris holds for that session. The key never crosses the pipe.
/// </summary>
public static class DelegateProof
{
    /// <summary>The side field a press carries the proof in, beside its envelope as <c>key</c> and <c>hold</c> are.</summary>
    public const string Field = "delegate";

    /// <summary>
    /// A press's canonical bytes, as one compact JSON array, UTF-8: its module and route, so a proof over one press is never another's;
    /// the payload's raw JSON text as the line carries it (empty where it has none); and the request's own key, which binds its id. One
    /// function on both sides, so the verb and the pipe read the same bytes from the same values.
    /// </summary>
    public static byte[] Canonical(string module, string route, string payload, string requestKey)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(module);
            writer.WriteStringValue(route);
            writer.WriteStringValue(payload);
            writer.WriteStringValue(requestKey);
            writer.WriteEndArray();
        }

        return stream.ToArray();
    }

    /// <summary>The proof the verb sends, from the key itself; null where the key or the nonce is not what it should be.</summary>
    public static string? Prove(string key, string nonce, string module, string route, string payload, string requestKey) =>
        SupervisorProof.Prove(key, nonce, Canonical(module, route, payload, requestKey));

    /// <summary>Whether <paramref name="proof"/> is made by the key of any of <paramref name="hashes"/> over this nonce and press.</summary>
    public static bool Verify(
        IReadOnlyList<byte[]> hashes, string nonce, string module, string route, string payload, string requestKey, string? proof)
    {
        var form = Canonical(module, route, payload, requestKey);
        var proved = false;
        // Every hash is tried, so how long the check takes says nothing of which one matched.
        foreach (var hash in hashes) proved |= SupervisorProof.Verify(hash, nonce, form, proof);
        return proved;
    }
}
