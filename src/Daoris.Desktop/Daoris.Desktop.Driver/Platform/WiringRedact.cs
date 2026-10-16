using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What This Daoris may show of a value (WIRING1b, the this-Daoris design §5 and its second opinion's point 1): done at the
/// reader, before anything is serialized, so neither the window's answer nor the terminal's JSON ever holds a secret.
/// </summary>
/// <remarks>
/// <para><b>A key is <c>set</c>, and at most a minted deployment key's audit prefix.</b> No prefix is assumed: a key that is not
/// in the shape a deployment mints (<c>ApiKeyStore</c>: <c>dk_</c> and 48 hex) shows no part of itself.</para>
///
/// <para><b>An address loses its credentials, its query and its fragment</b>, where a token is carried; one that does not parse
/// is not shown at all, and nothing of why it did not parse is passed on, since a parser's sentence may quote it.</para>
///
/// <para><b>A sentence passed through verbatim</b> (a notice) has every address in it so cut, and every secret the reader was
/// handed taken out, wherever it appears.</para>
/// </remarks>
public static partial class WiringRedact
{
    /// <summary>What stands where a secret was cut out of a sentence.</summary>
    public const string Cut = "[key]";

    /// <summary>A deployment's minted key: the service's <c>ApiKeyStore</c>'s marker and 24 random bytes in lower-case hex.</summary>
    [GeneratedRegex("^dk_[0-9a-f]{48}$")]
    private static partial Regex MintedKey();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+.\-]*://[^\s""'<>()\[\]{}]+")]
    private static partial Regex AddressIn();

    /// <summary>The audit prefix a deployment's own <c>keys list</c> prints, for a minted key alone; null for any other.</summary>
    public static string? Prefix(string? key) =>
        key is not null && MintedKey().IsMatch(key.Trim()) ? key.Trim()[3..11] : null;

    /// <summary>An address without what may carry a credential; null where it is no absolute address.</summary>
    public static string? Address(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var address) || string.IsNullOrEmpty(address.Host)) return null;
        var port = address.IsDefaultPort ? "" : $":{address.Port}";
        return $"{address.Scheme}://{address.Host}{port}{address.AbsolutePath}";
    }

    /// <summary>A sentence with each address in it cut as <see cref="Address"/> cuts one, and each secret taken out.</summary>
    public static string? Text(string? text, IReadOnlyCollection<string> secrets)
    {
        if (text is null) return null;
        var cut = AddressIn().Replace(text, match => Address(match.Value) ?? Cut);
        return Scrub(cut, secrets);
    }

    /// <summary>Every secret taken out of a sentence, the longest first so a shorter one inside it leaves nothing behind.</summary>
    public static string Scrub(string text, IReadOnlyCollection<string> secrets)
    {
        foreach (var secret in secrets.Where(s => s.Length >= 4).OrderByDescending(s => s.Length))
        {
            text = text.Replace(secret, Cut, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// The last pass over a whole answer: every string in it scrubbed of the secrets the reader saw. Belt and braces, since
    /// each field was already cut where it was read; a field a later change adds is cut here even where it was not.
    /// </summary>
    public static void Sweep(JsonNode? node, IReadOnlyCollection<string> secrets)
    {
        switch (node)
        {
            case JsonObject map:
                foreach (var name in map.Select(pair => pair.Key).ToArray())
                {
                    if (map[name] is JsonValue value && value.TryGetValue<string>(out var text)) map[name] = Scrub(text, secrets);
                    else Sweep(map[name], secrets);
                }

                break;
            case JsonArray list:
                for (var at = 0; at < list.Count; at++)
                {
                    if (list[at] is JsonValue value && value.TryGetValue<string>(out var text)) list[at] = Scrub(text, secrets);
                    else Sweep(list[at], secrets);
                }

                break;
        }
    }
}
